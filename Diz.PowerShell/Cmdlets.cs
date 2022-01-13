#nullable enable

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using Diz.Core;
using JetBrains.Annotations;
using LightInject;

namespace Diz.PowerShell;

[UsedImplicitly]
[Cmdlet(VerbsLifecycle.Build, "AssemblyFiles")]
public class BuildAssemblyFilesCmdlet : ServiceContainerCmdletBase
{
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    public string[]? ProjectNames { get; set; } = null;
    
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
        // this ONE TIME, this service locator anti-pattern is OK because we ARE the top-level class.
        var projectFileAssemblyExporter = ServiceContainer.GetInstance<IProjectFileAssemblyExporter>();
        Debug.Assert(projectFileAssemblyExporter != null);
        return projectFileAssemblyExporter.ExportAssembly(projectFileName);
    }

    protected override void StopProcessing() {}
    protected override void EndProcessing() {}
}