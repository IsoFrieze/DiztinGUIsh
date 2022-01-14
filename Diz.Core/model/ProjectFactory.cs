using System;
using Diz.Core.serialization;

namespace Diz.Core.model;

public interface IProjectFactory
{
    public IProject Create();
}

public class ProjectFactory : IProjectFactory
{
    private readonly IDataFactory dataFactory;

    public ProjectFactory(IDataFactory dataFactory)
    {
        this.dataFactory = dataFactory;
    }

    public IProject Create() =>
        new Project
        {
            Data = dataFactory.Create()
        };
}

public interface IProjectImporter
{
    /// <summary>
    /// Plug and pray, try and create a new project by importing Rom with the default auto-detected settings,
    /// no opportunity to interactively change them.
    /// Use this function more for things like automated tests etc, try not to use it for normal functionality
    /// in the app (instead, use other classes that go through the process or controllers+gui)
    /// </summary>
    /// <param name="romFilename">Name of a SNES ROM that will be used for this project</param>
    /// <returns>Newly created project</returns>
    IProject CreateProjectFromDefaultSettings(string romFilename);
    
    /// <summary>
    /// Import a ROM file using the given settings.
    /// This is the preferred way to create projects from disk.
    /// Typically you will want to use a GUI or other interactive methods to populate the settings first, then
    /// call this to do the actual "import" which creates the final project and data ready for use. 
    /// </summary>
    /// <param name="importSettings">Name of a SNES ROM that will be used for this project</param>
    /// <returns>Newly created project</returns>
    IProject CreateProjectFromSettings(IRomImportSettings importSettings);
}

public class ProjectImporter : IProjectImporter
{
    // private readonly IProjectImportSettingsFactory createProjectSettings;
    // private readonly Func<IRomImportSettings, IProjectFactoryFromRomImportSettings> createImporterFromSettings;
    //
    // public ProjectImporter(
    //     Func<IRomImportSettings, IProjectFactoryFromRomImportSettings> createImporterFromSettings, 
    //     IProjectImportSettingsFactory createProjectSettings)
    // {
    //     this.createImporterFromSettings = createImporterFromSettings;
    //     this.createProjectSettings = createProjectSettings;
    // }
    
    // note: this function is probably pretty useless without other functionality hooked in to populate other
    // parts of the data.
    // public IProject Create(string romFilename)
    // {
    //     // not helpful var importSettings = createProjectSettings.Create(romFilename);
    //     
    //     return CreateProjectFromSettings(importSettings);
    // }
    
    public IProject CreateProjectFromDefaultSettings(string romFilename)
    {
        throw new NotImplementedException();
        // 1. create default settings from rom filename via SnesDefaultSettingsFactory / IProjectImportSettingsFactory
        // 2. CreateProjectFromSettings
        // var settings = // get IProjectImportSettingsFactory.Create
    }

    public IProject CreateProjectFromSettings(IRomImportSettings importSettings)
    {
        throw new NotImplementedException();
        // TODO return createImporterFromSettings(importSettings)?.Read();
    }
}

