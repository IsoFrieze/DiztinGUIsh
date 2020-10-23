using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security.AccessControl;
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
                throw new ArgumentException("Selected Bank Height doesn't evenly divide. (pick a height that's a power of 2)");
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

        private void RomBytes_CollectionChanged(object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            AllDirty = true;
        }

        public bool AllDirty { get; set; } = true;
        public Dictionary<int, ROMByte> DirtyRomBytes = new Dictionary<int, ROMByte>();
        private int bankHeightPixels = 64;

        public bool NeedsUpdate =>
            AllDirty ||
            (AutoRefresh && DirtyRomBytes.Count > 0);

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
            DirtyRomBytes.Clear();
            RegenerateImage();
        }

        public void Refresh()
        {
            if (bitmap == null || NeedsUpdate)
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

            // requires use of the /unsafe compiler flag, because we're manipulating memory directly.
            // needed because the system bitmap SetPixel() operation is super-slow.
            var shouldRecreate = bitmap == null || 
                           bitmap.Width != totalWidth || 
                           bitmap.Height != totalHeight;

            if (shouldRecreate)
                bitmap = new Bitmap(totalWidth, totalHeight);

            var romBytes = AllDirty ? 
                Data.RomBytes.ToList() : 
                DirtyRomBytes.Values.Select(kvp => kvp);

            var fastBitmap = new FastBitmap(bitmap);
            using (fastBitmap.Lock())
            {
                foreach (var romByte in romBytes)
                {
                    SetPixel(romByte, fastBitmap, totalWidth);
                }
            }

            DirtyRomBytes.Clear();
            AllDirty = false;

            OnBitmapUpdated();
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
            if (!DirtyRomBytes.ContainsKey(romByte.Offset))
                DirtyRomBytes.Add(romByte.Offset, romByte);
            else
                DirtyRomBytes[romByte.Offset] = romByte;

            MarkedDirty?.Invoke(this, EventArgs.Empty);
        }
    }
}