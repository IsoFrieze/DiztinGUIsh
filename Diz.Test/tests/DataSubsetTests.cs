using System.Collections.Generic;
using System.Linq;
using Diz.Core.datasubset;
using Diz.Core.model.snes;
using Iced.Intel;
using Xunit;

namespace Diz.Test.tests
{
    public static class DataSubsetTests
    {
        public class TestRow
        {
            public TestItem OriginalItem { get; set; }
        }

        public class TestItem
        {
            public int CopyOfSourceLargeIndex { get; set; }
        }

        public class DataSubsetTestHarness
        {
            public List<TestItem> DataSource { get; init; }
            public DataSubsetWithSelection<TestRow, TestItem> DataSubset { get; init; }

            public void AssertSelectedIndexCorrect(int expectedLargeIndex)
            {
                Assert.Equal(expectedLargeIndex,DataSubset.SelectedRow.OriginalItem.CopyOfSourceLargeIndex);
            }
        }

        private static DataSubsetTestHarness CreateSetupData()
        {
            return new()
            {
                DataSource = Enumerable.Range(0, 1000)
                    .Select(i => new TestItem {CopyOfSourceLargeIndex = i})
                    .ToList(),
                
                DataSubset = new DataSubsetWithSelection<TestRow, TestItem>
                {
                    Items = Enumerable.Range(0, 1000)
                        .Select(i => new TestItem {CopyOfSourceLargeIndex = i})
                        .ToList(),
                    RowLoader = new DataSubsetSimpleLoader<TestRow, TestItem>
                    {
                        PopulateRow = (ref TestRow rowToPopulate, int largeIndex) =>
                        {
                            rowToPopulate.OriginalItem = Enumerable.Range(0, 1000)
                                .Select(i => new TestItem {CopyOfSourceLargeIndex = i})
                                .ToList()[largeIndex];
                        }
                    },
                    RowCount = 10,
                    StartingRowLargeIndex = 15,
                    EndingRowLargeIndex = 24,
                    SelectedLargeIndex = 17
                },
            };
        }
        
        
        [Fact]
        public static void TestSelection()
        {
            var harness = CreateSetupData();
            var subset = harness.DataSubset;

            Assert.Equal(10, subset.RowCount);
            Assert.Equal(10, subset.OutputRows.Count);
            
            harness.AssertSelectedIndexCorrect(17);

            harness.DataSubset.SelectRow(0);
            harness.AssertSelectedIndexCorrect(15);
            
            harness.DataSubset.SelectRow(9);
            harness.AssertSelectedIndexCorrect(24);
        }
    }
}