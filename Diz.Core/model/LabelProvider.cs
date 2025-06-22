using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using Diz.Core.Interfaces;
#if DIZ_3_BRANCH
using Diz.Core.model.byteSources;
#endif
using Diz.Core.model.snes;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.model
{
    public class LabelsExporterCache : IExporterCache
    {
        private readonly Dictionary<int, IAnnotationLabel> exporterLabelsSubset;

        public LabelsExporterCache(IEnumerable<KeyValuePair<int, IAnnotationLabel>> allLabels)
        {
            exporterLabelsSubset = allLabels
                .Where(x=> 
                    // exclude +/- labels
                    !RomUtil.IsValidPlusMinusLabel(x.Value.Name) &&
                    
                    // exclude some auto-generated types of labels
                    !x.Value.Name.StartsWith("UNREACH_") && 
                    !x.Value.Name.StartsWith("CODE_") && 
                    !x.Value.Name.StartsWith("DATA_") &&
                    !x.Value.Name.StartsWith("DATA8_") &&
                    !x.Value.Name.StartsWith("DATA16_") &&
                    !x.Value.Name.StartsWith("DATA24_") &&
                    !x.Value.Name.StartsWith("LOOSE_OP_") &&
                    !x.Value.Name.StartsWith("TEXT_")
                    
                    // probably more are useful to add here...
                )

                .ToDictionary();
        }

        public (int labelAddress, IAnnotationLabel labelEntry) SearchOptimizedForMirroredLabel(int snesAddress)
        {
            if (exporterLabelsSubset == null)
                return (-1, null);

            // do this WITHOUT LINQ for optimization purposes
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var kvp in exporterLabelsSubset)
            {
                if (RomUtil.AreLabelsSameMirror(snesAddress, kvp.Key))
                {
                    return (kvp.Key, kvp.Value);
                }
            }

            return (-1, null);
        }
    }
    
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
        public abstract IAnnotationLabel GetLabel(int snesAddress);
        
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

        public void AddOrReplaceTemporaryLabel(int snesAddress, IAnnotationLabel label)
        {
            if (cachedLabels != null)
                throw new InvalidOperationException("Cannot modify labels while cache is locked");
            
            // never generate a label that overrides a real human-generated label that was created manually
            if (NormalProvider.GetLabel(snesAddress) != null)
                return;
            
            var existingLabel = TemporaryProvider.GetLabel(snesAddress);
            if (existingLabel == null)
            {
                TemporaryProvider.AddLabel(snesAddress, label);
            }
            else
            {
                existingLabel.Comment = label.Comment;
                existingLabel.Name = label.Name;
            }
        }

        public void ClearTemporaryLabels()
        {
            if (cachedLabels != null)
                throw new InvalidOperationException("Cannot modify labels while cache is locked");
            
            TemporaryProvider.DeleteAllLabels();
        }

        public void LockLabelsCache()
        {
            cachedLabels = ConcatNormalAndTempLabels().ToDictionary();
            exporterCache = new LabelsExporterCache(cachedLabels);
        }

        public void UnlockLabelsCache()
        {
            cachedLabels = null;
            exporterCache = null;
        }
        
        // performance only: ALL LABELS: cache of combined temp and real labels together,
        // so that requests don't have to ask for Concat() which is slow.
        [CanBeNull] private Dictionary<int, IAnnotationLabel> cachedLabels;
        // performance only: SUBSET of LABELS: this is mostly used for reducing the search space for
        // intense label searches for bank mirroring/etc, where complexity is O(N^2) that grows per-label.
        [CanBeNull] private IExporterCache exporterCache;

        // very expensive method, use sparingly
        // returns both real and temporary labels
        //
        // the result is unordered (despite the two sources being sorted dicts)
        public IEnumerable<KeyValuePair<int, IAnnotationLabel>> Labels => 
            cachedLabels ?? ConcatNormalAndTempLabels();

        private IEnumerable<KeyValuePair<int, IAnnotationLabel>> ConcatNormalAndTempLabels() => 
            NormalProvider.Labels.Concat(TemporaryProvider.Labels);

        public override IAnnotationLabel GetLabel(int snesAddress)
        {
            // if there's a real label (like, added in the Diz GUI), prefer that.
            // if there's not, use an auto-generated label if it exists
            var normalExisting = NormalProvider.GetLabel(snesAddress);
            return normalExisting ?? TemporaryProvider.GetLabel(snesAddress);
        }

        public IExporterCache ExporterCache => exporterCache;

        public void DeleteAllLabels()
        {
            if (cachedLabels != null)
                throw new InvalidOperationException("Cannot modify labels while cache is locked");
            
            NormalProvider.DeleteAllLabels();
            TemporaryProvider.DeleteAllLabels();
            
            OnLabelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveLabel(int snesAddress)
        {
            if (cachedLabels != null)
                throw new InvalidOperationException("Cannot modify labels while cache is locked");
            
            // we should only operate on real labels here. ignore temporary labels
            
            NormalProvider.RemoveLabel(snesAddress);
            
            OnLabelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddLabel(int snesAddress, IAnnotationLabel labelToAdd, bool overwrite = false)
        {
            if (cachedLabels != null)
                throw new InvalidOperationException("Cannot modify labels while cache is locked");
            
            // we should only operate on real labels here. ignore temporary labels.
            // explicitly use AddTemporaryLabel() for temp stuff.
            
            NormalProvider.AddLabel(snesAddress, labelToAdd, overwrite);
            
            OnLabelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetAll(Dictionary<int, IAnnotationLabel> newLabels)
        {
            if (cachedLabels != null)
                throw new InvalidOperationException("Cannot modify labels while cache is locked");
            
            ClearTemporaryLabels();
            NormalProvider.SetAll(newLabels);
            
            OnLabelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AppendLabels(Dictionary<int, IAnnotationLabel> newLabels, bool smartMerge = false)
        {
            if (cachedLabels != null)
                throw new InvalidOperationException("Cannot modify labels while cache is locked");
            
            NormalProvider.AppendLabels(newLabels, smartMerge);
            
            OnLabelChanged?.Invoke(this, EventArgs.Empty);
        }
        
        #region "Equality"
        public bool Equals(LabelsServiceWithTemp other)
        {
            if (ReferenceEquals(null, other)) return false;
            return ReferenceEquals(this, other) || Labels.SequenceEqual(other.Labels); // expensive, allocates memory for copy. probably ok though.
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((LabelsServiceWithTemp)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = NormalProvider != null ? NormalProvider.GetHashCode() : 0;
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

        public override Label GetLabel(int snesAddress) => ByteSource.GetOneAnnotation<Label>(snesAddress);

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
        public SortedDictionary<int, IAnnotationLabel> Labels { get; } = new();
        
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
            AppendLabels(newLabels);
        }

        public void AppendLabels(Dictionary<int, IAnnotationLabel> newLabels, bool smartMerge=false)
        {
            foreach (var snesAddress in newLabels.Keys)
            {
                if (!smartMerge) {
                    Labels.Add(snesAddress, newLabels[snesAddress]);
                    continue;
                }
                
                // smart merging: try something a little better to preserve/merge existing data
                if (!Labels.TryGetValue(snesAddress, out var label))
                {
                    // doesn't exist so just add normally
                    Labels.Add(snesAddress, newLabels[snesAddress]);
                }
                else
                {
                    // does exist, so let's more smartly merge the label content
                    // prefer incoming data, if it exists, to overwrite existing data
                    var newLabelName = newLabels[snesAddress].Name;
                    var newLabelComment = newLabels[snesAddress].Comment;
                    
                    label.Name = newLabelName == "" ? label.Name : newLabelName;
                    label.Comment = newLabelComment == "" ? label.Comment : newLabelComment;
                }
            }
        }

        public override IAnnotationLabel GetLabel(int snesAddress) => 
            Labels.TryGetValue(snesAddress, out var label) ? label : null;

        // non-temp label provider will never use this
        public IExporterCache ExporterCache => null;

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
            return Labels != null ? Labels.GetHashCode() : 0;
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