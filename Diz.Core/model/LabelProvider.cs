using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using Diz.Core.Interfaces;
#if DIZ_3_BRANCH
using Diz.Core.model.byteSources;
#endif
using Diz.Core.model.snes;
using JetBrains.Annotations;

namespace Diz.Core.model
{
    // this system could probably use a redesign.
    // the entire point of this class is to route all read/writes for Label class
    // through one point.  then, we can augment the real labels (user-created)
    // with temporary labels (like temporarily generated during the output assembly code generation).
    //
    // when there's no need for assembly labels anymore, we can dump them.
    //
    // I think once things are further along, it should be possible to just use a new ByteSource that's overlaid
    // on top of SnesAddressSpace and add labels to just THAT.
    public abstract class LabelProviderBase
    {
        [CanBeNull] public abstract IAnnotationLabel GetLabel(int snesAddress);
        
        public string GetLabelName(int snesAddress) => 
            GetLabel(snesAddress)?.Name ?? "";

        public string GetLabelComment(int snesAddress) => 
            GetLabel(snesAddress)?.Comment ?? "";
    }
    
    public class LabelsServiceWithTemp : LabelProviderBase, ILabelServiceWithTempLabels, IEquatable<LabelsServiceWithTemp>
    {
        public LabelsServiceWithTemp(Data data)
        {
            Data = data;
            
            #if DIZ_3_BRANCH
            NormalProvider = new ByteSourceLabelProvider(data.SnesAddressSpace);
            #else
            NormalProvider = new LabelsCollection();
            #endif
            
            TemporaryProvider = new LabelsCollection();
        }
        
        [XmlIgnore] 
        public Data Data { get; }

        private ILabelService NormalProvider { get; }
        
        [XmlIgnore] 
        private ILabelService TemporaryProvider { get; }
        
        
        // this isn't bulletproof, but the best we can do for now.
        // better to replace this with observable collections or something later.
        public event EventHandler OnLabelChanged;

        // returns both real and temporary labels
        IEnumerable<KeyValuePair<int, IAnnotationLabel>> IReadOnlyLabelProvider.Labels => Labels;

        public void AddTemporaryLabel(int snesAddress, IAnnotationLabel label)
        {
            if (NormalProvider.GetLabel(snesAddress) == null && TemporaryProvider.GetLabel(snesAddress) == null)
                TemporaryProvider.AddLabel(snesAddress, label);
        }

        public void ClearTemporaryLabels()
        {
            TemporaryProvider.DeleteAllLabels();
        }

        // probably a very expensive method, use sparingly
        // returns both real and temporary labels
        //
        // this method is unordered
        public IEnumerable<KeyValuePair<int, IAnnotationLabel>> Labels => 
            NormalProvider.Labels.Concat(TemporaryProvider.Labels);

        [CanBeNull]
        public override IAnnotationLabel GetLabel(int snesAddress)
        {
            var normalExisting = NormalProvider.GetLabel(snesAddress);
            return normalExisting ?? TemporaryProvider.GetLabel(snesAddress);
        }

