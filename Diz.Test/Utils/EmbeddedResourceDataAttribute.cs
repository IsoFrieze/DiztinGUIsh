using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Diz.Core.util;
using Xunit.Sdk;

namespace Diz.Test.Utils
{
    // based on https://patriksvensson.se/2017/11/using-embedded-resources-in-xunit-tests/
    public sealed class EmbeddedResourceDataAttribute : DataAttribute
    {
        private readonly string[] resourcesToRead;
        
        public EmbeddedResourceDataAttribute(params string[] resourcesToRead)
        {
            this.resourcesToRead = resourcesToRead;
        }

        public static string ReadResource(string resource)
        {
            var assembly = typeof(EmbeddedResourceDataAttribute).GetTypeInfo().Assembly;
            return Util.ReadManifestData(assembly, resource);
        } 

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            return resourcesToRead
                .Select(ReadResource)
                .Select(resource => new[] {resource});
        }
    }
}