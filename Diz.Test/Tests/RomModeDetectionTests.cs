using Diz.Core.Interfaces;
using Diz.Core.model.project;
using Diz.Core.util;
using Diz.Cpu._65816.import;
using Diz.Test.Tests.RomInterfaceTests;
using Diz.Test.Utils;
using FluentAssertions;
using LightInject;
using Xunit;

namespace Diz.Test.Tests;

public class RomModeDetectionTests : ContainerFixture
{
    [Inject] private readonly IReadFromFileBytes fileReader = null!;
    [Inject] private readonly ISampleRomTestData sampleDataFixture = null!;

    protected override void Configure(IServiceRegistry serviceRegistry)
    {
        base.Configure(serviceRegistry);
        serviceRegistry.Register<ISampleRomTestData, SampleRomTestDataFixture>();
    }

    [FactOnlyIfFilePresent(new[]{CartNameData.ExampleLoRomFile})]
    public void TestRomDetectionLoRom()
    {
        var detectRomMapMode = RomUtil.DetectRomMapMode(fileReader.ReadRomFileBytes(CartNameData.ExampleLoRomFile), out var detectedValidRomMapType);
        detectedValidRomMapType.Should().Be(true);
        detectRomMapMode.Should().Be(RomMapMode.LoRom);
    }
    
    [FactOnlyIfFilePresent(new[]{CartNameData.ExampleHiRomFile})]
    public void TestRomDetectionHiRom()
    {
        var detectRomMapMode = RomUtil.DetectRomMapMode(
            fileReader.ReadRomFileBytes(CartNameData.ExampleHiRomFile), 
            out var detectedValidRomMapType);
        
        detectedValidRomMapType.Should().Be(true);
        detectRomMapMode.Should().Be(RomMapMode.HiRom);
    }
    
    [FactOnlyIfFilePresent(new[]{CartNameData.ExampleHiRomFile})]
    public void TestRomDetectionFastRom()
    {
        var analyzer = new SnesRomAnalyzer();
        analyzer.Analyze(CartNameData.ExampleHiRomFile);

        analyzer.Filename.Should().Be(CartNameData.ExampleHiRomFile);
        analyzer.AnalysisResults!.RomMapMode.Should().Be(RomMapMode.HiRom);
        analyzer.AnalysisResults.RomSpeed.Should().Be(RomSpeed.FastRom);
        analyzer.RomBytes.Should().HaveCount(0x400000);
        analyzer.RomSettingsOffset.Should().Be(RomUtil.HiromSettingOffset);
    }
    
    [Fact]
    public void TestRomDetectionGeneratedRom()
    {
        sampleDataFixture.Should().NotBeNull();
        var detectRomMapMode = RomUtil.DetectRomMapMode(sampleDataFixture.SampleRomBytes, out var detectedValidRomMapType);
        detectedValidRomMapType.Should().Be(true);
        detectRomMapMode.Should().Be(RomMapMode.LoRom);
    }
}