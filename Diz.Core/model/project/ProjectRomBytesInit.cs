#nullable enable

using System;
using System.IO;
using Diz.Core.model.snes;
using Diz.Core.serialization.xml_serializer;

namespace Diz.Core.model.project
{
    /// <summary>
    /// After a project file is deserialized from XML, it contains all its info EXCEPT
    /// we don't store the bytes of the actual ROM file in the project file (for copyright and redundancy reasons).
    /// So, on post-serialization we need to add the bytes from the ROM file on disk into our newly deserialized Project class
    /// We also need to verify that we have the right file by running checks (like rom title and checksum) that we
    /// indeed have the right file on disk/etc.
    ///
    /// This class is designed to open a ROM file on disk and safely copy it into a Project file, ensuring data integrity.
    /// </summary>
    public class AddRomDataCommand
    {
        public bool ShouldProjectCartTitleMatchRomBytes { get; set; } = true;
        public ProjectXmlSerializer.Root Root { get; }
        public Project Project => Root.Project;
        public Func<string, string>? GetNextRomFileToTry { get; set; }
        public MigrationRunner? MigrationRunner { get; set; }
        

        public AddRomDataCommand(ProjectXmlSerializer.Root root)
        {
            Root = root;
            
            if (root.Project == null)
                throw new InvalidDataException("Root element should contain a Project element, but none was found.");
        }
        
        public void TryReadAttachedProjectRom()
        {
            MigrationRunner?.OnLoadingBeforeAddLinkedRom(this);
            Populate();
            MigrationRunner?.OnLoadingAfterAddLinkedRom(this);
        }

        private void Populate()
        {
            if (FillIfOverride()) // unusual non-normal override case 
                return;

            FillIfSearchFoundRom(); // normal case
        }

        private bool FillIfOverride()
        {
            // TODO: move this to an override that overrides Populate()
            // then, don't call the base method, fail if this doesn't work.
            
            var bytes = Project.Data.GetOverriddenRomBytes();
            if (bytes == null)
                return false;
            
            EnsureProjectCompatibleWithRom(bytes);
            Project.Data.PopulateFrom(bytes);
            return true;
        }

        private void FillIfSearchFoundRom()
        {
            var result = SearchForValidRom();
            if (result == null)
                throw new InvalidOperationException("Search failed, couldn't find compatible ROM to link");

            Project.AttachedRomFilename = result.Value.filename;
            Project.Data.PopulateFrom(result.Value.romBytes);
        }

        private static ILinkedRomBytesProvider GetLinkedRomProvider() => new LinkedRomBytesFileSearchProvider();

        private (string filename, byte[] romBytes)? SearchForValidRom()
        {
            var searchProvider = GetLinkedRomProvider();
            searchProvider.EnsureCompatible = (romFilename, romBytes) => EnsureProjectCompatibleWithRom(romBytes);
            searchProvider.GetNextFilename = reasonWhyLastFileNotCompatible => 
                GetNextRomFileToTry?.Invoke(reasonWhyLastFileNotCompatible) ?? null;

            return searchProvider.SearchAndReadFromCompatibleRom(initialRomFile: Project.AttachedRomFilename);
        }
        
        private void EnsureProjectCompatibleWithRom(byte[] romFileBytes)
        {
            var validator = new AddRomDataCommandValidator(ShouldProjectCartTitleMatchRomBytes);
            var container = new RomToProjectAssociation
            {
                Project = Project,
                RomBytes = romFileBytes,
            };
            var results = validator.Validate(container);
            if (!results.IsValid)
                throw new InvalidDataException(results.ToString());
        }
    }
}