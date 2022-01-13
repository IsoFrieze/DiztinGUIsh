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
    IProject ImportWithDefaultSettings(string romFilename);
    IProject Import(IRomImportSettings importSettings);
}

public class ProjectImporter : IProjectImporter
{
    private readonly IProjectImportSettingsFactory createProjectSettings;
    private readonly Func<IRomImportSettings, IProjectFactoryFromRomImportSettings> createProjectImporterFromSettings;
    
    public ProjectImporter(
        Func<IRomImportSettings, IProjectFactoryFromRomImportSettings> createProjectImporterFromSettings, 
        IProjectImportSettingsFactory createProjectSettings)
    {
        this.createProjectImporterFromSettings = createProjectImporterFromSettings;
        this.createProjectSettings = createProjectSettings;
    }
    
    public IProject ImportWithDefaultSettings(string romFilename)
    {
        var importSettings = createProjectSettings.Create(romFilename);
        return Import(importSettings);
    }

    public IProject Import(IRomImportSettings importSettings)
    {
        var importer = CreateProjectImporterFromSettings(importSettings);
        return importer?.Read();
    }

    private IProjectFactoryFromRomImportSettings CreateProjectImporterFromSettings(IRomImportSettings importSettings) => 
        createProjectImporterFromSettings(importSettings);
}

