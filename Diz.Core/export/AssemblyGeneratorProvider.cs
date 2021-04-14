using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.export.assemblyGenerators;

namespace Diz.Core.export
{
    public static class AssemblyGeneratorRegistration
    {
        public static IEnumerable<Type> AssemblyGeneratorTypes()
        {
            return new List<Type>
            {
                typeof(AssemblyGeneratePercent),
                typeof(AssemblyGenerateEmpty),
                typeof(AssemblyGenerateLabel),
                typeof(AssemblyGenerateCode),
                typeof(AssemblyGenerateOrg),
                typeof(AssemblyGenerateMap),
                typeof(AssemblyGenerateIncSrc),
                typeof(AssemblyGenerateBankCross),
                typeof(AssemblyGenerateIndirectAddress),
                typeof(AssemblyGenerateProgramCounter),
                typeof(AssemblyGenerateOffset),
                typeof(AssemblyGenerateDataBytes),
                typeof(AssemblyGenerateComment),
                typeof(AssemblyGenerateDataBank),
                typeof(AssemblyGenerateDirectPage),
                typeof(AssemblyGenerateMFlag),
                typeof(AssemblyGenerateXFlag),
                typeof(AssemblyGenerateLabelAssign),
            };
        }

        public static Dictionary<string, AssemblyPartialLineGenerator> Create()
        {
            return AssemblyGeneratorTypes()
                .Select(gType => (AssemblyPartialLineGenerator)Activator.CreateInstance(gType))
                .ToDictionary(generator => generator.Token);
        }
    }
}