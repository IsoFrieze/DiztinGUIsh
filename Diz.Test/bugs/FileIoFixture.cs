using System;
using Diz.Core.serialization;
using Moq;

namespace Diz.Test.bugs;

/// <summary>
/// Simulate reading and writing to the same file
/// File bytes starts empty
/// </summary>
public class FileIoFixture
{
    public byte[] FakeFileBytes { get; set; } = Array.Empty<byte>();
    public Mock<IFileByteProvider> Mock { get; }

    public FileIoFixture()
    {
        Mock = new Mock<IFileByteProvider>();
        Mock.Setup(x => x.WriteBytes(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback((string filename, byte[] bytesToWrite) =>
                FakeFileBytes = bytesToWrite
            );

        Mock.Setup(x => x.ReadAllBytes(It.IsAny<string>()))
            .Returns<string>(_ =>
                FakeFileBytes
            );
    }
}