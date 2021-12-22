using Diz.Core.model;
using Diz.Core.util;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

public class ProjectSessionTest
{
    [Fact]
    void TestProjectSessionUnsaved()
    {
        var sampleProject = new Project();

        sampleProject.Session = new ProjectSession(sampleProject, "test");

        sampleProject.Session.UnsavedChanges.Should().BeFalse();
    }
}