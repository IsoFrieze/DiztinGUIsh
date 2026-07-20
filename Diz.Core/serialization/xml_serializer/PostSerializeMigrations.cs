using System.Collections.Generic;
using System.Xml.Linq;

namespace Diz.Core.serialization.xml_serializer
{
    public interface IMigrationEvents
    {
        // add migrations to hook in various places in the code as needed.
        // example: something to pre-process incoming XML text, or modify the XML deserializer before it's used

        /// <summary>
        /// Rewrite the raw XML document after it's parsed but before the real deserializer sees it.
        /// This is the only hook that can fix up things the deserializer itself can't survive, such as
        /// element/attribute renames: the deserializer silently ignores XML names it doesn't recognize,
        /// so an un-rewritten old name loads as a default value instead of an error.
        /// Modify the document in place.
        /// </summary>
        void OnLoadingPreProcessXml(XDocument document) { }

        void OnLoadingBeforeAddLinkedRom(IAddRomDataCommand romAddCmd) { }
        void OnLoadingAfterAddLinkedRom(IAddRomDataCommand romAddCmd) { }
    }
    
    public interface IMigration : IMigrationEvents
    {
        // Each Migration has a unique version#, and will upgrade data in that version#
        // to the next version#.
        public int AppliesToSaveVersion { get; }
    }

    public interface IMigrationRunner : IMigrationEvents
    {
        public IReadOnlyList<IMigration> Migrations { get; }
        int StartingSaveVersion { get; set; }
        int TargetSaveVersion { get; set; }
    }
}