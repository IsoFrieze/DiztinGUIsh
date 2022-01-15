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

public interface IProjectImportDefaultSettingsFactory : IProjectImportSettingsFactory
{
    // we can replace this interface with a named service 
}

public class ProjectImporter : IProjectImporter
{
    private readonly IProjectImportDefaultSettingsFactory generateDefaultSettingsFromRom;
    private readonly Func<IRomImportSettings, IProjectFactoryFromRomImportSettings> createImporterFromSettings;

    public ProjectImporter(
        IProjectImportDefaultSettingsFactory generateDefaultSettingsFromRom, 
        Func<IRomImportSettings, IProjectFactoryFromRomImportSettings> createImporterFromSettings)
    {
        this.generateDefaultSettingsFromRom = generateDefaultSettingsFromRom;
        this.createImporterFromSettings = createImporterFromSettings;
    }
    
    public IProject CreateProjectFromDefaultSettings(string romFilename)
    {
        var settings = generateDefaultSettingsFromRom.Create(romFilename);
        return CreateProjectFromSettings(settings);
    }

    public IProject CreateProjectFromSettings(IRomImportSettings importSettings)
    {
        return createImporterFromSettings(importSettings)?.Read();
    }
}

