using Diz.Test.TestData;
using Diz.Test.Utils;
using Xunit;

namespace Diz.Test.Tests.Asar
{
    public class AssemblyPipeline
    {
        public static TheoryData<AssemblyPipelineTester> PipelineTesters => new()
        {
            AssemblyPipelineTester.SetupFromResource(TinyHiRomSample.TinyHiRomWithExtraLabel, "Diz.Test/Resources/asartestrun.asm")
        };

        /*public static IReadOnlyList<byte> AssemblyRom => AsarRunner.AssembleToRom(@"
            hirom

            SNES_VMADDL = $002116
            ; SNES_VMADDL = $7E2116

            ORG $C00000

            STA.W SNES_VMADDL"
        );*/
        
        [Theory(Skip = "temp disabled til log exporter is less busted. TODO")]
        [MemberData(nameof(PipelineTesters))]
        public static void TestRom2(AssemblyPipelineTester romTester)
        {
            romTester.Test();
        }
    }
}