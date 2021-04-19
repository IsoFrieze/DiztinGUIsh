using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using Diz.Core.export;
using Diz.Core.serialization;

namespace Diz.PowerShell
{

    [Cmdlet(VerbsLifecycle.Build, "AssemblyFiles")]
    public class BuildAssemblyFilesCmdlet : PSCmdlet
    {
        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty]
        public string[] ProjectNames { get; set; }

        protected override void BeginProcessing()
        {
            
        }

        protected override void ProcessRecord()
        {
            if (ProjectNames == null)
                return;

            if (ProjectNames.Length <= 0)
                return;

            foreach (var projectName in ProjectNames)
            {
                BuildAssembly(projectName);
            }
        }

        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
        private bool BuildAssembly(string projectFileName)
        {
            var (project, warning) = new ProjectFileManager().Open(projectFileName);
            if (project == null)
                return false;

            if (!string.IsNullOrEmpty(warning))
            {
                WriteObject($"ERROR: {warning}");
                return false;
            }

            WriteDebug($"Loaded project, rom is: {project.AttachedRomFilename}");

            var failReason = project.LogWriterSettings.Validate();
            if (failReason != null)
            {
                WriteObject($"ERROR: invalid assembly build settings {failReason}");
                return false;
            }

            var lc = new LogCreator()
            {
                Settings = project.LogWriterSettings,
                Data = project.Data,
            };

            WriteCommandDetail("Building....");
            var result = lc.CreateLog();

            if (!result.Success)
            {
                WriteObject($"Failed to build, error was: {result.OutputStr}");
            }

            return true;
        }

        protected override void EndProcessing()
        {

        }

        protected override void StopProcessing()
        {
            
        }
    }
}