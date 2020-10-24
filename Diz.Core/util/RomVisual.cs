using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Diz.Core.model;
using FastBitmapLib;

namespace Diz.Core.util
{
    public class RomVisual
    {
        public bool AutoRefresh { get; set; } = true;

        public Data Data => Project?.Data;

        public event EventHandler ImageDataUpdated;
        public event EventHandler MarkedDirty;

        public Bitmap Bitmap
        {
            get
            {
                Refresh();
                return bitmap;
            }
        }

        public int PixelsPerBank => Data.GetBankSize();

        public int BankHeightPixels
        {
            get => bankHeightPixels;
            set
            {
                ValidateHeight(value);
                bankHeightPixels = value;
            }
        }

        public int BankWidthPixels
        {
            get
            {
                ValidateHeight(BankHeightPixels);
                return PixelsPerBank / BankHeightPixels;
            }
        }

        public void ValidateHeight(int heightPixels)
        {
            if (PixelsPerBank % heightPixels != 0)
                throw new ArgumentException(
                    "Selected Bank Height doesn't evenly divide. (pick a height that's a power of 2)");
        }

        public Project Project
        {
            get => project;
            set
            {
                if (ReferenceEquals(project, value)) return;
                project = value;
                if (project?.Data == null) return;
                project.Data.RomBytes.PropertyChanged += RomBytes_PropertyChanged;
                project.Data.RomBytes.CollectionChanged += RomBytes_CollectionChanged;
                InvalidateImage();
            }
        }

        private Bitmap bitmap;
        private Project project;
        private readonly object dirtyLock = new object();

        private void RomBytes_CollectionChanged(object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            AllDirty = true;
        }

        public bool AllDirty { get; set; } = true;
        public Dictionary<int, ROMByte> DirtyRomBytes = new Dictionary<int, ROMByte>();
        private int bankHeightPixels = 64;

        public bool IsDirty
        {
            get
            {
                lock (dirtyLock)
                {
                    return AllDirty ||
                           (AutoRefresh && DirtyRomBytes.Count > 0) ||
                           bitmap == null;
                }
            }
        }

        private void RomBytes_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!(sender is ROMByte romByte))
                return;

            if (e.PropertyName != "TypeFlag")
                return;

            MarkDirty(romByte);
        }

        private void InvalidateImage()
        {
            bitmap = null;
            AllDirty = true;
            lock (dirtyLock)
            {
                DirtyRomBytes.Clear();
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

            var totalHeight = BankHeightPixels * Data.GetNumberOfBanks();
            var totalWidth = BankWidthPixels;

            var shouldRecreateBitmap = bitmap == null ||
                                 bitmap.Width != totalWidth ||
                                 bitmap.Height != totalHeight;

            if (shouldRecreateBitmap)
                bitmap = new Bitmap(totalWidth, totalHeight);

            var romBytes = ConsumeRomDirtyBytes();

            var fastBitmap = new FastBitmap(bitmap); // needs compiler flag "/unsafe" enabled
            using (fastBitmap.Lock())
            {
                foreach (var romByte in romBytes)
                {
                    SetPixel(romByte, fastBitmap, totalWidth);
                }
            }

            AllDirty = false;
            OnBitmapUpdated();
        }

        // returns the RomBytes we should use to update our image
        // this can either be ALL RomBytes, or, a small set of dirty RomBytes that were changed
        // since our last redraw.
        private IEnumerable<ROMByte> ConsumeRomDirtyBytes()
        {
            if (AllDirty)
                return Data.RomBytes.ToList();

            IEnumerable<ROMByte> romBytes;
            lock (dirtyLock)
            {
                // make a copy so we can release the lock.
                romBytes = new List<ROMByte>(DirtyRomBytes.Values.Select(kvp => kvp));
                DirtyRomBytes.Clear();
            }

            return romBytes;
        }

        private static void SetPixel(ROMByte romByte, FastBitmap fastBitmap, int bankWidthPixels)
        {
            var romOffset = romByte.Offset;
            var y = romOffset / bankWidthPixels;
            var x = romOffset - (y * bankWidthPixels);
            var color = Util.GetColorFromFlag(romByte.TypeFlag);
            fastBitmap.SetPixel(x, y, color);
        }

        protected virtual void OnBitmapUpdated()
        {
            ImageDataUpdated?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void MarkDirty(ROMByte romByte)
        {
            lock (dirtyLock)
            {
                if (!DirtyRomBytes.ContainsKey(romByte.Offset))
                    DirtyRomBytes.Add(romByte.Offset, romByte);
                else
                    DirtyRomBytes[romByte.Offset] = romByte;
            }

            MarkedDirty?.Invoke(this, EventArgs.Empty);
        }
    }
}