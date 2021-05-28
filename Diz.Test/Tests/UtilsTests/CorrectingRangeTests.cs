using Diz.Core.util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diz.Test.Tests.UtilsTests
{
    public class CorrectingRangeTests
    {
        [Fact]
        public void TestSomeBasics()
        {
            var range = new CorrectingRange(100) {StartIndex = 2, RangeCount = 10};
            range.EndIndex.Should().Be(11);

            range.EndIndex = 11;
            range.RangeCount.Should().Be(10);

            range.EndIndex = 99;
            range.StartIndex.Should().Be(100 - 10);
            range.RangeCount.Should().Be(10);

            range.StartIndex = 95;
            range.RangeCount.Should().Be(5);

            range.StartIndex = -1;
            range.StartIndex.Should().Be(0);
            range.EndIndex.Should().Be(5 - 1);
            
            range.StartIndex = 101;
            range.StartIndex.Should().Be(99);
        }

        [Fact]
        public void TestInvalid()
        {
            var range = new CorrectingRange(100);
            range.EndIndex.Should().Be(-1);
            range.RangeCount.Should().Be(0);
            range.StartIndex.Should().Be(0);

            range.RangeCount = 1;
            range.EndIndex.Should().Be(0);
            range.RangeCount.Should().Be(1);
            range.StartIndex.Should().Be(0);
            
            range.RangeCount = 10;
            range.EndIndex.Should().Be(9);
            range.RangeCount.Should().Be(10);
            range.StartIndex.Should().Be(0);
        }

        [Fact]
        public void TestCombos()
        {
            new CorrectingRange(100) {StartIndex = 2, RangeCount = 10};
            new CorrectingRange(100) {StartIndex = 2, EndIndex = 20, RangeCount = 10};
            new CorrectingRange(100) {EndIndex = 20, RangeCount = 10, StartIndex = 2};
        }
    }
}