        public void DeleteAllLabels()
        {
            NormalProvider.DeleteAllLabels();
            TemporaryProvider.DeleteAllLabels();
            
            OnLabelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveLabel(int snesAddress)
        {
            // we should only operate on real labels here. ignore temporary labels
            
            NormalProvider.RemoveLabel(snesAddress);
            
            OnLabelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddLabel(int snesAddress, IAnnotationLabel labelToAdd, bool overwrite = false)
        {
            // we should only operate on real labels here. ignore temporary labels.
            // explicitly use AddTemporaryLabel() for temp stuff.
            
            NormalProvider.AddLabel(snesAddress, labelToAdd, overwrite);
            
            OnLabelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetAll(Dictionary<int, IAnnotationLabel> newLabels)
        {
            ClearTemporaryLabels();
            NormalProvider.SetAll(newLabels);
            
            OnLabelChanged?.Invoke(this, EventArgs.Empty);
        }
        
        #region "Equality"
        public bool Equals(LabelsServiceWithTemp other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Labels.SequenceEqual(other.Labels); // expensive, allocates memory for copy. probably ok though.
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LabelsServiceWithTemp)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Data != null ? Data.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (NormalProvider != null ? NormalProvider.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (TemporaryProvider != null ? TemporaryProvider.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(LabelsServiceWithTemp left, LabelsServiceWithTemp right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LabelsServiceWithTemp left, LabelsServiceWithTemp right)
        {
            return !Equals(left, right);
        }
        #endregion
    }

    #if DIZ_3_BRANCH
    public class ByteSourceLabelProvider : LabelProviderBase, ILabelService, IEquatable<ByteSourceLabelProvider>
    {
        public bool Equals(ByteSourceLabelProvider other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ReferenceEquals(ByteSource, other.ByteSource);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ByteSourceLabelProvider)obj);
        }

        public override int GetHashCode()
        {
            return ByteSource.GetHashCode();
        }

        public static bool operator ==(ByteSourceLabelProvider left, ByteSourceLabelProvider right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ByteSourceLabelProvider left, ByteSourceLabelProvider right)
        {
            return !Equals(left, right);
        }

        private ByteSource ByteSource { get; }
        
        // pass in topleve (i.e. Data.SnesAddressSpace)
        public ByteSourceLabelProvider(ByteSource byteSource)
        {
            ByteSource = byteSource;
        }

        public IEnumerable<KeyValuePair<int, IAnnotationLabel>> Labels => 
            ByteSource.GetAnnotationsIncludingChildrenEnumerator<IAnnotationLabel>();

        public static bool IsLabel(Annotation annotation) => annotation.GetType() == typeof(Label);
        
        public void DeleteAllLabels()
        {
            ByteSource.RemoveAllAnnotations(IsLabel);
        }

        public void RemoveLabel(int snesAddress)
        {
            ByteSource.RemoveAllAnnotationsAt(snesAddress, IsLabel);
        }

        public void SetAll(Dictionary<int, IAnnotationLabel> newLabels)
        {
            DeleteAllLabels();
            foreach (var (key, value) in newLabels)
            {
                AddLabel(key, value);
            }
        }

        public override Label? GetLabel(int snesAddress) => ByteSource.GetOneAnnotation<Label>(snesAddress);

        public void AddLabel(int snesAddress, IAnnotationLabel labelToAdd, bool overwrite = false)
        {
            Debug.Assert(labelToAdd != null);
            
            if (overwrite)
                RemoveLabel(snesAddress);

            var existing = ByteSource.GetOneAnnotation<IAnnotationLabel>(snesAddress);
            
            if (existing == null)
                ByteSource.AddAnnotation(snesAddress, labelToAdd);
        }
    } 
    #endif
    
    public class LabelsCollection : LabelProviderBase, ILabelService, IEquatable<LabelsCollection>
    {
        // ReSharper disable once MemberCanBePrivate.Global
        public Dictionary<int, IAnnotationLabel> Labels { get; } = new();
        
        [XmlIgnore]
        IEnumerable<KeyValuePair<int, IAnnotationLabel>> IReadOnlyLabelProvider.Labels => Labels;

        public void AddLabel(int snesAddress, IAnnotationLabel labelToAdd, bool overwrite = false)
        {
            Debug.Assert(labelToAdd != null);
            
            if (overwrite)
                RemoveLabel(snesAddress);

            var existing = Labels.ContainsKey(snesAddress);

            if (!existing)
                Labels.Add(snesAddress, labelToAdd);
        }

        public void DeleteAllLabels()
        {
            Labels.Clear();
        }
        
        public void RemoveLabel(int snesAddress)
        {
            Labels.Remove(snesAddress);
        }

        public void SetAll(Dictionary<int, IAnnotationLabel> newLabels)
        {
            DeleteAllLabels();
            foreach (var key in newLabels.Keys)
            {
                Labels.Add(key, newLabels[key]);
            }
        }

        [CanBeNull] public override IAnnotationLabel GetLabel(int snesAddress) => 
            Labels.TryGetValue(snesAddress, out var label) ? label : null;
        
        #region "Equality"
        public bool Equals(LabelsCollection other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Labels, other.Labels);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LabelsCollection)obj);
        }

        public override int GetHashCode()
        {
            return (Labels != null ? Labels.GetHashCode() : 0);
        }

        public static bool operator ==(LabelsCollection left, LabelsCollection right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LabelsCollection left, LabelsCollection right)
        {
            return !Equals(left, right);
        }
        #endregion
    }
}