using Diz.Core;
using Diz.Core.model;
using Diz.Core.util;
using Diz.LogWriter;
using Diz.LogWriter.util;

namespace Diz.PowerShell;

public class ProjectFileAssemblyExporter : IProjectFileAssemblyExporter
{
    private readonly IDizLogger logger;
    private readonly IFilesystemService fs;
    private readonly IProjectFileOpener projectFileSource;

    public ProjectFileAssemblyExporter(IDizLogger logger, IProjectFileOpener projectFileSource, IFilesystemService fs)
    {
        this.logger = logger;
        this.projectFileSource = projectFileSource;
        this.fs = fs;
    }
    
    private Project? OpenProjectFile(string projectFileName)
    {
        var project = projectFileSource.ReadProjectFromFile(projectFileName);
        if (project == null)
            return null;

        logger.Debug($"Loaded project, rom is: {project.AttachedRomFilename}");
        return project;
    }

    public bool ExportAssembly(string projectFileName)
    {
        var project = OpenProjectFile(projectFileName);
        return project != null && ExportAssembly(project);
    }

    public bool ExportAssembly(Project project)
    {
        var failReason = project.LogWriterSettings.Validate(fs);
        if (failReason != null)
        {
            logger.Error($"invalid assembly build settings {failReason}");
            return false;
        }

        var lc = new LogCreator
        {
            Settings = project.LogWriterSettings,
            Data = new LogCreatorByteSource(project.Data),
        };

        logger.Debug("Building....");
        var result = lc.CreateLog();

        if (!result.Success)
        {
            logger.Error($"Failed to build, error was: {result.OutputStr}");
            return false;
        }

        logger.Info("Successfully exported assembly output.");
        return true;
    }
}