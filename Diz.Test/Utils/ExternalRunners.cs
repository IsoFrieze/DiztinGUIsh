using System.IO;
using Xunit;

namespace Diz.Test.Utils
{
    public abstract class FactIfFilePresent : FactAttribute
    {
        private string exePath;

        public string ExePath
        {
            get => exePath;
            set
            {
                if (!File.Exists(value))
                    Skip = $"Can't find tool {exePath}";
                else
                    exePath = value;
            }
        }
    }
}