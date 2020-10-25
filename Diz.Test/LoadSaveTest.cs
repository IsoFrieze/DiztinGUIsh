using System.Xml;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using IX.Observable;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test
{
    public class LoadSaveTest
    {
        [Fact]
        private void SerializeAndDeserialize()
        {
            var sampleProject = new Project {Data = SampleRomData.SampleData};

            var serializer = new ProjectXmlSerializer();
            var outputBytes = serializer.Save(sampleProject);

            var (deserializedProject, warning) = serializer.Load(outputBytes);

            Assert.True(warning == "");
            Assert.True(sampleProject.Equals(deserializedProject));

            // todo: run a couple tests where we mess with the end repeater bytes
        }
    }
}