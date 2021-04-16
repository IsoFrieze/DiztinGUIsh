using System.Collections.Generic;
using System.Diagnostics;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test
{
    public class LoadSaveTest
    {
        [Fact(Skip="temporarily not working, serialization is busted.")]
        private void FullSerializeAndDeserialize()
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
            deserializedProject.Data.PopulateFrom((IReadOnlyCollection<byte>) romFileBytes);

            // now we can do a full compare between the original project, and the project which has been cycled through
            // serialization and deserialization
            Assert.True(warning == "");
            Assert.True(sampleProject.Equals(deserializedProject));
        }
        
        private readonly ITestOutputHelper output;
        public LoadSaveTest(ITestOutputHelper output)
        {
            this.output = output;
        }
        
        private static Project OpenProject(string openFile)
        {
            var projectFileManager = new ProjectFileManager();
            var (project, warning) = projectFileManager.Open(openFile);

            Assert.Equal("", warning);
            Assert.True(project.Data.RomByteSource?.Bytes.Count >= 0x1000 * 64);
            
            return project;
        }

        [Fact(Skip = "Performance Test")]
        private void OpenFilePerformanceTest()
        {
            var s = Stopwatch.StartNew();
            s.Start();
            
            var openFile = "INSERT YOUR FILE HERE BEFORE RUNNING THIS TEST.dizraw";
            var project = OpenProject(openFile);

            s.Stop();

            output.WriteLine($"runtime: {s.ElapsedMilliseconds:N0}, #bytes={project.Data.RomByteSource?.Bytes.Count}");
        }


        [Fact(Skip = "Performance Test")]
        private void SaveFilePerformanceTest()
        {
            var openFile = "INSERT YOUR FILE HERE BEFORE RUNNING THIS TEST.dizraw";
            var project = OpenProject(openFile);
            
            var s = Stopwatch.StartNew();
            s.Start();

            var data = new ProjectXmlSerializer().Save(project);

            s.Stop();
            
            Assert.True(data.Length != 0);

            output.WriteLine($"runtime: {s.ElapsedMilliseconds:N0}");
        }
    }
}
