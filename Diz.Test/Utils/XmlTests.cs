using System;
using System.Xml;
using Diz.Core.model.byteSources;
using Diz.Core.serialization.xml_serializer;
using Diz.Test.tests;
using ExtendedXmlSerializer;

namespace Diz.Test.Utils
{
    public static class XmlTestUtils
    {
        public static void RunFullCycle<T>(Func<T> createFn, out T expectedCopy, out T deserializedObj)
        {
            RunFullCycleObj(() => createFn(), out var expectedObjCopy, out var deserializedObjCopy);

            expectedCopy = (T)expectedObjCopy;
            deserializedObj = (T) deserializedObjCopy;
        }

        public static void RunFullCycleObj(Func<object> createFn, out object expectedCopy, out object deserializedObj)
        {
            var objToCycle = createFn();
            expectedCopy = createFn();
            
            deserializedObj = XmlFullCycle(objToCycle);
        }


        public static T XmlFullCycle<T>(T objToCycle)
        {
            // var xmlToCycle = XmlSerializationSupportNew.Serialize(objToCycle);

            var serializer = XmlSerializationSupportNew.GetConfig()
                .Type<StorageList<ByteEntry>>()
                .WithInterceptor(new XmlSaveTestsIndividual.ByteListInterceptor())
                // .Type<StorageList<ByteEntryTest>>()
                // .Member(x=>x.Parent).Ignore()
                //
                // .Type<ByteEntryTest>()
                //
                // // .Member(x => x.Arch).Ignore()
                // // .Member(x => x.Byte).Ignore()
                // // .Member(x => x.Point).Ignore()
                // // .Member(x => x.DataBank).Ignore()
                // // .Member(x => x.DirectPage).Ignore()
                // // .Member(x => x.MFlag).Ignore()
                // // .Member(x => x.XFlag).Ignore()
                // // .Member(x => x.TypeFlag).Ignore()
                // //.Member(x => x.DontSetParentOnCollectionItems).Ignore()
                //
                // .EnableReferences()

                .Create();

            var xmlToCycle = serializer.Serialize(
                new XmlWriterSettings {OmitXmlDeclaration = false, Indent = true, NewLineChars = "\r\n"},
                objToCycle);

            var deserialized = serializer.Deserialize<T>(xmlToCycle);

            return deserialized;
        }
    }
}