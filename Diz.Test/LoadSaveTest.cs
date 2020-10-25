using System.Linq;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;
using Xunit;

namespace Diz.Test
{
    public class LoadSaveTest
    {
        [Fact]
        private void SerializeAndDeserialize()
        {
            // use the sample data to fake a project
            var sampleProject = new Project {Data = SampleRomData.SampleData};
            
            // extract the bytes that would normally be in the SMC file (they only exist in code for this sample data)
            var romFileBytes = sampleProject.Data.GetFileBytes();

            // save it to create an output byte stream, we'd normally write this to the disk
            var serializer = new ProjectXmlSerializer();
            var outputBytes = serializer.Save(sampleProject);

            // now do the reverse and load our output back as the input
            var (deserializedProject, warning) = serializer.Load(outputBytes);
            
            // final step, the loading process doesn't save the actual SMC file bytes, so we do it ourselves here
            deserializedProject.Data.CopyRomDataIn(romFileBytes);

            // now we can do a full compare between the original project, and the project which has been cycled through
            // serialization and deserialization
            Assert.True(warning == "");
            Assert.True(sampleProject.Equals(deserializedProject));
        }
    }
}