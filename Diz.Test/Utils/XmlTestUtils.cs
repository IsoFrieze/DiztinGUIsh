using System;
using Diz.Core.serialization.xml_serializer;
using Xunit;

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
            var xmlToCycle = XmlSerializationSupportNew.Serialize(objToCycle);
            var deserialized = XmlSerializationSupportNew.Deserialize<T>(xmlToCycle);
            return deserialized;
        }
        
        public static void RunFullCycle(Func<object> createFn)
        {
            RunFullCycle(createFn, out var unchanged, out var cycled);
            Assert.Equal(unchanged, cycled);
        }
    }
}