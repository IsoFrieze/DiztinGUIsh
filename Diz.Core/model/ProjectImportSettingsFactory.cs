using Diz.Core.serialization;

namespace Diz.Core.model;

public interface IProjectImportSettingsFactory
{
    IRomImportSettings Create(string? romFilename = null);
}

// ReSharper disable once ClassNeverInstantiated.Global
public class ProjectImportSettingsFactory : IProjectImportSettingsFactory
{
    public IRomImportSettings Create(string? romFilename = null) =>
        new ImportRomSettings
        {
            RomFilename = romFilename
        };
}