using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Diz.Test.Utils;

public interface ISkippableTest
{
    string Skip { get; set; }
} 

public static class DependentFileChecker
{
    public static void SkipUnlessFilesExist(this ISkippableTest @this, string[] files) => 
        @this.Skip ??= CheckExists(files);
    
    private static string CheckExists(string[] files = null)
    {
        var toCheck = new List<string>();
        if (files != null)
            toCheck.AddRange(files);
        
        var missingFile = toCheck.Find(f => !File.Exists(f));
        return missingFile != null ? $"Can't find test prerequisite file {missingFile}" : null;
    }
}

public class FactOnlyIfFilePresent : FactAttribute, ISkippableTest
{
    public FactOnlyIfFilePresent(string[] files = null) => 
        this.SkipUnlessFilesExist(files);
}

public sealed class TheoryOnlyIfFilePresent : TheoryAttribute, ISkippableTest
{
    public TheoryOnlyIfFilePresent(string[] files = null) => 
        this.SkipUnlessFilesExist(files);
}