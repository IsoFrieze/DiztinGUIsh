using ExtendedXmlSerializer.Configuration;

namespace Diz.Core.serialization.xml_serializer;

public interface IXmlSerializerFactory
{
    public IConfigurationContainer GetSerializer();
}