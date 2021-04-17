using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using FastBitmapLib;

namespace Diz.Core.util
{
    public class RomVisual
    {
        public event EventHandler ImageDataUpdated;
        public event EventHandler MarkedDirty;

        public bool AutoRefresh { get; set; } = true;

        public Data Data => Project?.Data;

        public Project Project
        {
            get => project;
            set
            {
                if (ReferenceEquals(project, value)) return;
                SwitchProject(value);
            }
        }

        private void SwitchProject(Project value)
        {
            bitmap = null;

            project = value;
            if (project?.Data == null) 
                return;

            // TODO
            //project.Data.RomBytes += RomBytes_PropertyChanged;
            //project.Data.RomBytes.CollectionChanged += RomBytes_CollectionChanged;
        }

        public bool IsDirty
        {
            get
            {
                lock (dirtyLock)
                {
                    return AllDirty ||
                           (AutoRefresh && dirtyRomBytes.Count > 0) ||
                           bitmap == null;
                }
            }
        }

        public Bitmap Bitmap
        {
            get
            {
                Refresh();
                return bitmap;
            }
        }

        public int RomStartingOffset
        {
            get => romStartingOffset;
            set
            {
                if (value < 0 || value >= project.Data.RomByteSource?.Bytes.Count)
                    throw new ArgumentOutOfRangeException();

                romStartingOffset = value;
            }
        }

        public int LengthOverride
        {
            get => lengthOverride;
            set
            {
                if (value != -1 && (value == 0 || RomStartingOffset + value > project.Data.RomByteSource?.Bytes.Count))
                    throw new ArgumentOutOfRangeException();

                lengthOverride = value;
            }
        }

        public int PixelCount => lengthOverride != -1 ? lengthOverride : project.Data.RomByteSource?.Bytes.Count ?? 0;

        private int RomMaxOffsetAllowed => RomStartingOffset + PixelCount - 1;

        public int Width
        {
            get => width;
            set
            {
                if (Width <= 0)
                    throw new ArgumentOutOfRangeException();

                width = value;
            }
        }

        // this rounds up the height by one pixel if needed, meaning our last row can be incomplete if Width doesn't divide evenly into Length
        public int Height => (int)Math.Ceiling(PixelCount / (double)Width);

        public bool AllDirty { get; set; } = true;

        private int romStartingOffset = 0;
        private int lengthOverride = -1;
        private int width = 1024;
        private Bitmap bitmap;
        private Project project;

        private readonly object dirtyLock = new object();
        private readonly Dictionary<int, ByteEntry> dirtyRomBytes = new Dictionary<int, ByteEntry>();

        private void RomBytes_CollectionChanged(object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            AllDirty = true;
        }

        private bool OffsetInRange(int offset)
        {
            return (offset >= romStartingOffset && offset < romStartingOffset + lengthOverride);
        }

        private void RomBytes_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!(sender is ByteEntry entry))
                return;

            if (e.PropertyName != nameof(MarkAnnotation.TypeFlag))
                return;

            if (!OffsetInRange(entry.ParentByteSourceIndex))
                return;

            MarkDirty(entry);
        }

        private void InvalidateImage()
        {
            bitmap = null;
            AllDirty = true;
            lock (dirtyLock)
            {
                dirtyRomBytes.Clear();
            }

            RegenerateImage();
        }

        public void Refresh()
        {
            if (IsDirty)
                RegenerateImage();
        }

        private void RegenerateImage()
        {
            if (Data == null)
            {
                bitmap = null;
                return;
            }

            var h = Height;
            var w = Width;

            var shouldRecreateBitmap = bitmap == null || bitmap.Width != w || bitmap.Height != h;
            if (shouldRecreateBitmap)
                bitmap = new Bitmap(w, h);

            var romBytes = ConsumeRomDirtyBytes(out var usedDirtyList);
            var currentPixel = 0;

            var fastBitmap = new FastBitmap(bitmap); // needs compiler flag "/unsafe" enabled
            using (fastBitmap.Lock())
            {
                foreach (var romByte in romBytes)
                {
                    SetPixel(romByte, fastBitmap);
                    ++currentPixel;
                }

                if (!usedDirtyList)
                {
                    // rom bytes may not fully fill up the last row. fill it in with
                    // blank pixels
                    while (currentPixel < w * h)
                    {
                        var (x, y) = ConvertPixelIndexToXy(currentPixel);
                        fastBitmap.SetPixel(x, y, Color.SlateGray);
                        ++currentPixel;
                    }
                }
            }

            AllDirty = false;
            OnBitmapUpdated();
        }

        // returns the RomBytes we should use to update our image
        // this can either be ALL RomBytes, or, a small set of dirty RomBytes that were changed
        // since our last redraw.
        private IEnumerable<ByteEntry> ConsumeRomDirtyBytes(out bool usedDirtyList)
        {
            usedDirtyList = false;

            if (AllDirty)
                return Data.RomByteSource?.Bytes
                    .Where(rb => 
                        rb.ParentByteSourceIndex >= RomStartingOffset && rb.ParentByteSourceIndex <= RomMaxOffsetAllowed
                    )
                    .ToList();

            usedDirtyList = true;
            IEnumerable<ByteEntry> romBytes;
            lock (dirtyLock)
            {
                // make a copy so we can release the lock.
                romBytes = new List<ByteEntry>(dirtyRomBytes.Values.Select(kvp => kvp));
                dirtyRomBytes.Clear();
            }

            return romBytes;
        }

        private (int x, int y) ConvertPixelIndexToXy(int offset)
        {
            var y = offset / Width;
            var x = offset - (y * Width);
            return (x, y);
        }

        private void SetPixel(ByteEntry byteOffset, FastBitmap fastBitmap)
        {
            var pixelIndex = ConvertRomOffsetToPixelIndex(byteOffset.ParentByteSourceIndex);
            var (x, y) = ConvertPixelIndexToXy(pixelIndex);
            var color = Util.GetColorFromFlag(byteOffset.GetOneAnnotation<MarkAnnotation>().TypeFlag);
            fastBitmap.SetPixel(x, y, color);
        }

        private int ConvertRomOffsetToPixelIndex(int romByteOffset)
        {
            return romByteOffset - RomStartingOffset;
        }

        protected virtual void OnBitmapUpdated()
        {
            ImageDataUpdated?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void MarkDirty(ByteEntry byteOffset)
        {
            lock (dirtyLock)
            {
                if (!dirtyRomBytes.ContainsKey(byteOffset.ParentByteSourceIndex))
                    dirtyRomBytes.Add(byteOffset.ParentByteSourceIndex, byteOffset);
                else
                    dirtyRomBytes[byteOffset.ParentByteSourceIndex] = byteOffset;
            }

            MarkedDirty?.Invoke(this, EventArgs.Empty);
        }
    }
}