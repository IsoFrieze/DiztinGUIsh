using Diz.Core.model.snes;

namespace Diz.Test.Utils
{
    public class AssemblyPipelineTester
    {
        public Data Source { get; init; }
        public string ExpectedAsmOutput { get; init; }

        public virtual void Test()
        {
            var outputAsmToTest = LogWriterHelper.ExportAssembly(Source);
            LogWriterHelper.AssertAssemblyOutputEquals(ExpectedAsmOutput, outputAsmToTest);
        }

        public static AssemblyPipelineTester SetupFromResource(Data input, string expectedAsmOutputResource)
        {
            return new()
            {
                Source = input,
                ExpectedAsmOutput = EmbeddedResourceDataAttribute.ReadResource(expectedAsmOutputResource)
            };
        }
    }
}