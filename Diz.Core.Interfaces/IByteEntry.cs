using System.Xml.Serialization;
using Diz.Core.model;

namespace Diz.Core.Interfaces;

public interface IByteEntry
{
    int ParentIndex { get; }

    // if null, it means caller either needs to dig one level deeper in
    // parent container to find the byte value, or, there is no data
    [XmlIgnore] public byte? Byte { get; set; }

    [XmlIgnore] public byte DataBank { get; set; }

    [XmlIgnore] public int DirectPage { get; set; }

    [XmlIgnore] public bool XFlag { get; set; }

    [XmlIgnore] public bool MFlag { get; set; }

    [XmlIgnore] public Architecture Arch { get; set; }

    [XmlIgnore] FlagType TypeFlag { get; set; }

    [XmlIgnore] InOutPoint Point { get; set; }
}