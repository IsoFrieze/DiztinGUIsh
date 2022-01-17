using System;
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
            throw new NotImplementedException();
            
            // setup. use InjectData now instead of this mess
            // // TODO: need to inject SNES api, something vaguely like....
            // var dataFactoryMock = new Mock<IProjectDataFactory>();
            // dataFactoryMock.Setup(x => x.Create()).Returns(new Data());
            // var data = dataFactoryMock.Object;
            //
            // var factory = new XmlSerializerFactory(dataFactoryMock,
            //     () => new XmlSerializerFactory.SnesDataInterceptor(this));
            
            // actual test below:
            // var xmlToCycle = XmlSerializerFactory.GetSerializer().Create().Serialize(objToCycle);
            // Output?.WriteLine(xmlToCycle);
            // var deserialized = XmlSerializerFactory.GetSerializer().Create().Deserialize<T>(xmlToCycle);
            // return deserialized;
        }
        
        public void RunFullCycle(Func<object> createFn)
        {
            RunFullCycle(createFn, out var unchanged, out var cycled);
            Assert.Equal(unchanged, cycled);
        }
    }
}