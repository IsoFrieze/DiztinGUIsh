using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Diz.Core.export;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;

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
        public abstract Label GetLabel(int snesAddress);
        
        public string GetLabelName(int snesAddress) => 
            GetLabel(snesAddress)?.Name ?? "";

        public string GetLabelComment(int snesAddress) => 
            GetLabel(snesAddress)?.Comment ?? "";
    }
    
    public class LabelProvider : LabelProviderBase, ILabelServiceWithTempLabels
    {
        public LabelProvider(Data data)
        {
            Data = data;
            
            #if DIZ_3_BRANCH
            NormalProvider = new ByteSourceLabelProvider(data.SnesAddressSpace);
            #else
            NormalProvider = new TemporaryLabelProvider();
            #endif
            
            TemporaryProvider = new TemporaryLabelProvider();
        }
        
        public Data Data { get; }

        private ILabelService NormalProvider { get; }
        private ILabelService TemporaryProvider { get; }


        // returns both real and temporary labels
        IEnumerable<KeyValuePair<int, Label>> IReadOnlyLabelProvider.Labels => Labels;

        public void AddTemporaryLabel(int snesAddress, Label label)
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
        public IEnumerable<KeyValuePair<int, Label>> Labels => 
            NormalProvider.Labels.Concat(TemporaryProvider.Labels);

        public override Label GetLabel(int snesAddress)
        {
            var normalExisting = NormalProvider.GetLabel(snesAddress);
            return normalExisting ?? TemporaryProvider.GetLabel(snesAddress);
        }

        public void DeleteAllLabels()
        {
            NormalProvider.DeleteAllLabels();
            TemporaryProvider.DeleteAllLabels();
        }

        public void RemoveLabel(int snesAddress)
        {
            // we should only operate on real labels here. ignore temporary labels
            
            NormalProvider.RemoveLabel(snesAddress);
        }

        public void AddLabel(int snesAddress, Label labelToAdd, bool overwrite = false)
        {
            // we should only operate on real labels here. ignore temporary labels.
            // explicitly use AddTemporaryLabel() for temp stuff.
            
            NormalProvider.AddLabel(snesAddress, labelToAdd, overwrite);
        }
    }

    #if DIZ_3_BRANCH
    public class ByteSourceLabelProvider : LabelProviderBase, ILabelService
    {
        private ByteSource ByteSource { get; }
        
        // pass in topleve (i.e. Data.SnesAddressSpace)
        public ByteSourceLabelProvider(ByteSource byteSource)
        {
            ByteSource = byteSource;
        }

        public IEnumerable<KeyValuePair<int, Label>> Labels => 
            ByteSource.GetAnnotationsIncludingChildrenEnumerator<Label>();

        public static bool IsLabel(Annotation annotation) => annotation.GetType() == typeof(Label);
        
        public void DeleteAllLabels()
        {
            ByteSource.RemoveAllAnnotations(IsLabel);
        }

        public void RemoveLabel(int snesAddress)
        {
            ByteSource.RemoveAllAnnotationsAt(snesAddress, IsLabel);
        }
        
        public override Label GetLabel(int snesAddress) => ByteSource.GetOneAnnotation<Label>(snesAddress);

        public void AddLabel(int snesAddress, Label labelToAdd, bool overwrite)
        {
            Debug.Assert(labelToAdd != null);
            
            if (overwrite)
                RemoveLabel(snesAddress);

            var existing = ByteSource.GetOneAnnotation<Label>(snesAddress);
            
            if (existing == null)
                ByteSource.AddAnnotation(snesAddress, labelToAdd);
        }
    } 
    #endif
    
    public class TemporaryLabelProvider : LabelProviderBase, ILabelService
    {
        private readonly Dictionary<int, Label> labels = new();
        public IEnumerable<KeyValuePair<int, Label>> Labels => labels;

        public void AddLabel(int snesAddress, Label labelToAdd, bool overwrite = false)
        {
            Debug.Assert(labelToAdd != null);
            
            if (overwrite)
                RemoveLabel(snesAddress);

            var existing = labels.ContainsKey(snesAddress);

            if (!existing)
                labels.Add(snesAddress, labelToAdd);
        }

        public void DeleteAllLabels()
        {
            labels.Clear();
        }
        
        public void RemoveLabel(int snesAddress)
        {
            labels.Remove(snesAddress);
        }
        
        public override Label GetLabel(int snesAddress) => 
            labels.TryGetValue(snesAddress, out var label) ? label : null;
    }
}