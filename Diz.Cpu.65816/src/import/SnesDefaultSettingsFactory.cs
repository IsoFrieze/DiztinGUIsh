using Diz.Core.model;
using Diz.Core.serialization;
using JetBrains.Annotations;

namespace Diz.Cpu._65816.import;

[UsedImplicitly]
public class SnesDefaultSettingsFactory : IProjectImportDefaultSettingsFactory
{
    private readonly ISnesRomImportSettingsBuilder snesRomImportSettingsBuilder;

    public SnesDefaultSettingsFactory(ISnesRomImportSettingsBuilder snesRomImportSettingsBuilder)
    {
        this.snesRomImportSettingsBuilder = snesRomImportSettingsBuilder;
    }

    public IRomImportSettings Create(string romFilename = null)
    {
        // automated headless helper method to use all default settings and pray it works
        // no GUI or anything. use with caution, only if you know what you're doing
        
        snesRomImportSettingsBuilder.Analyze(romFilename);
        return snesRomImportSettingsBuilder.GenerateSettings(); // plug and pray....
    }
}