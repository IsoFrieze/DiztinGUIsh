using System.Diagnostics;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.serialization;

namespace Diz.Cpu._65816.import;

// TODO: hook this up

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
        
        Debug.Assert(project?.Data != null);

        project.AttachedRomFilename = importSettings.RomFilename;
        project.Session = new ProjectSession(project, "")
        {
            UnsavedChanges = true
        };

        var snesApi = project.Data.GetSnesApi();
        Debug.Assert(snesApi != null);

#if DIZ_3_BRANCH
        // new way, though TODO we want to decouple the SNES stuff from here
        project.Data.PopulateFrom(importSettings.RomBytes, importSettings.RomMapMode, importSettings.RomSpeed);
#else
        // old way
        snesApi.RomMapMode = importSettings.RomMapMode;
        snesApi.RomSpeed = importSettings.RomSpeed;
        project.Data.RomBytes.CreateRomBytesFromRom(importSettings.RomBytes);
#endif

        foreach (var (romOffset, label) in importSettings.InitialLabels)
        {
            var snesAddress = snesApi.ConvertPCtoSnes(romOffset);
            project.Data.Labels.AddLabel(snesAddress, label, true);
        }

        foreach (var (offset, flagType) in importSettings.InitialHeaderFlags)
            snesApi.SetFlag(offset, flagType);

        snesApi.CacheVerificationInfoFor(project);

        return project;
    }
}