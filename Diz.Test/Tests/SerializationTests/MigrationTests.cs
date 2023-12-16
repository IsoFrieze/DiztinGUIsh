using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diz.Test
{
    public class MigrationTests
    {
        
        [Theory]
        [MemberData(nameof(Harnesses))]
        public void TestMigrationFailsIfOutsideBounds(Harness harness)
        {
            var runner = new MigrationRunner(harness.MigrationObjs) {
                StartingSaveVersion = harness.RunnerStart,
                TargetSaveVersion = harness.RunnerTarget,
            };
            
            harness.Run(() => runner.OnLoadingAfterAddLinkedRom(null));
            harness.Verify();
        }

        [Fact] public static void TestNullElementFails() =>
            new MigrationRunner { Migrations = {null} }.Invoking(r=>r.OnLoadingAfterAddLinkedRom(null))
                .Should().Throw<DizMigrationException>().WithMessage("*all migrations must be non-null*");
        
        [Fact] public static void TestInvalidStartAndTarget() =>
            new MigrationRunner { StartingSaveVersion = 5, TargetSaveVersion = 4}.Invoking(r=>r.OnLoadingAfterAddLinkedRom(null))
                .Should().Throw<DizMigrationException>().WithMessage("*starting migration version is greater than target version*");

        #region HarnessData
        public static TheoryData<Harness> Harnesses => new()
        {
            new Harness
            {
                RunnerStart = 100,
                RunnerTarget = 101,
                Migrations =
                {
                    CreateMigrationMock(99,  false, "outtaRangeLow"),
                    CreateMigrationMock(100,  true, "inRange1"),
                    CreateMigrationMock(100,  true, "inRange2"),
                    CreateMigrationMock(101,  false, "outtaRangeHigh"),
                }
            },
            new Harness
            {
                ExpectedException = "*migration out of sequence. version 102 not valid here. needed to upgrade from 100*",
                RunnerStart = 100,
                RunnerTarget = 103,
                Migrations =
                {
                    CreateMigrationMock(100,  true),
                    // leave a gap, should throw error.
                    CreateMigrationMock(102,  false),
                }
            },
            new Harness
            {
                ExpectedException = "*all migrations must >= other migrations in the sequence*",
                RunnerStart = 100,
                RunnerTarget = 101,
                Migrations =
                {
                    CreateMigrationMock(101,  false),
                    CreateMigrationMock(100,  false),
                }
            },
            new Harness {
                ExpectedException = "*migration failed. we were trying to*",
                RunnerStart = 100, RunnerTarget = 101,
                Migrations = {
                    CreateMigrationMock(99,  false),
                    CreateMigrationMock(99,  false),
                }
            },
            new Harness {
                ExpectedException = "*migration failed. we were trying to*",
                RunnerStart = 100, RunnerTarget = 150,
                Migrations = {
                    CreateMigrationMock(100,  true),
                    CreateMigrationMock(101,  true),
                }
            },
            new Harness {
                RunnerStart = 100, RunnerTarget = 100,
                Migrations = {
                    CreateMigrationMock(99,  false),
                    CreateMigrationMock(100,  false),
                    CreateMigrationMock(101,  false),
                }
            }
            
            // 
        };
        #endregion

        #region Backend

        public class MigrationMock
        {
            public Mock<IMigration> Mock;
            public Action<Mock<IMigration>> Verify;
        }

        private static MigrationMock CreateMigrationMock(int saveVersion, bool shouldHaveRun=false, string extraName = "")
        {
            var mock = new Mock<IMigration>(MockBehavior.Strict);

            mock.SetupGet(x => x.AppliesToSaveVersion)
                .Returns(saveVersion);

            mock.Setup(x => x
                .OnLoadingAfterAddLinkedRom(It.IsAny<IAddRomDataCommand>()));

            mock.Name += $"--{extraName}-- (v{mock.Object.AppliesToSaveVersion})";
            
            return new MigrationMock
            {
                Mock = mock,
                Verify = m => {
                    m.Verify(
                        migration => migration.OnLoadingAfterAddLinkedRom(null), 
                        Times.Exactly(shouldHaveRun ? 1 : 0));
                }
            };
        }

        public class Harness
        {
            public int RunnerStart;
            public int RunnerTarget;
            public string ExpectedException = null;
            public readonly List<MigrationMock> Migrations = new();

            public List<IMigration> MigrationObjs => Migrations.Select(mock => mock.Mock.Object).ToList();
            
            public void Verify()
            {
                foreach (var migration in Migrations)
                {
                    migration.Verify(migration.Mock);
                }
            }

            public void Run(Action action)
            {
                if (ExpectedException == null)
                {
                    action();
                }
                else
                {
                    action.Should().Throw<DizMigrationException>()
                        .WithMessage(ExpectedException);
                }
            }
        }
        
        #endregion
    }
}