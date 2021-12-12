

using Diz.Core.model;
using Diz.Core.model.snes;

namespace Diz.Test.Utils
{
    public class AssemblyPipelineTester
    {
        public Data Source { get; set; }
        public string ExpectedAsmOutput { get; set; }

        public virtual void Test()
        {
            var outputAsmToTest = LogWriterHelper.ExportAssembly(Source);
            LogWriterHelper.AssertAssemblyOutputEquals(ExpectedAsmOutput, outputAsmToTest);
        }

        public static AssemblyPipelineTester SetupFromResource(Data input, string expectedAsmOutputResource)
        {
            return new AssemblyPipelineTester
            {
                Source = input,
                ExpectedAsmOutput = EmbeddedResourceDataAttribute.ReadResource(expectedAsmOutputResource)
            };
        }
    }
}