using System;
using System.Collections.Generic;

namespace Diz.Core.export
{
    public class LogCreatorLineFormatter
    {
        public readonly List<Tuple<string, int>> ParsedLineFormat;

        public LogCreatorLineFormatter(string lineFormatStr, IReadOnlyDictionary<string, LogCreator.AssemblyPartialLineGenerator> generators)
        {
            ParsedLineFormat = ParseLineFormatStr(lineFormatStr, generators);
        }
        
        
        // every line printed in a .asm file is done so by variable substitution according to a format string.
        //
        // example:
        // in a format string like this:
        // "%label:-22% %code:37%;%pc%|%bytes%|%ia%; %comment%";
        //
        // you might get output like this:
        //
        // CODE_808000: LDA.W Test_Data,X                    ;808000|BD5B80  |80805B;
        //
        // GetParameter() takes a ROM offset and the name of a "parameter" i.e. one of the labels in that format string
        // like "label", "code", "pc", "bytes", etc.  There are also special params that start with a % sign, like
        // "%empty", "%map", "%bankcross" etc.
        //
        // 
        // It will look for a function in LogCreator tagged with an AssemblerHandler attribute that matches the 
        // parameter passed in.
        private static List<Tuple<string, int>> ParseLineFormatStr(string lineFormatStr, IReadOnlyDictionary<string, LogCreator.AssemblyPartialLineGenerator> generators)
        {
            var formatItems = new List<Tuple<string, int>>();
            
            var split = lineFormatStr.Split('%');
            for (var i = 0; i < split.Length; i++)
            {
                string param;
                int length;
                if (i % 2 == 0)
                {
                    param = split[i];
                    length = int.MaxValue;
                }
                else
                {
                    var indexColon = split[i].IndexOf(':');
                    if (indexColon < 0)
                    {
                        // default, length comes from the attribute.
                        // NOTE: this is weird and could use a refactor because later this value will
                        // be passed into AssemblyGenerator code to be compared against itself. works fine
                        // just kind of adds extra confusion.
                        param = split[i];
                        length = generators[param].DefaultLength;
                    }
                    else
                    {
                        // override, length comes from the format string
                        // example: "%label:-22%"
                        param = split[i].Substring(0, indexColon);
                        length = int.Parse(split[i].Substring(indexColon + 1));
                    }
                }

                formatItems.Add(Tuple.Create(param, length));
            }

            return formatItems;
        }
        
        
        // DON'T use directly [except to setup the caching]
        // private static Dictionary<string, Tuple<MethodInfo, int>> _parametersCache;

        // SAFE to use directly.
        /*public static Dictionary<string, Tuple<MethodInfo, int>> ParameterMethodsCached
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
                    x => x.GetCustomAttributes(typeof(LogCreator.AssemblerHandler), false).FirstOrDefault() != null
                );

            foreach (var method in methodsWithAttributes)
            {
                var assemblerHandler = method.GetCustomAttribute<LogCreator.AssemblerHandler>();
                var token = assemblerHandler.Token;
                var length = assemblerHandler.Length;

                // check your method signature if you hit this stuff.
                Debug.Assert(method.GetParameters().Length == 2);

                Debug.Assert(method.GetParameters()[0].ParameterType == typeof(int));
                Debug.Assert(method.GetParameters()[0].Name == "offset" || method.GetParameters()[0].Name == "snesOffset");

                Debug.Assert(method.GetParameters()[1].ParameterType == typeof(int));
                Debug.Assert(method.GetParameters()[1].Name == "length");

                Debug.Assert(method.ReturnType == typeof(string));

                _parametersCache.Add(token, Tuple.Create(method, length));
            }

            Debug.Assert(_parametersCache.Count != 0);
        }*/

        /*public string GetParameter(int offset, string parameter, int length)
        {
            if (!ParameterMethodsCached.TryGetValue(parameter, out var methodAndLength))
            {
                throw new InvalidDataException($"Unknown parameter: {parameter}");
            }

            var methodInfo = methodAndLength.Item1;
            var callParams = new object[] { offset, length };

            var returnValue = methodInfo.Invoke(this, callParams);

            Debug.Assert(returnValue is string);
            return returnValue as string;
        }*/

        public static bool ValidateFormat(string formatString, IReadOnlyDictionary<string, LogCreator.AssemblyPartialLineGenerator> generators)
        {
            var tokens = formatString.ToLower().Split('%');

            // not valid if format has an odd amount of %s
            if (tokens.Length % 2 == 0) return false;

            for (var i = 1; i < tokens.Length; i += 2)
            {
                var indexOfColon = tokens[i].IndexOf(':');
                var kind = indexOfColon >= 0 ? tokens[i].Substring(0, indexOfColon) : tokens[i];

                // not valid if base token isn't one we know of
                if (!generators.ContainsKey(kind))
                    return false;

                // not valid if parameter isn't an integer
                if (indexOfColon >= 0 && !int.TryParse(tokens[i].Substring(indexOfColon + 1), out _)) 
                    return false;
            }

            return true;
        }
    }
}