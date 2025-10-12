using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Cpu._65816;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diz.Test.Utils;


public interface ISampleRomTestData
{
    byte[] SampleRomBytes { get; }
    public Project Project { get; }
}

public class SampleRomTestDataFixture : ContainerFixture, ISampleRomTestData
{
    public byte[] SampleRomBytes => sampleBytes.Value;
    
    [Inject] private readonly ISnesSampleProjectFactory sampleFactory = null!;
    private readonly Lazy<byte[]> sampleBytes;
    public SampleRomTestDataFixture()
    {
        sampleBytes = new Lazy<byte[]>(() =>
        {
            Project = sampleFactory!.Create() as Project;
            return Project!.Data.GetFileBytes().ToArray();
        });
    }

    public Project Project { get; set; }
}


public static class TheoryDataGenerator
{
    public static TheoryData<T> CreateTheoryData<T>(this IEnumerable<Func<T>> data)
    {
        return data
            .Select(fn => fn())
            .Aggregate(new TheoryData<T>(), (theoryData, item) =>
            {
                theoryData.Add(item);
                return theoryData;
            });
    }
}
    
public static class TestUtil
{
    public static void AssertCollectionEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
    {
        // do some weirdness here to better display the differences in the output window.
            
        var largestListCount = Math.Max(expected.Count, actual.Count);
        for (var i = 0; i < largestListCount; ++i)
        {
            // if this gets hit, lengths of lists are different
            (i < actual.Count).Should().BeTrue();
            (i < expected.Count).Should().BeTrue();

            var expectedItem = expected[i];
            var actualItem = actual[i];
                
            expectedItem.Should().BeEquivalentTo(actualItem);
        }
            
        expected.Count.Should().Be(actual.Count);

        expected.Should().BeEquivalentTo(actual);
    }

    public static Mock<IReadFromFileBytes> CreateReadFromFileMock(byte[] mockedFileBytes)
    {
        var mockLinkedRomBytesProvider = new Mock<IReadFromFileBytes>();
        mockLinkedRomBytesProvider.Setup(x =>
                x.ReadRomFileBytes(It.IsAny<string>()))
            .Returns<string>(filename => mockedFileBytes);

        return mockLinkedRomBytesProvider;
    }
}