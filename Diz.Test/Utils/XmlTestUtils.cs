using System;
using Diz.Core.serialization.xml_serializer;
using ExtendedXmlSerializer;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test.Utils
{
    public class XmlTestUtils
    {
        public ITestOutputHelper Output { get; set; }
        
        public void RunFullCycle<T>(Func<T> createFn, out T expectedCopy, out T deserializedObj)
        {
            RunFullCycleObj(() => createFn(), out var expectedObjCopy, out var deserializedObjCopy);

            expectedCopy = (T)expectedObjCopy;
            deserializedObj = (T) deserializedObjCopy;
        }

        public void RunFullCycleObj(Func<object> createFn, out object expectedCopy, out object deserializedObj)
        {
            var objToCycle = createFn();
            expectedCopy = createFn();
            
            deserializedObj = XmlFullCycle(objToCycle);
        }
        
        public T XmlFullCycle<T>(T objToCycle)
        {
            var xmlToCycle = XmlSerializerSupport.GetSerializer().Create().Serialize(objToCycle);
            Output?.WriteLine(xmlToCycle);
            var deserialized = XmlSerializerSupport.GetSerializer().Create().Deserialize<T>(xmlToCycle);
            return deserialized;
        }
        
        public void RunFullCycle(Func<object> createFn)
        {
            RunFullCycle(createFn, out var unchanged, out var cycled);
            Assert.Equal(unchanged, cycled);
        }
    }
}