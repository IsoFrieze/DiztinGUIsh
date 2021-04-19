using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.datasubset;
using Diz.Core.util;
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

            subset.SelectRow(0);
            subset.SelectRow(9);
            Assert.ThrowsAny<Exception>(() => subset.SelectRow(10));
            Assert.ThrowsAny<Exception>(() => subset.SelectRow(-1));
        }

        [Fact]
        public static void TestNotifyPropChangedFieldEqual()
        {
            Assert.True(NotifyPropertyChangedExtensions.FieldIsEqual(2, 2));

            var x = new object();
            var y = x;
            Assert.True(NotifyPropertyChangedExtensions.FieldIsEqual(x,y, compareRefOnly: true));
        }

        [Fact]
        public static void TestClampIndex()
        {
            void TestSize(int input, int expected, int size) => 
                Assert.Equal(expected, Util.ClampIndex(input, size));
            
            TestSize(input: 5, expected: 5, size: 10);
            TestSize(input: 0, expected: 0, size: 10);
            TestSize(input: 10, expected: 9, size: 10);
            TestSize(input: 11, expected: 9, size: 10);
            TestSize(input: -1, expected: 0, size: 10);
        }

        [Fact]
        public static void TestClampMinMax()
        {
            void TestClamp(int input, int expected, int minIndex, int maxIndex) => 
                Assert.Equal(expected, Util.ClampIndex(input, minIndex, maxIndex));

            TestClamp(input: -1, expected: 0, minIndex: 0, maxIndex: 9);
            TestClamp(input: 0, expected: 0, minIndex: 0, maxIndex: 9);
            TestClamp(input: 1, expected: 1, minIndex: 0, maxIndex: 9);
            
            TestClamp(input: 8, expected: 8, minIndex: 0, maxIndex: 9);
            TestClamp(input: 9, expected: 9, minIndex: 0, maxIndex: 9);
            TestClamp(input: 10, expected: 9, minIndex: 0, maxIndex: 9);
        }

        [Fact]
        public static void TestSelectionBoundedToWindow()
        {
            var harness = CreateSetupData(numSourceItems: 100, numRows: 10);
            var subset = harness.DataSubset;
            subset.WindowResizeKeepsSelectionInRange = true;
            subset.EnsureBoundariesEncompassWhenSelectionChanges = false;
            
            Assert.False(subset.EnsureBoundariesEncompassWhenSelectionChanges);
            Assert.True(subset.WindowResizeKeepsSelectionInRange);
            
            // setup a window with 10 items starting at index=10 [so.. 10...19 inclusive]
            subset.StartingRowLargeIndex = 10;
            subset.EndingRowLargeIndex = 19;
            Assert.Equal(10, subset.RowCount);
            Assert.Equal(10, subset.OutputRows.Count);
            
            harness.AssertSelectedLargeIndexCorrect(10);
            harness.AssertSelectedRowIndexCorrect(0);
            
            // if we try and set the index outside of that window, the selection should be allowed
            // outside the window AND the boundaries shouldn't change
            Assert.False(subset.EnsureBoundariesEncompassWhenSelectionChanges);
            subset.SelectedLargeIndex = 0;
            harness.AssertSelectedLargeIndexCorrect(0);
            harness.AssertSelectedRowIndexCorrect(-1);
            Assert.Equal(10, subset.StartingRowLargeIndex);
            Assert.Equal(19, subset.EndingRowLargeIndex);

            subset.SelectedLargeIndex = 25;
            harness.AssertSelectedLargeIndexCorrect(25);
            harness.AssertSelectedRowIndexCorrect(-1);
            Assert.Equal(10, subset.StartingRowLargeIndex);
            Assert.Equal(19, subset.EndingRowLargeIndex);
        }

        [Fact]
        public static void TestWindowMovesWithSelection()
        {
            var harness = CreateSetupData(numSourceItems: 100, numRows: 10);
            var subset = harness.DataSubset;

            subset.EnsureBoundariesEncompassWhenSelectionChanges = true;
            subset.WindowResizeKeepsSelectionInRange = true;
            
            // setup a window with 10 items starting at index=10 [so.. 10...19 inclusive]
            subset.StartingRowLargeIndex = 10;
            subset.EndingRowLargeIndex = 19;
            Assert.Equal(10, subset.StartingRowLargeIndex);
            Assert.Equal(19, subset.EndingRowLargeIndex);
            harness.AssertSelectedLargeIndexCorrect(10);
            
            // select something below it, we expect the window to snap so we're now at [0...9]
            harness.DataSubset.SelectedLargeIndex = 0;
            harness.AssertSelectedLargeIndexCorrect(0);
            Assert.Equal(0, subset.StartingRowLargeIndex);
            Assert.Equal(9, subset.EndingRowLargeIndex);
            Assert.Equal(10, subset.RowCount);
            Assert.Equal(10, subset.OutputRows.Count);
            
            // select something above it, we expect the window to snap so we're ending at 29 so [20...29] 
            harness.DataSubset.SelectedLargeIndex = 29;
            harness.AssertSelectedLargeIndexCorrect(29);
            harness.AssertSelectedRowIndexCorrect(9);
            Assert.Equal(20, subset.StartingRowLargeIndex);
            Assert.Equal(29, subset.EndingRowLargeIndex);
            
            // cool, now select something on the far end of this thing, make sure window snaps
            harness.SelectLargeIndexAndVerify(99, 9);
            Assert.Equal(90, subset.StartingRowLargeIndex);
            Assert.Equal(99, subset.EndingRowLargeIndex);
            
            // change starting boundary, selection will change
            subset.StartingRowLargeIndex = 40;
            harness.AssertSelectedLargeIndexCorrect(49);
            
            subset.EndingRowLargeIndex = 59;
            harness.AssertSelectedLargeIndexCorrect(50);
            
            subset.RowCount = 20;
            harness.AssertSelectedLargeIndexCorrect(50);
            
            subset.EndingRowLargeIndex = 20;
            harness.AssertSelectedLargeIndexCorrect(20);
        }

        [Fact]
        public static void TestWindowMovePastEnd()
        {
            var harness = CreateSetupData(numSourceItems: 100, numRows: 10);
            var subset = harness.DataSubset;

            subset.EnsureBoundariesEncompassWhenSelectionChanges = true;
            subset.WindowResizeKeepsSelectionInRange = true;
            
            subset.StartingRowLargeIndex = 90;
            Assert.ThrowsAny<Exception>(() => subset.StartingRowLargeIndex = 91);

            subset.EndingRowLargeIndex = 9;
            Assert.ThrowsAny<Exception>(() => subset.EndingRowLargeIndex = 8);
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