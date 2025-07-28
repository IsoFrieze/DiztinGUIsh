using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Diz.Core.Interfaces;
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
    
    [Serializable]
    public class ContextMapping : IContextMapping
    {
        private string context = "";
        private string nameOverride = "";

        public string Context
        {
            get => context;
            set { context = value; OnPropertyChanged(); }
        }

        public string NameOverride
        {
            get => nameOverride;
            set { nameOverride = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // represent a label at a particular SNES address
    //
    // Comments here are for the LABEL itself, and not so much about where they're used.
    // i.e. a label for 0x7E0020 might store a character's HP in RAM. It would look like:
    // - address: snes address 0x7E0020 (i.e. mapped to a WRAM address)
    // - label:   "character_3_hp"
    // - comment: "this address is only used in RAM during battle sequences"
    public class Label : Annotation, IAnnotationLabel, IComparable<Label>, IComparable
    {
        private string comment = "";
        private string name = "";

        public ObservableCollection<IContextMapping> ContextMappings { get; set; } = [];
        IEnumerable<IReadOnlyContextMapping> IReadOnlyLabel.ContextMappings => ContextMappings;
        ObservableCollection<IContextMapping> IAnnotationLabel.ContextMappings => ContextMappings;

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

        public string GetName(string contextName = "")
        {
            var mapping = ContextMappings.FirstOrDefault(cm => cm.Context == contextName);
            var overriddenName = mapping?.NameOverride ?? Name;
            return string.IsNullOrWhiteSpace(overriddenName) ? Name : overriddenName;
        }

        #region Equality

        private bool Equals(Label other)
        {
            return Name == other.Name && 
                   Comment == other.Comment && 
                   ContextMappings.SequenceEqual(other.ContextMappings, new ContextMappingComparer());
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
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Comment != null ? Comment.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ GetContextMappingsHashCode();
                return hashCode;
            }
        }

        private int GetContextMappingsHashCode()
        {
            unchecked
            {
                var hash = 0;
                foreach (var mapping in ContextMappings)
                {
                    hash = (hash * 397) ^ (mapping.Context?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ (mapping.NameOverride?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }

        public int CompareTo(Label other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            
            var nameComparison = string.Compare(name, other.name, StringComparison.Ordinal);
            if (nameComparison != 0) return nameComparison;
            
            var commentComparison = string.Compare(comment, other.comment, StringComparison.Ordinal);
            if (commentComparison != 0) return commentComparison;
            
            return CompareContextMappings(other);
        }

        private int CompareContextMappings(Label other)
        {
            var thisCount = ContextMappings.Count;
            var otherCount = other.ContextMappings.Count;
            
            var countComparison = thisCount.CompareTo(otherCount);
            if (countComparison != 0) return countComparison;
            
            var thisSorted = ContextMappings.OrderBy(cm => cm.Context).ThenBy(cm => cm.NameOverride).ToList();
            var otherSorted = other.ContextMappings.OrderBy(cm => cm.Context).ThenBy(cm => cm.NameOverride).ToList();
            
            for (int i = 0; i < thisCount; i++)
            {
                var contextComparison = string.Compare(thisSorted[i].Context, otherSorted[i].Context, StringComparison.Ordinal);
                if (contextComparison != 0) return contextComparison;
                
                var nameOverrideComparison = string.Compare(thisSorted[i].NameOverride, otherSorted[i].NameOverride, StringComparison.Ordinal);
                if (nameOverrideComparison != 0) return nameOverrideComparison;
            }
            
            return 0;
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is Label other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Label)}");
        }

        #endregion

        // Helper class for comparing ContextMapping objects
        private class ContextMappingComparer : IEqualityComparer<IContextMapping>
        {
            public bool Equals(IContextMapping x, IContextMapping y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                return x.Context == y.Context && x.NameOverride == y.NameOverride;
            }

            public int GetHashCode(IContextMapping obj)
            {
                unchecked
                {
                    return ((obj.Context?.GetHashCode() ?? 0) * 397) ^ (obj.NameOverride?.GetHashCode() ?? 0);
                }
            }
        }
        
        public bool IsDefault() => !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Comment);
    }

    // used mostly in the assembly text exporting process for generating temp labels that need extra info.
    // don't serialize or use outside that context.
    public class TempLabel : Label
    {
        [Flags]
        public enum TempLabelFlags
        {
            None = 0,
            DisallowPlusMinusGeneration = 0x01,
        }

        public TempLabelFlags Flags { get; set; } = TempLabelFlags.None;
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