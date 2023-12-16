using Diz.Core.model;
using ExtendedXmlSerializer.Configuration;

namespace Diz.Core.serialization.xml_serializer;

public interface IXmlSerializerFactory
{
    public IConfigurationContainer GetSerializer(RomBytesOutputFormatSettings romBytesOutputFormat);
}