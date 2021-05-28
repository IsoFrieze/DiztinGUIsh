using Xunit.Abstractions;

namespace Diz.Test.Utils
{
    public abstract class XmlTestUtilBase
    {
        public readonly ITestOutputHelper TestOutputHelper;
        public XmlTestUtils XmlTestUtils => new() {Output = TestOutputHelper}; 
        public XmlTestUtilBase(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
        }
    }
}