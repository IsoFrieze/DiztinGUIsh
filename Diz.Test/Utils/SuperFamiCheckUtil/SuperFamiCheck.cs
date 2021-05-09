using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Diz.Test.Utils.SuperFamiCheckUtil
{
    public class SuperFamiCheckTool : ExternalToolRunner
    {
        public const string Exe = @"D:\projects\DiztinGUIsh-main\Diz.Test\external-tools\superfamicheck.exe";

        public SuperFamiCheckTool() : base(Exe) {}

        public struct Result
        {
            public uint Checksum { get; set; }
            public uint Complement { get; set; }
            public uint AllCheckBytes => (Checksum << 16) | Complement;
        }
        
        public static Result Run(string romName) => new SuperFamiCheckTool().RunInternal(romName);

        private Result RunInternal(string romName)
        {
            var output = RunAndGetOutput(romName);
            var checksumLine = output.Find(x => x.Contains("Checksum"));
            var complementLine = output.Find(x => x.Contains("Complement"));

            var (checksumTxt, checksum) = DizSuperFamiCheckParse.ParseKvpLine(checksumLine);
            var (complementTxt, complement) = DizSuperFamiCheckParse.ParseKvpLine(complementLine);

            if (checksumTxt != "Checksum" || complementTxt != "Complement")
                throw new InvalidDataException("Couldn't parse output from SuperFamiCheck");

            return new Result
            {
                Checksum = checksum,
                Complement = complement
            };
        }

        private List<string> RunAndGetOutput(string romName)
        {
            var escapedFilename = $"\"{romName}\"";
            return RunCommand(escapedFilename).Split('\n').ToList();
        }
    }

    public class FactIfFamicheckPresent : FactIfFilePresent { public FactIfFamicheckPresent() => ExePath = SuperFamiCheckTool.Exe; }
}