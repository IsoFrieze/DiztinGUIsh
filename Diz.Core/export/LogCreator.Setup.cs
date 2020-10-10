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
            public string token;
            public int length;
        }

        public class OutputResult
        {
            public bool success;
            public int error_count = -1;
            public LogCreator logCreator;
            public string outputStr = ""; // only set if outputString=true
        }


        // DONT use directly [except to setup the caching]
        protected static Dictionary<string, Tuple<MethodInfo, int>> parameters;

        // SAFE to use directly.
        protected static Dictionary<string, Tuple<MethodInfo, int>> Parameters
        {
            get
            {
                CacheAssemblerAttributeInfo();
                return parameters;
            }
        }

        protected static void CacheAssemblerAttributeInfo()
        {
            if (parameters != null)
                return;

            parameters = new Dictionary<string, Tuple<MethodInfo, int>>();

            var methodsWithAttributes = typeof(LogCreator)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(
                    x => x.GetCustomAttributes(typeof(AssemblerHandler), false).FirstOrDefault() != null
                );

            foreach (var method in methodsWithAttributes)
            {
                var assemblerHandler = method.GetCustomAttribute<AssemblerHandler>();
                var token = assemblerHandler.token;
                var length = assemblerHandler.length;

                // check your method signature if you hit this stuff.
                Debug.Assert(method.GetParameters().Length == 2);

                Debug.Assert(method.GetParameters()[0].ParameterType == typeof(int));
                Debug.Assert(method.GetParameters()[0].Name == "offset" || method.GetParameters()[0].Name == "snesOffset");

                Debug.Assert(method.GetParameters()[1].ParameterType == typeof(int));
                Debug.Assert(method.GetParameters()[1].Name == "length");

                Debug.Assert(method.ReturnType == typeof(string));

                parameters.Add(token, (new Tuple<MethodInfo, int>(method, length)));
            }

            Debug.Assert(parameters.Count != 0);
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
            var pointer = 0;

            while (pointer < Data.GetROMSize())
            {
                var offset = GetAddressOfAnyUsefulLabelsAt(pointer, out var length);
                pointer += length;

                if (offset == -1)
                    continue;

                AddExtraLabel(offset, new Label()
                {
                    name = Data.GetDefaultLabel(pointer)
                });
            }
        }

        protected int GetAddressOfAnyUsefulLabelsAt(int pcoffset, out int length)
        {
            length = GetLineByteLength(pcoffset);
            switch (Settings.unlabeled)
            {
                case FormatUnlabeled.ShowNone:
                    return -1;
                case FormatUnlabeled.ShowAll:
                    return Data.ConvertPCtoSNES(pcoffset);
            }

            var flag = Data.GetFlag(pcoffset);
            var usefulToCreateLabelFrom =
                flag == Data.FlagType.Opcode || flag == Data.FlagType.Pointer16Bit ||
                flag == Data.FlagType.Pointer24Bit || flag == Data.FlagType.Pointer32Bit;

            if (!usefulToCreateLabelFrom)
                return -1;

            var ia = Data.GetIntermediateAddressOrPointer(pcoffset);
            if (ia >= 0 && Data.ConvertSNEStoPC(ia) >= 0)
                return ia;

            return -1;
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
                int oof;
                if (indexOfColon >= 0 && !int.TryParse(tokens[i].Substring(indexOfColon + 1), out oof)) 
                    return false;
            }

            return true;
        }
    }
}