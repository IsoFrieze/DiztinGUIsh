using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Diz.Core.model;

namespace DiztinGUIsh.window2
{
    /* DONT NEED ANYMORE
    public interface ISubView : INotifyPropertyChanged
    {
        // any valid subset of our data source
        public int SourceStart { get; set; }
        public int SourceCount { get; set; }

        // any valid index between SourceStart and SourceCurrentIndex
        public int SourceCurrentIndex { get; set; }
    }
    
    public class SubView<TItem, TStore> : PropertyNotifyChanged, ISubView where TStore : IList<TItem>
    {
        // SOURCE: window within Data.
        // SUB: window within that window

        // i.e. if Data is 100 elements
        // SourceStart = 25, SourceCount = 25 means we can access elements 25 through 50
        // SubIndex = 0 means SourceIndex = 25, gives you Data[25]. Subindex range is 0 through 25.

        private int sourceStart;
        private int sourceCount;
        private int sourceCurrentIndex;

        public int SourceStart
        {
            get => sourceStart;
            set
            {
                if (sourceStart < 0 || sourceStart >= SourceData.Count)
                    throw new ArgumentOutOfRangeException();

                SetField(ref sourceStart, value);
            }
        }

        public int SourceCount
        {
            get => sourceCount;
            set
            {
                if (sourceStart + sourceCount > SourceData.Count)
                    throw new ArgumentOutOfRangeException();

                SetField(ref sourceCount, value);
            }
        }

        public int SourceCurrentIndex
        {
            get => sourceCurrentIndex;
            set
            {
                if (!IsSourceIndexInRange(value))
                    throw new IndexOutOfRangeException();

                SetField(ref sourceCurrentIndex, value);
            }
        }

        public TStore SourceData { get; init; }

        private bool IsSourceIndexInRange(int i)
        {
            return i >= SourceStart && i < SourceStart + SourceCount;
        }

        // will be in range of (0...count]
        private int ConvertSubIndexToSource(int i)
        {
            return SourceStart + i;
        }

        public TItem this[int i]
        {
            get
            {
                if (!IsSourceIndexInRange(i))
                    throw new IndexOutOfRangeException();

                return SourceData[ConvertSubIndexToSource(i)];
            }
            set
            {
                if (!IsSourceIndexInRange(i))
                    throw new IndexOutOfRangeException();

                SourceData[ConvertSubIndexToSource(i)] = value;
            }
        }
    }

    public class RomDataView : SubView<RomByte, RomBytes>
    {
        public IEnumerable<byte> GetBytes()
        {
            return SourceData.AsEnumerable().Skip(SourceStart).Take(SourceCount).Select(rb => rb.Rom);
        }
    }
    */
}