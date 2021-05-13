using System.Collections.Generic;
using System.Linq;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;
using Diz.LogWriter;
using Diz.Test.TestData;
using Diz.Test.Utils;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Diz.Test.Tests.LogCreatorTests
{
    public sealed class LogCreatorTests
    {
        private readonly ITestOutputHelper debugWriter;

        public LogCreatorTests(ITestOutputHelper debugWriter)
        {
            this.debugWriter = debugWriter;
        }

        [Fact]
        public void TestLabelShit()
        {
            var data = SpaceCatsRom.CreateInputRom();
            
            void TestIt()
            {
                var test22Label = (Label) data.SnesAddressSpace.ChildSources[0].ByteSource.Bytes[6].Annotations[3];
                var testDataLabel = (Label) data.SnesAddressSpace.Bytes[0x808000 + 0x5B].Annotations[0];

                Assert.Equal("Test22", test22Label.Name);
                Assert.Equal("Test_Data", testDataLabel.Name);

                // TODO // Assert.Equal("Space Cats 2: Rise of Lopsy Dumpwell", test22Label.Parent.ParentByteSource.Name);
                // TODO // Assert.Equal("SNES Main Cpu BUS", testDataLabel.Parent.ParentByteSource.Name);
                
                Assert.Equal(6, test22Label.Parent.ParentIndex);
                Assert.Equal(0x808000 + 0x5B, testDataLabel.Parent.ParentIndex);
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
            var data = SpaceCatsRom.CreateInputRom();
            var assemblyOutput = LogWriterHelper.ExportAssembly(data, logCreator =>
            {
                logCreator.Settings = logCreator.Settings with {OutputExtraWhitespace = false};
                
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
            var data = SpaceCatsRom.CreateInputRom();
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
            var data = SpaceCatsRom.CreateInputRom();

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
            var data = SpaceCatsRom.CreateInputRom();
            
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
            Assert.Equal(parentIndex, label.Parent.ParentIndex);
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