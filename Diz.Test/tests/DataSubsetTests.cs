using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.datasubset;
using Diz.Core.model.snes;
using Diz.Core.util;
using Iced.Intel;
using Xunit;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Global

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

            public void AssertSelectedLargeIndexCorrect(int expectedLargeIndex)
            {
                Assert.Equal(expectedLargeIndex,DataSubset.SelectedRow.OriginalItem.CopyOfSourceLargeIndex);
            }
            
            public void AssertSelectedRowIndexCorrect(int expectedRowIndex)
            {
                Assert.Equal(expectedRowIndex,DataSubset.SelectedRowIndex);
            }

            public void SelectLargeIndexAndVerify(int largeIndexToSelect, int expectedRowIndex)
            {
                DataSubset.SelectedLargeIndex = largeIndexToSelect;
                AssertSelectedLargeIndexCorrect(largeIndexToSelect);
                AssertSelectedRowIndexCorrect(expectedRowIndex);
            }
        }

        private static DataSubsetTestHarness CreateSetupData(int numSourceItems, int numRows)
        {
            var srcData = Enumerable.Range(0, numSourceItems)
                .Select(i => new TestItem {CopyOfSourceLargeIndex = i})
                .ToList();
            
            var harness = new DataSubsetTestHarness
            {
                DataSource = srcData,
                DataSubset = new DataSubsetWithSelection<TestRow, TestItem>
                {
                    Items = srcData,
                    RowLoader = new DataSubsetSimpleLoader<TestRow, TestItem>
                    {
                        PopulateRow = (ref TestRow rowToPopulate, int largeIndex) =>
                        {
                            Assert.NotNull(srcData);
                            Assert.True(Util.ClampIndex(largeIndex, srcData.Count) == largeIndex);
                            Assert.NotNull(rowToPopulate);
                            Assert.Null(rowToPopulate.OriginalItem);
                            
                            rowToPopulate.OriginalItem = srcData[largeIndex];
                        }
                    }
                }
            };

            var subset = harness.DataSubset;

            if (numRows > 0)
                subset.RowCount = numRows;
                
            return harness;
        }
        
        
        [Fact]
        public static void TestSelection()
        {
            var harness = CreateSetupData(numSourceItems: 100, numRows: 10);
            var subset = harness.DataSubset;

            subset.StartingRowLargeIndex = 15;
            subset.EndingRowLargeIndex = 24;
            subset.SelectedLargeIndex = 17;

            Assert.Equal(10, subset.RowCount);
            Assert.Equal(10, subset.OutputRows.Count);
            
            harness.SelectLargeIndexAndVerify(17, 2);
            harness.SelectLargeIndexAndVerify(15, 0);
            harness.SelectLargeIndexAndVerify(24, 9);
        }
        
        [Fact]
        public static void TestEmpty()
        {
            var harness = CreateSetupData(numSourceItems: 0, numRows: 0);
            var subset = harness.DataSubset;

            Assert.Equal(0, subset.RowCount);
            Assert.Empty(subset.OutputRows);

            Assert.ThrowsAny<Exception>(() => subset.StartingRowLargeIndex = 2);
            Assert.ThrowsAny<Exception>(() => subset.StartingRowLargeIndex = -2);
            Assert.ThrowsAny<Exception>(() => subset.EndingRowLargeIndex = 2);
            Assert.ThrowsAny<Exception>(() => subset.EndingRowLargeIndex = -2);
            Assert.ThrowsAny<Exception>(() => subset.EndingRowLargeIndex = 2);
            Assert.ThrowsAny<Exception>(() => subset.EndingRowLargeIndex = -200);
        }
    }
}