using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Diz.Core.Interfaces;
using Diz.Core.model;

namespace Diz.Core.model
{
    public class RomBytes : IRomBytes<RomByte>
    {
        private ObservableCollection<RomByte> bytes;

        // TODO: might be able to do something more generic for RomBytes now that other refactorings are completed.
        // This class needs to do these things that are special:
        // 1) Be handled specially by our custom XML serializer (compresses to save disk space)
        // 2) Handle Equals() by comparing each element in the list (SequenceEqual)
        // 3) Emit notifypropertychanged if any members change
        private ObservableCollection<RomByte> Bytes
        {
            get => bytes;
            set
            {
                bytes = value;

                bytes.CollectionChanged += Bytes_CollectionChanged;
                foreach (var romByte in bytes)
                {
                    romByte.PropertyChanged += RomByteObjectChanged;
                }
            }
        }

        public RomByte this[int i]
        {
            get => Bytes[i];
            set => Bytes[i] = value;
        }

        IRomByte IRomBytes<RomByte>.this[int i] => Bytes[i];

        public RomBytes()
        {
            Bytes = new ObservableCollection<RomByte>();
        }

        public void SetFrom(RomByte[] romBytes)
        {
            Bytes = new ObservableCollection<RomByte>(romBytes);
            for (var i = 0; i < romBytes.Length; ++i)
            {
                romBytes[i].SetCachedOffset(i);
            }
        }
        
        public void SetBytesFrom(IReadOnlyList<byte> copyFrom, int dstStartingOffset)
        {
            for (var i = 0; i < copyFrom.Count; ++i) 
                this[i + dstStartingOffset].Rom = copyFrom[i];
        }

        public int Count => Bytes.Count;
        public bool SendNotificationChangedEvents { get; set; } = true;

        public void Add(RomByte romByte)
        {
            Bytes.Add(romByte);
            romByte.SetCachedOffset(Bytes.Count - 1); // I don't love this....
        }

        public void Create(int size)
        {
            for (var i = 0; i < size; ++i)
                Add(new RomByte());
        }
        public void Clear()
        {
            Bytes.Clear();
        }

        #region Equality
        protected bool Equals(RomBytes other)
        {
            return Bytes.SequenceEqual(other.Bytes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RomBytes)obj);
        }

        public override int GetHashCode()
        {
            return (Bytes != null ? Bytes.GetHashCode() : 0);
        }
        #endregion

        #region Enumerator
        public IEnumerator<RomByte> GetEnumerator()
        {
            return Bytes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private void Bytes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (RomByte item in e.NewItems)
                    item.PropertyChanged += RomByteObjectChanged;

            if (e.OldItems != null)
                foreach (RomByte item in e.OldItems)
                    item.PropertyChanged -= RomByteObjectChanged;

            if (SendNotificationChangedEvents)
                CollectionChanged?.Invoke(sender, e);
        }

        private void RomByteObjectChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (SendNotificationChangedEvents)
                PropertyChanged?.Invoke(sender, e);
        }
    }
}


public static class RomBytesExtensions
{
    public static void CreateRomBytesFromRom(this RomBytes @this, IEnumerable<byte> actualRomBytes)
    {
        Debug.Assert(@this.Count == 0);
            
        var previousNotificationState = @this.SendNotificationChangedEvents;
        @this.SendNotificationChangedEvents = false;

        @this.Clear();
        foreach (var fileByte in actualRomBytes)
        {
            @this.Add(new RomByte
            {
                Rom = fileByte,
            });
        }

        @this.SendNotificationChangedEvents = previousNotificationState;
    }
        
    public static byte[] GetRomBytes(this IData @this, int pcOffset, int count)
    {
        var output = new byte[count];
        for (var i = 0; i < output.Length; i++)
            output[i] = (byte)@this.GetRomByte(pcOffset + i);

        return output;
    }

    public static void CopyRomDataIn(this IRomBytes<IRomByte> @this, IEnumerable<byte> trueRomBytes)
    {
        var previousNotificationState = @this.SendNotificationChangedEvents;
        @this.SendNotificationChangedEvents = false;
            
        var i = 0;
        foreach (var b in trueRomBytes)
        {
            @this[i].Rom = b;
            ++i;
        }
        Debug.Assert(@this.Count == i);

        @this.SendNotificationChangedEvents = previousNotificationState;
    }
}
