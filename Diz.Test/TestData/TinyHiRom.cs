using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;

namespace Diz.Test.TestData
{
    public static class TinyHiRomCreator
    {
        public static List<ByteEntry> CreateByteOffsetData() =>
            new()
            {
                // starts at PC=0, which is SNES=0xC00000

                // STA.W SNES_VMADDL
                // OR (equivalent)
                // STA.W $2116
                
                new ByteEntry(new AnnotationCollection {
                    new OpcodeAnnotation {MFlag = true, XFlag = true, DataBank = 0x00, DirectPage = 0},
                    new MarkAnnotation {TypeFlag = FlagType.Opcode}
                })
                {
                    Byte = 0x8D
                },
                new(new AnnotationCollection {
                    new MarkAnnotation {TypeFlag = FlagType.Operand},
                    new Comment {Text = "unused"} // 0xC00001
                })
                {
                    Byte = 0x16
                },
                
                // sidenote: demonstrates another way to create Byte's, identical to above
                new(new AnnotationCollection {
                    new MarkAnnotation {TypeFlag = FlagType.Operand},
                    new ByteAnnotation {Byte = 0x21}
                })
            };

        public static Data CreateSampleRomByteSource(IReadOnlyCollection<ByteEntry> srcData)
        {
            var romByteSource = new ByteSource
            {
                Name = "Snes Rom",
                Bytes = new StorageList<ByteEntry>(srcData)
            };

            var data = new Data();
            data.PopulateFromRom(romByteSource,RomMapMode.HiRom, RomSpeed.FastRom);
            return data;
        }

        public static (List<ByteEntry>, Data) CreateSampleRomByteSourceElements()
        {
            var byteOffsetData = CreateByteOffsetData();
            return (byteOffsetData, CreateSampleRomByteSource(byteOffsetData));
        }
        
        public static Data CreateBaseRom()
        {
            var (_, newData) = CreateSampleRomByteSourceElements();
            return newData;
        }
    }
    
    public static class TinyHiRomSample
    {
        public static Data TinyHiRom => TinyHiRomCreator.CreateBaseRom();

        public static Data TinyHiRomWithExtraLabel
        {
            get
            {
                var data = TinyHiRomCreator.CreateBaseRom();

                data.Labels.AddLabel(
                    0x002116, new Label {Name = "SNES_VMADDL", Comment = "SNES hardware register example."}
                );

                return data;
            }
        }
    }
}