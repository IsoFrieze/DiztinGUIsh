using System.IO;
using Diz.Core.serialization.xml_serializer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diz.Test
{
    public class MigrationTests
    {
        private static IMigration CreateMigrationMock(int saveVersion)
        {
            var mock = new Mock<IMigration>();

            mock.SetupGet(x => x.AppliesToSaveVersion)
                .Returns(saveVersion);

            return mock.Object;
        }
        
        [Fact]
        public void TestMigration()
        {
            var runner = new MigrationRunner
            {
                StartingSaveVersion = 100,
                TargetSaveVersion = 102,
                Migrations =
                {
                    CreateMigrationMock(100),
                    // leave a gap, should throw error.
                    CreateMigrationMock(102),
                }
            };

            runner
                .Invoking(x => x.OnLoadingAfterAddLinkedRom(null))
                .Should().Throw<InvalidDataException>().WithMessage("internal: couldn't find migration for version# 101");
        }
    }
}