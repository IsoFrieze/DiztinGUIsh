using System.Collections.Generic;
using System.Diagnostics;
using Diz.Core.model.byteSources;
using Diz.Core.util;

namespace Diz.Core.model.snes
{
    public static class DataInitializer
    {
        public static Data PopulateFrom(this Data data, IReadOnlyCollection<byte> actualRomBytes, RomMapMode romMapMode, RomSpeed romSpeed)
        {
            var mapping = RomUtil.CreateRomMappingFromRomRawBytes(actualRomBytes, romMapMode, romSpeed);
            return PopulateFrom(data, mapping);
        }

        public static Data PopulateFromRom(this Data data, ByteSource romByteSource, RomMapMode romMapMode, RomSpeed romSpeed)
        {
            var mapping = RomUtil.CreateRomMappingFromRomByteSource(romByteSource, romMapMode, romSpeed);
            return PopulateFrom(data, mapping);
        }

        public static Data PopulateFrom(this Data data, ByteSourceMapping romByteSourceMapping)
        {
            // var previousNotificationState = SendNotificationChangedEvents;
            // data.SendNotificationChangedEvents = false;

            // setup a common SNES mapping, just the ROM and nothing else.
            // this is very configurable, for now, this class is sticking with the simple setup.
            // you can get as elaborate as you want, with RAM, patches, overrides, etc.
            data.SnesAddressSpace.ChildSources.Add(romByteSourceMapping);

            //data.SendNotificationChangedEvents = previousNotificationState;
            return data;
        }

        // precondition, everything else has already been setup but adding in the actual bytes,
        // and is ready for actual rom byte data now
        public static Data PopulateFrom(this Data data, IReadOnlyCollection<byte> actualRomBytes)
        {
            // this method is basically a shortcut which only works under some very specific constraints
            Debug.Assert(data.SnesAddressSpace != null);
            Debug.Assert(data.SnesAddressSpace.ChildSources.Count == 1);
            Debug.Assert(data.SnesAddressSpace.ChildSources[0].RegionMapping.GetType() == typeof(RegionMappingSnesRom));
            Debug.Assert(ReferenceEquals(data.RomByteSourceMapping, data.SnesAddressSpace.ChildSources[0]));
            Debug.Assert(data.RomMapping != null);
            Debug.Assert(data.RomByteSourceMapping?.ByteSource != null);
            Debug.Assert(actualRomBytes.Count == data.RomByteSource.Bytes.Count);

            var i = 0;
            foreach (var b in actualRomBytes)
            {
                data.RomByteSource.Bytes[i].Byte = b;
                ++i;
            }
            return data;
        }
        
        public static Data InitializeEmptyRomMapping(this Data data, int size, RomMapMode mode, RomSpeed speed)
        {
            var romByteSource = new ByteSource
            {
                Bytes = new StorageList<ByteEntry>(size),
                Name = "Snes ROM"
            };
            PopulateFromRom(data, romByteSource, mode, speed);
            return data;
        }
    }
}