using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Diz.Core.util;
using JetBrains.Annotations;

#if DIZ_3_BRANCH
using Diz.Core.model.byteSources;
#endif

namespace Diz.Core.model
{
    public abstract class Annotation : AnnotationBase
    #if !DIZ_3_BRANCH
    {}
    #else
        , IParentAware<ByteEntry>
    {
        public ByteEntry Parent { get; protected set; }
        public void OnParentChanged(ByteEntry parent)
        {
            Parent = parent;
        }
    } 
    #endif
    
    public abstract class AnnotationBase : INotifyPropertyChangedExt
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MarkAnnotation : Annotation, IComparable<MarkAnnotation>, IComparable
    {
        public FlagType TypeFlag
        {
            get => typeFlag;
            set => this.SetField(ref typeFlag, value);
        }
        
        private FlagType typeFlag = FlagType.Unreached;

        protected bool Equals(MarkAnnotation other)
        {
            return typeFlag == other.typeFlag;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MarkAnnotation) obj);
        }
        
        public override int GetHashCode()
        {
            return (int) typeFlag;
        }
        
        public int CompareTo(MarkAnnotation other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return typeFlag.CompareTo(other.typeFlag);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is MarkAnnotation other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(MarkAnnotation)}");
        }
    }

    public class ByteAnnotation : Annotation, IComparable<ByteAnnotation>, IComparable
    {
        public byte Val
        {
            get => dataByte;
            set => this.SetField(ref dataByte, value);
        }
        
        private byte dataByte;

        #region Generated Comparison
        public int CompareTo(ByteAnnotation other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return dataByte.CompareTo(other.dataByte);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is ByteAnnotation other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(ByteAnnotation)}");
        }

        protected bool Equals(ByteAnnotation other)
        {
            return dataByte == other?.dataByte;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ByteAnnotation) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), dataByte);
        }
        #endregion
    }
    
    public class OpcodeAnnotation : Annotation, IComparable<OpcodeAnnotation>, IComparable
    {
        public byte DataBank
        {
            get => dataBank;
            set => this.SetField(ref dataBank, value);
        }

        public int DirectPage
        {
            get => directPage;
            set => this.SetField(ref directPage, value);
        }

        public bool XFlag
        {
            get => xFlag;
            set => this.SetField(ref xFlag, value);
        }

        public bool MFlag
        {
            get => mFlag;
            set => this.SetField(ref mFlag, value);
        }
        
        public Architecture Arch
        {
            get => arch;
            set => this.SetField(ref arch, value);
        }

        private byte dataBank;
        private int directPage;
        private bool xFlag;
        private bool mFlag;

        private Architecture arch;

        #region Equality
        protected bool Equals(OpcodeAnnotation other)
        {
            return DataBank == other.DataBank && DirectPage == other.DirectPage && XFlag == other.XFlag && MFlag == other.MFlag && Arch == other.Arch;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((OpcodeAnnotation)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = DataBank.GetHashCode();
                hashCode = (hashCode * 397) ^ DirectPage;
                hashCode = (hashCode * 397) ^ XFlag.GetHashCode();
                hashCode = (hashCode * 397) ^ MFlag.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Arch;
                return hashCode;
            }
        }
        public int CompareTo(OpcodeAnnotation other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var dataBankComparison = dataBank.CompareTo(other.dataBank);
            if (dataBankComparison != 0) return dataBankComparison;
            var directPageComparison = directPage.CompareTo(other.directPage);
            if (directPageComparison != 0) return directPageComparison;
            var xFlagComparison = xFlag.CompareTo(other.xFlag);
            if (xFlagComparison != 0) return xFlagComparison;
            var mFlagComparison = mFlag.CompareTo(other.mFlag);
            if (mFlagComparison != 0) return mFlagComparison;
            return arch.CompareTo(other.arch);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is OpcodeAnnotation other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(OpcodeAnnotation)}");
        }
        #endregion
    } 
    
    // technically, this computed data can be re-created at any time and we keep it because:
    // 1) serialize so we don't have to recompute on load
    // 2) so it's faster when figuring out what to display to the user (vs recomputing on the fly)
    public class BranchAnnotation : Annotation, IComparable<BranchAnnotation>, IComparable
    {
        // never modify fields directly. only go through the public fields
        // cached mark if it's an in vs out point
        private InOutPoint point = InOutPoint.None;
        
        // cached data
        public InOutPoint Point
        {
            get => point;
            set => this.SetField(ref point, value);
        }
        
        #region Equality
        protected bool Equals(BranchAnnotation other)
        {
            return Point == other.Point;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((BranchAnnotation)obj);
        }

        public override int GetHashCode()
        {
            var hashCode = (int)Point;
            return hashCode;
        }
        
        public int CompareTo(BranchAnnotation other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return point.CompareTo(other.point);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is BranchAnnotation other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(BranchAnnotation)}");
        }
        #endregion
    }

    // represent a label at a particular SNES address
    //
    // Comments here are for the LABEL itself, and not so much about where they're used.
    // i.e. a label for 0x7E0020 might store a character's HP in RAM. It would look like:
    // - address: 0x7E0020 (if HiRom, 0x7EXXXX means it's a RAM address)
    // - label:   "character_3_hp"
    // - comment: "this address is only used in RAM during battle sequences"
    public class Label : Annotation, IReadOnlyLabel, IComparable<Label>, IComparable
    {
    
        private string comment = "";
        private string name = "";

        public string Name
        {
            get => name;
            set => this.SetField(ref name, value ?? "");
        }

        public string Comment
        {
            get => comment;
            set => this.SetField(ref comment, value ?? "");
        }
        
        #region Equality

        protected bool Equals(Label other)
        {
            return Name == other.Name && Comment == other.Comment;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Label)obj);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Comment != null ? Comment.GetHashCode() : 0);
            }
        }
        
        public int CompareTo(Label other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var commentComparison = string.Compare(comment, other.comment, StringComparison.Ordinal);
            if (commentComparison != 0) return commentComparison;
            return string.Compare(name, other.name, StringComparison.Ordinal);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is Label other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Label)}");
        }


        #endregion

        public bool IsDefault() => !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Comment);
    }
    
    public class Comment : Annotation, IComparable<Comment>, IComparable
    {
        private string text = "";

        public string Text
        {
            get => text;
            set => this.SetField(ref text, value ?? "");
        }

        #region Equality

        protected bool Equals(Comment other)
        {
            return Text == other.Text;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Comment)obj);
        }
        public override int GetHashCode()
        {
            return Text != null ? Text.GetHashCode() : 0;
        }
        
        public int CompareTo(Comment other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return string.Compare(text, other.text, StringComparison.Ordinal);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is Comment other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Comment)}");
        }

        #endregion
    }
}