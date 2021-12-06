using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Diz.Test.Utils
{
    public class FactOnlyIfFilePresent : FactAttribute
    {
        public FactOnlyIfFilePresent(string[] files = null)
        {
            var toCheck = new List<string>();
            if (files != null)
                toCheck.AddRange(files);

            CheckExists(toCheck);
        }

        private void CheckExists(List<string> toCheck)
        {
            var missingFile = toCheck.Find(f => !File.Exists(f));
            if (missingFile != null)
                Skip = $"Can't find test prerequisite file {missingFile}";
        }
    }
}