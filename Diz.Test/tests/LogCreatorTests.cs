using System.Collections.Generic;
using System.Linq;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;
using Diz.Test.Utils;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Diz.Test.tests
{
    public sealed class LogCreatorTests
    {
        private Data CreateInputRom()
        {
            var bytes = new List<ByteEntry>
            {
                // --------------------------
                // highlighting a particular section here
                // we will use this for unit tests as well.

                // SNES address: 808000
                // instruction: LDA.W Test_Data,X
                new()
                {
                    Byte = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint,
                    DataBank = 0x80, DirectPage = 0x2100
                },
                new() {Byte = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data
                new() {Byte = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data

                // SNES address: 808003
                // instruction: STA.W $0100,X
                new() {Byte = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},

                // SNES address: 808006
                // instruction: DEX
                new()
                {
                    Byte = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100,
                    Annotations = {new Label {Name = "Test22"}}
                },

                // SNES address: 808007
                // instruction: BPL CODE_808000
                new()
                {
                    Byte = 0x10, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint,
                    DataBank = 0x80, DirectPage = 0x2100
                },
                new() {Byte = 0xF7, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
            };

            var actualRomBytes = new ByteSource
            {
                Name = "Space Cats 2: Rise of Lopsy Dumpwell",
                Bytes = new StorageList<ByteEntry>(bytes)
            };

            // var data = new TestData()
            var data = new Data()
                .PopulateFromRom(actualRomBytes, RomMapMode.LoRom, RomSpeed.FastRom);

            // another way to add comments, adds it to the SNES address space instead of the ROM.
            // retrievals should be unaffected.
            data.Labels.AddLabel(0x808000 + 0x5B, new Label {Name = "Test_Data", Comment = "Pretty cool huh?"});

            return data;
        }

        private readonly ITestOutputHelper debugWriter;
        public LogCreatorTests(ITestOutputHelper debugWriter)
        {
            this.debugWriter = debugWriter;
        }

        [Fact]
        public void TestLabelShit()
        {
            var data = CreateInputRom();
            
            void TestIt()
            {
                var test22Label = (Label) data.SnesAddressSpace.ChildSources[0].ByteSource.Bytes[6].Annotations[3];
                var testDataLabel = (Label) data.SnesAddressSpace.Bytes[0x808000 + 0x5B].Annotations[0];

                Assert.Equal("Test22", test22Label.Name);
                Assert.Equal("Test_Data", testDataLabel.Name);

                // TODO // Assert.Equal("Space Cats 2: Rise of Lopsy Dumpwell", test22Label.Parent.ParentByteSource.Name);
                // TODO // Assert.Equal("SNES Main Cpu BUS", testDataLabel.Parent.ParentByteSource.Name);
                
                Assert.Equal(6, test22Label.Parent.ParentByteSourceIndex);
                Assert.Equal(0x808000 + 0x5B, testDataLabel.Parent.ParentByteSourceIndex);
            }

            TestIt();
            data.Labels.AddTemporaryLabel(0x808000, new Label {Name="CODE_808000"});
            TestIt();

            var codeLabel = data.Labels.GetLabel(0x808000);
            Assert.NotNull(codeLabel);
            Assert.Equal("CODE_808000", codeLabel.Name);
            Assert.Null(codeLabel.Parent);
        }

        [Fact]
        public void TestAnnotationParentWhenPopulatedFrom()
        {
            static void TestParents(ByteEntry byteEntry9)
            {
                var by1 = byteEntry9.Byte;
                Assert.NotNull(by1);
                Assert.Equal(0xCA, by1.Value);
                // TODO // Assert.NotNull(byteEntry9.ParentByteSource);

                Assert.Single(byteEntry9.Annotations);

                foreach (var annotation in byteEntry9.Annotations)
                {
                    Assert.NotNull(annotation.Parent);
                    Assert.True(ReferenceEquals(byteEntry9, annotation.Parent));
                }
            }

            var actualRomBytes = new ByteSource
            {
                Bytes = new StorageList<ByteEntry>(new List<ByteEntry>
                {
                    new()
                    {
                        Byte = 0xCA,
                    }
                })
            };

            var byteEntry1 = actualRomBytes.Bytes[0];
            TestParents(byteEntry1);

            var data = new Data().PopulateFromRom(actualRomBytes, RomMapMode.LoRom, RomSpeed.FastRom);

            var byteEntry2 = data.RomByteSource.Bytes[0];
            TestParents(byteEntry2);
        } 

        [Theory]
        [EmbeddedResourceData("Diz.Test/Resources/samplerom-a-few-lines.asm")]
        public void TestAFewLines(string expectedAsm)
        {
            var data = CreateInputRom();
            var assemblyOutput = LogWriterHelper.ExportAssembly(data, logCreator =>
            {
                var settings = logCreator.Settings;
                settings.OutputExtraWhitespace = false;
                logCreator.Settings = settings;
                
                logCreator.ProgressChanged += (_, progressEvent) =>
                {
                    switch (progressEvent.State)
                    {
                        case LogCreator.ProgressEvent.Status.StartTemporaryLabelsGenerate:
                            {
                                var actualDict = logCreator.Data.Labels.Labels.ToDictionary(p => p.Key);
                                TestOriginal2Labels(actualDict);
                            }
                            break;
                        case LogCreator.ProgressEvent.Status.DoneTemporaryLabelsGenerate:
                            {
                                var actualDict = logCreator.Data.Labels.Labels.ToDictionary(p => p.Key);
                                TestOriginal2Labels(actualDict);
                                TestLabelInDict(actualDict, 0x808000, "CODE_808000", int.MinValue);
                            }
                            break;
                    }
                };
            });

            LogWriterHelper.AssertAssemblyOutputEquals(expectedAsm, assemblyOutput, debugWriter);
        }

        [Fact]
        public void TestLabelTracker()
        {
            var data = CreateInputRom();
            foreach (var l in data.Labels.Labels)
            {
                Assert.NotNull(l.Value);
            }
            
            // TODO: cleanup
            
            /*Dictionary<int, IReadOnlyLabel> cachedUnvisitedLabels = new();
            foreach (var (snesAddress, label) in data.Labels.Labels)
            {
                cachedUnvisitedLabels.Add(snesAddress, label);
            }*/
            
            /*var labelMock = new Mock<IReadOnlyLabelProvider>();
            labelMock
            
            var dataMock = new Mock<ILogCreatorDataSource>();
            dataMock.SetupGet(p => p.Labels).Returns(dataMock.Object);
                /*.Setup(m => m.ConvertPCtoSnes(
                    It.IsAny<int>())).Returns(() =>
                {
                    return expectedOffset;
                });#1#

            var logCreatorMock = new Mock<ILogCreatorForGenerator>();
            logCreatorMock.SetupGet(prop => prop.Data).Returns(dataMock.Object);
            
            var labelTracker = new LabelTracker(logCreatorMock.Object);*/
            // Debug.Assert(InputRom);
        }

        [Fact]
        public void TestParent()
        {
            var data = CreateInputRom();

            var annotations = data.RomByteSource
                .GetOnlyOwnAnnotations<Label>().ToList();

            Assert.Single(annotations);
            
            foreach (var item in annotations)
            {
                Assert.NotNull(item);
                Assert.NotNull(item.Parent);
            }
        }

        [Fact]
        public void TestLabelAccess()
        {
            var data = CreateInputRom();
            
            var actualDict = data.SnesAddressSpace
                .GetAnnotationsIncludingChildrenEnumerator<Label>()
                .ToDictionary(pair => pair.Key);

            Assert.Equal(2, actualDict.Count);

            TestOriginal2Labels(actualDict);
            Assert.Equal(default, actualDict.GetValueOrDefault(0x808008)); // made up address with no label
        }

        private static void TestOriginal2Labels(IReadOnlyDictionary<int, KeyValuePair<int, Label>> actualDict)
        {
            TestLabelInDict(actualDict, 0x808006, "Test22", 6);
            TestLabelInDict(actualDict, 0x80805B, "Test_Data");
        }

        // set parentIndex to int.MinValue to skip the parent check
        private static void TestLabelInDict(IReadOnlyDictionary<int, KeyValuePair<int, Label>> actual, int snesAddress, string expectedName, int parentIndex = -1)
        {
            parentIndex = parentIndex == -1 ? snesAddress : parentIndex;
            
            var label = actual.GetValueOrDefault(snesAddress);
            Assert.NotEqual(default, label);
            Assert.Equal(snesAddress, label.Key);
            TestLabel(label.Value, snesAddress, expectedName, parentIndex);
        }

        private static void TestLabel(Label label, int snesAddress, string expectedName, int parentIndex = -1)
        {
            Assert.NotNull(label);
            Assert.Equal(expectedName, label.Name);

            if (parentIndex == int.MinValue) 
                return;
            
            parentIndex = parentIndex == -1 ? snesAddress : parentIndex;
            Assert.Equal(parentIndex, label.Parent.ParentByteSourceIndex);
        }

        [Theory]
        [EmbeddedResourceData("Diz.Test/Resources/emptyrom.asm")]
        public void TestEmptyRom(string expectedAsm)
        {
            var result = LogWriterHelper.ExportAssembly(new Data());
            LogWriterHelper.AssertAssemblyOutputEquals(expectedAsm, result);
        }
    }
}