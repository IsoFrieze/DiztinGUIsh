#nullable enable
using System.IO;

namespace Diz.Core.serialization;

public class FileByteProviderSingleFile : IFileByteProvider
{
    public byte[] ReadAllBytes(string filename) => 
        File.ReadAllBytes(filename);
    
    public void WriteBytes(string filename, byte[] data) => 
        File.WriteAllBytes(filename, data);
}