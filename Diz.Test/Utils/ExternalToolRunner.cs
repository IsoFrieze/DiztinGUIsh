using System;
using Diz.Core.util;

namespace Diz.Test.Utils.SuperFamiCheckUtil
{
    public class ExternalToolRunner
    {
        private string exeFilename;
        public ExternalToolRunner(string exeFilename)
        {
            this.exeFilename = exeFilename;
        }

        // some tools may need this false
        public bool ThrowIfAnythingOnStderr { get; set; } = true;

        public string RunCommand(string args)
        {
            var (stdout, stderr) = Util.RunCommandGetOutput(exeFilename, args);

            if (ThrowIfAnythingOnStderr && !string.IsNullOrEmpty(stderr))
                throw new InvalidOperationException($"Tool run failed, stderr present: {stderr}");

            return stdout;
        }
    }
}