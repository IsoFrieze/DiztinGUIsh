using System.Linq;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.model.snes;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Test.Utils;
using FluentAssertions;
using LightInject;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test;

public class LogCreatorTests
{
    // TODO: re-enable all of this, it was just for testing purposes.
    // these tests all work.
    
    /*private const string ExpectedRaw =
        //          label:       instructions                         ;PC    |rawbytes|ia
        "                        lorom                                ;      |        |      ;  \r\n" +
        "                                                             ;      |        |      ;  \r\n" +
        "                                                             ;      |        |      ;  \r\n" +
        "                        ORG $808000                          ;      |        |      ;  \r\n" +
        "                                                             ;      |        |      ;  \r\n" +
        "           CODE_808000: LDA.W Test_Data,X                    ;808000|BD5B80  |80805B;  \r\n" +
        "                        STA.W $0100,X                        ;808003|9D0001  |800100;  \r\n" +
        "           Test22:      DEX                                  ;808006|CA      |      ;  \r\n" +
        "                        BPL CODE_808000                      ;808007|10F7    |808000;  \r\n" +
        "                                                             ;      |        |      ;  \r\n" +
        "                        Test_Data = $80805B                  ;      |        |      ;  \r\n";*/

    // [Fact(Skip = "need to reset the .asm file")]
    // public void TestAFewLines()
    // {
    //     var data = CreateSampleData();
    //     LogWriterHelper.AssertAssemblyOutputEquals(ExpectedRaw, LogWriterHelper.ExportAssembly(data, creator =>
    //     {
    //         creator.Settings = new LogWriterSettings
    //         {
    //             OutputExtraWhitespace = false    
    //         };
    //     }), debugWriter);
    // }

    [Fact]
    public void TestServices()
    {
        Service.Recreate();

        Service.Container.Register<IFilesystemService>(x => new FilesystemService());
        Service.Container.Register<IFilesystemService>(x => new Mock<IFilesystemService>().Object);

        Service.Container.GetAllInstances(typeof(IFilesystemService)).Count().Should().Be(1);

        var fs = Service.Container.GetInstance<IFilesystemService>();
        fs.GetType().Name.Should().Contain("Proxy");
        
        Service.Container.Register<IFilesystemService>(x => new FilesystemService());
    }
        
    [Fact]
    public void TestLabelCount()
    {
        /*Data data = null; // TODO: CreateSampleData();
            
        // should give us "Test22" and "Test_Data"
        Assert.Equal(2, data.Labels.Labels.Count());*/
    }
        
    // [Fact(Skip = "need to reset the .asm file")]
    // public void TestOneLine()
    // {
    //     var exportAssembly = LogWriterHelper.ExportAssembly(DataUtils.FactoryCreate());
    //     LogWriterHelper.AssertAssemblyOutputEquals(ExpectedRaw, exportAssembly);
    // }
        
    // [Theory]
    // [EmbeddedResourceData("Diz.Test/Resources/emptyrom.asm")]
    // public void TestEmptyRom(string expectedAsm)
    // {
    //     /*var emptyData = DataUtils.FactoryCreate();
    //     emptyData.ArchProvider.AddApiProvider(new SnesApi(emptyData));
    //     var result = LogWriterHelper.ExportAssembly(emptyData);
    //     LogWriterHelper.AssertAssemblyOutputEquals(expectedAsm, result, debugWriter);*/
    // }

    [Fact]
    public void Q()
    {
        // var x = true;
        // x.Should().BeTrue();
    }
        
        
    /*private readonly ITestOutputHelper debugWriter;*/
    public LogCreatorTests(/*ITestOutputHelper debugWriter*/)
    {
        /*this.debugWriter = debugWriter;*/
            
        AppServicesForTests.RegisterNormalAppServices();
    }

    /*
    private Data CreateSampleData()
    {
        var data = DataUtils.FactoryCreate();
        data.RomMapMode = RomMapMode.LoRom;
        data.RomSpeed = RomSpeed.FastRom;
        data.RomBytes = new RomBytes
        {
            // --------------------------
            // highlighting a particular section here
            // we will use this for unit tests as well.

            // CODE_808000: LDA.W Test_Data,X
            new()
            {
                Rom = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 }, // Test_Data
            new() { Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 }, // Test_Data

            // STA.W $0100,X
            new() { Rom = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },

            // DEX
            new() { Rom = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100 },

            // BPL CODE_808000
            new()
            {
                Rom = 0x10, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0xF7, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
        };

        data.Labels.AddLabel(0x808000 + 0x06, new Label { Name = "Test22" });
        data.Labels.AddLabel(0x808000 + 0x5B, new Label { Name = "Test_Data", Comment = "Pretty cool huh?" });
        return data;
    }*/
}