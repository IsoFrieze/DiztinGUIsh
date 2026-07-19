#nullable enable

using System;
using System.Diagnostics;
using System.Management.Automation;
using Diz.Core;
using JetBrains.Annotations;
using LightInject;

namespace Diz.PowerShell;

/// <summary>
/// Build-AssemblyFiles: run the full assembly export on a .diz project file, same as
/// pressing Ctrl+E in the GUI (including asset export, build.ninja generation, and
/// tool vendoring for projects that use region asset export).
///
/// Usage (from the build output dir):
///   Import-Module ./Diz.App.PowerShell.dll
///   Build-AssemblyFiles "C:\path\to\project.dizdir"
/// </summary>
[UsedImplicitly]
[Cmdlet(VerbsLifecycle.Build, "AssemblyFiles")]
public class BuildAssemblyFilesCmdlet : ServiceContainerCmdletBase
{
    [Parameter(Position = 0, Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string[]? ProjectNames { get; set; } = null;

    protected override void ProcessRecord()
    {
        if (ProjectNames == null || ProjectNames.Length == 0)
            return;

        foreach (var projectName in ProjectNames)
        {
            BuildAssembly(GetUnresolvedProviderPathFromPSPath(projectName));
        }
    }

    private bool BuildAssembly(string projectFileName)
    {
        // this ONE TIME, this service locator anti-pattern is OK because we ARE the top-level class.
        var projectFileAssemblyExporter = ServiceContainer.GetInstance<IProjectFileAssemblyExporter>();
        Debug.Assert(projectFileAssemblyExporter != null);

        try
        {
            return projectFileAssemblyExporter.ExportAssembly(projectFileName);
        }
        catch (Exception ex)
        {
            // a corrupt/invalid project file must not take down the whole pipeline --
            // report it as a normal (non-terminating) error and move on.
            WriteError(new ErrorRecord(
                new InvalidOperationException($"Failed to export '{projectFileName}': {ex.Message}", ex),
                "DizProjectExportFailed", ErrorCategory.InvalidData, projectFileName));
            return false;
        }
    }

    protected override void StopProcessing() {}
}
