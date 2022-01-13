using System.Diagnostics;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.serialization;

namespace Diz.Cpu._65816.import;

// TODO: not sure if we still need this?
// public class SnesDefaultSettingsFactory : IProjectImportSettingsFactory
// {
//     private readonly ISnesRomAnalyzer snesRomAnalyzer;
//     
//     public ImportRomSettings GetDefaultSettingsFor(string romFilename)
//     {
//         // automated headless helper method to use all default settings and pray it works
//         // no GUI or anything. use with caution, only if you know what you're doing
//         var importRomSettingsBuilder = new SnesRomAnalyzer();
//         importRomSettingsBuilder.AnalyzeRomFile(romFilename);
//         return importRomSettingsBuilder.GenerateSettings();
//     }
// }

public class SnesProjectFactoryFromRomImportSettings : IProjectFactoryFromRomImportSettings
{
    private readonly IRomImportSettings importSettings;
    private readonly IProjectFactory baseProjectFactory; 

    public SnesProjectFactoryFromRomImportSettings(IProjectFactory baseProjectFactory, IRomImportSettings importSettings)
    {
        this.importSettings = importSettings;
        this.baseProjectFactory = baseProjectFactory;
    }

    public Project Read()
    {
        var project = baseProjectFactory.Create()
            as Project; // TODO: after more refactoring, remove cast and use IProject directly 
        
        Debug.Assert(project != null);

        project.AttachedRomFilename = importSettings.RomFilename;
        project.Session = new ProjectSession(project, "")
        {
            UnsavedChanges = true
        };

        var snesData = new SnesApi(project.Data);
        project.Data.Apis.Add(snesData);

#if DIZ_3_BRANCH
        // new way, though TODO we want to decouple the SNES stuff from here
        project.Data.PopulateFrom(importSettings.RomBytes, importSettings.RomMapMode, importSettings.RomSpeed);
#else
        // old way
        snesData.RomMapMode = importSettings.RomMapMode;
        snesData.RomSpeed = importSettings.RomSpeed;
        project.Data.RomBytes.CreateRomBytesFromRom(importSettings.RomBytes);
#endif

        foreach (var (romOffset, label) in importSettings.InitialLabels)
        {
            var snesAddress = snesData.ConvertPCtoSnes(romOffset);
            project.Data.Labels.AddLabel(snesAddress, label, true);
        }

        foreach (var (offset, flagType) in importSettings.InitialHeaderFlags)
            snesData.SetFlag(offset, flagType);

        snesData.CacheVerificationInfoFor(project);

        return project;
    }
}