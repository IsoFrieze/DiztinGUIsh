using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Diz.Core.model;

namespace Diz.Core.export
{
    // setup-specific parts of LogCreator
    // TODO: probably should move this stuff into a helper class so it's not a partial
    public partial class LogCreator
    {
        protected class AssemblerHandler : Attribute
        {
            public string Token;
            public int Length;
        }

        public class OutputResult
        {
            public bool Success;
            public int ErrorCount = -1;
            public LogCreator LogCreator;
            public string OutputStr = ""; // only set if outputString=true
        }


        // DONT use directly [except to setup the caching]
        private static Dictionary<string, Tuple<MethodInfo, int>> _parametersCache;

        // SAFE to use directly.
        protected static Dictionary<string, Tuple<MethodInfo, int>> Parameters
        {
            get
            {
                CacheAssemblerAttributeInfo();
                return _parametersCache;
            }
        }

        protected static void CacheAssemblerAttributeInfo()
        {
            if (_parametersCache != null)
                return;

            _parametersCache = new Dictionary<string, Tuple<MethodInfo, int>>();

            var methodsWithAttributes = typeof(LogCreator)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(
                    x => x.GetCustomAttributes(typeof(AssemblerHandler), false).FirstOrDefault() != null
                );

            foreach (var method in methodsWithAttributes)
            {
                var assemblerHandler = method.GetCustomAttribute<AssemblerHandler>();
                var token = assemblerHandler.Token;
                var length = assemblerHandler.Length;

                // check your method signature if you hit this stuff.
                Debug.Assert(method.GetParameters().Length == 2);

                Debug.Assert(method.GetParameters()[0].ParameterType == typeof(int));
                Debug.Assert(method.GetParameters()[0].Name == "offset" || method.GetParameters()[0].Name == "snesOffset");

                Debug.Assert(method.GetParameters()[1].ParameterType == typeof(int));
                Debug.Assert(method.GetParameters()[1].Name == "length");

                Debug.Assert(method.ReturnType == typeof(string));

                _parametersCache.Add(token, (new Tuple<MethodInfo, int>(method, length)));
            }

            Debug.Assert(_parametersCache.Count != 0);
        }

        protected string GetParameter(int offset, string parameter, int length)
        {
            if (!Parameters.TryGetValue(parameter, out var methodAndLength))
            {
                throw new InvalidDataException($"Unknown parameter: {parameter}");
            }

            var methodInfo = methodAndLength.Item1;
            var callParams = new object[] { offset, length };

            var returnValue = methodInfo.Invoke(this, callParams);

            Debug.Assert(returnValue is string);
            return returnValue as string;
        }

        public void AddExtraLabel(int i, Label v)
        {
            Debug.Assert(v != null);
            if (ExtraLabels.ContainsKey(i))
                return;

            v.CleanUp();

            ExtraLabels.Add(i, v);
        }

        // Generate labels like "CODE_856469" and "DATA_763525"
        // These will be combined with the original labels to produce our final assembly
        // These labels exist only for the duration of this export, and then are discarded.
        //
        // TODO: generate some nice looking "+"/"-" labels here.
        protected void GenerateAdditionalExtraLabels()
        {
            if (Settings.Unlabeled == FormatUnlabeled.ShowNone)
                return;

            for (var pointer = 0; pointer < Data.GetRomSize(); pointer += GetLineByteLength(pointer))
            {
                GenerateLabelIfNeededAt(pointer);
            }
        }

        private void GenerateLabelIfNeededAt(int pcoffset)
        {
            var snes = GetAddressOfAnyUsefulLabelsAt(pcoffset);
            if (snes == -1) 
                return;

            var labelName = Data.GetDefaultLabel(snes);
            AddExtraLabel(snes, new Label() {
                Name = labelName,
            });
        }

        protected int GetAddressOfAnyUsefulLabelsAt(int pcoffset)
        {
            if (Settings.Unlabeled == FormatUnlabeled.ShowAll) 
                return Data.ConvertPCtoSnes(pcoffset); // this may not be right either...

            var flag = Data.GetFlag(pcoffset);
            var usefulToCreateLabelFrom =
                flag == FlagType.Opcode || flag == FlagType.Pointer16Bit ||
                flag == FlagType.Pointer24Bit || flag == FlagType.Pointer32Bit;

            if (!usefulToCreateLabelFrom)
                return -1;

            var snesIa = Data.GetIntermediateAddressOrPointer(pcoffset);
            var pc = Data.ConvertSnesToPc(snesIa);
            return pc >= 0 ? snesIa : -1;
        }

        public static bool ValidateFormat(string formatString)
        {
            var tokens = formatString.ToLower().Split('%');

            // not valid if format has an odd amount of %s
            if (tokens.Length % 2 == 0) return false;

            for (int i = 1; i < tokens.Length; i += 2)
            {
                int indexOfColon = tokens[i].IndexOf(':');
                string kind = indexOfColon >= 0 ? tokens[i].Substring(0, indexOfColon) : tokens[i];

                // not valid if base token isn't one we know of
                if (!Parameters.ContainsKey(kind))
                    return false;

                // not valid if parameter isn't an integer
                if (indexOfColon >= 0 && !int.TryParse(tokens[i].Substring(indexOfColon + 1), out _)) 
                    return false;
            }

            return true;
        }
    }
}