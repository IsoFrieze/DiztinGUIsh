using System.Linq;
using Diz.Core.util;
using IX.Observable;

namespace Diz.Core.model
{
    public partial class Data
    {
        // don't modify these directly, always go through the public properties so
        // other objects can subscribe to modification notifications
        private RomMapMode romMapMode;
        private RomSpeed romSpeed = RomSpeed.Unknown;
        private ObservableDictionary<int, string> comments = new ObservableDictionary<int, string>();
        private ObservableDictionary<int, Label> labels = new ObservableDictionary<int, Label>();
        private RomBytes romBytes = new RomBytes();

        // Note: order of these public properties matters for the load/save process. Keep 'RomBytes' LAST
        // TODO: should be a way in the XML serializer to control the order, remove this comment
        // when we figure it out.
        public RomMapMode RomMapMode
        {
            get => romMapMode;
            set => SetField(ref romMapMode, value);
        }

        public RomSpeed RomSpeed
        {
            get => romSpeed;
            set => SetField(ref romSpeed, value);
        }

        // next 2 dictionaries store in SNES address format (since memory labels can't be represented as a PC address)
        public ObservableDictionary<int, string> Comments
        {
            get => comments;
            set => SetField(ref comments, value);
        }

        public ObservableDictionary<int, Label> Labels
        {
            get => labels;
            set => SetField(ref labels, value);
        }

        // RomBytes stored as PC file offset addresses (since ROM will always be mapped to disk)
        public RomBytes RomBytes
        {
            get => romBytes;
            set => SetField(ref romBytes, value);
        }


        #region Equality
        protected bool Equals(Data other)
        {
            return Labels.SequenceEqual(other.Labels) && RomMapMode == other.RomMapMode && RomSpeed == other.RomSpeed && Comments.SequenceEqual(other.Comments) && RomBytes.Equals(other.RomBytes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((Data)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Labels.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)RomMapMode;
                hashCode = (hashCode * 397) ^ (int)RomSpeed;
                hashCode = (hashCode * 397) ^ Comments.GetHashCode();
                hashCode = (hashCode * 397) ^ RomBytes.GetHashCode();
                return hashCode;
            }
        }
        #endregion
    }
}