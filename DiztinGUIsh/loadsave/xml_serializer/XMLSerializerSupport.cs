using DiztinGUIsh.core;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;

namespace DiztinGUIsh.loadsave.xml_serializer
{
    public static class XmlSerializerSupport
    {
        public static IExtendedXmlSerializer GetSerializer()
        {
            // var sourceD = new Data.ObservableDictionaryAdaptor<int, string>();
            // IDictionary IDict = (IDictionary)sourceD;

            // TODO: doesn't work for saving ObservableDictionary related stuff yet. fix.

            return new ConfigurationContainer()
                .Type<Project>()
                .Member(x => x.UnsavedChanges).Ignore()
                .Type<RomBytes>()
                .Register().Serializer().Using(RomBytesSerializer.Default)
                .Type<Data>()
                //.Member(x => x.Comments).Ignore()
                //.Member(x => x.Labels).Ignore()
                // .Type<ObservableDictionary<int, string>>()
                // .Extend(ObservableDictionaryExtension<int, string>.Default)
                //.EnableImplicitTyping(typeof(Data.ObservableDictionaryAdaptor<int,string>))
                //.Type<Data.ObservableDictionaryAdaptor<int, string>>()
                //.Register().Serializer().ByCalling(dSerialize, dDeserialize)
                //.Type<Data>()
                //.Type<Data.ODIntString>()
                //.WithInterceptor(new InterceptorGuy())
                .UseOptimizedNamespaces()
                .UseAutoFormatting()
                .EnableImplicitTyping(typeof(Data))
                .EnableImplicitTyping(typeof(Label))
                .Create();
        }

        /*private static Data.ObservableDictionaryAdaptor<int, string> dDeserialize(IFormatReader arg)
        {
            throw new NotImplementedException();
        }

        private static void dSerialize(IFormatWriter arg1, Data.ObservableDictionaryAdaptor<int, string> arg2)
        {
            throw new NotImplementedException();
        }*/
    }
}