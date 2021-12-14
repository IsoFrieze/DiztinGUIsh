using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Diz.Core.export;
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
    public class LabelProvider : ILabelProvider
    {
        public LabelProvider(Data data)
        {
            Data = data;
            #if DIZ_3_BRANCH
            NormalProvider = new NormalLabelProvider(data);
            #endif
            NormalProvider = new TemporaryLabelProvider();
            TemporaryProvider = new TemporaryLabelProvider();
        }
        
        public Data Data { get; }

        private TemporaryLabelProvider NormalProvider { get; }
        private TemporaryLabelProvider TemporaryProvider { get; }


        // returns both real and temporary labels
        IEnumerable<KeyValuePair<int, Label>> IReadOnlyLabelProvider.Labels => Labels;

        public void AddTemporaryLabel(int snesAddress, Label label)
        {
            if (NormalProvider.GetLabel(snesAddress) == null && TemporaryProvider.GetLabel(snesAddress) == null)
                TemporaryProvider.AddLabel(snesAddress, label);
        }

        public void ClearTemporaryLabels()
        {
            TemporaryProvider.ClearTemporaryLabels();
        }

        // probably a very expensive method, use sparingly
        // returns both real and temporary labels
        //
        // this method is unordered
        public IEnumerable<KeyValuePair<int, Label>> Labels => 
            NormalProvider.Labels.Concat(TemporaryProvider.Labels);

        public Label GetLabel(int snesAddress)
        {
            var normalExisting = NormalProvider.GetLabel(snesAddress);
            return normalExisting ?? TemporaryProvider.GetLabel(snesAddress);
        }

        public string GetLabelName(int snesAddress)
        {
            var label = GetLabel(snesAddress);
            return label?.Name ?? "";
        }
        
        public string GetLabelComment(int snesAddress)
        {
            var label = GetLabel(snesAddress);
            return label?.Comment ?? "";
        }

        public void DeleteAllLabels()
        {
            NormalProvider.DeleteAllLabels();
            TemporaryProvider.ClearTemporaryLabels();
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
    public class NormalLabelProvider
    {
        private Data Data { get; }
        
        public NormalLabelProvider(Data data)
        {
            Data = data;
        }

        public IEnumerable<KeyValuePair<int, Label>> Labels => 
            Data.SnesAddressSpace.GetAnnotationsIncludingChildrenEnumerator<Label>();

        public static bool IsLabel(Annotation annotation) => annotation.GetType() == typeof(Label);
        
        public void DeleteAllLabels()
        {
            Data.SnesAddressSpace.RemoveAllAnnotations(IsLabel);
        }

        public void RemoveLabel(int snesAddress)
        {
            Data.SnesAddressSpace.RemoveAllAnnotationsAt(snesAddress, IsLabel);
        }
        
        public Label GetLabel(int snesAddress) => Data.SnesAddressSpace.GetOneAnnotation<Label>(snesAddress);

        public void AddLabel(int snesAddress, Label labelToAdd, bool overwrite)
        {
            Debug.Assert(labelToAdd != null);
            
            if (overwrite)
                RemoveLabel(snesAddress);

            var existing = Data.SnesAddressSpace.GetOneAnnotation<Label>(snesAddress);
            
            if (existing == null)
                Data.SnesAddressSpace.AddAnnotation(snesAddress, labelToAdd);
        }
    } 
    #endif
    
    public class TemporaryLabelProvider
    {
        private Dictionary<int, Label> TempLabels { get; } = new();  // NEVER serialize
        
        public IEnumerable<KeyValuePair<int, Label>> Labels => TempLabels;
        
        public void AddLabel(int snesAddress, Label labelToAdd, bool overwrite = false)
        {
            Debug.Assert(labelToAdd != null);
            
            if (overwrite)
                RemoveLabel(snesAddress);

            var existing = TempLabels.ContainsKey(snesAddress);

            if (!existing)
                TempLabels.Add(snesAddress, labelToAdd);
        }

        public void ClearTemporaryLabels()
        {
            TempLabels.Clear();
        }
        
        public void DeleteAllLabels()
        {
            ClearTemporaryLabels();
        }
        
        public bool RemoveLabel(int snesAddress)
        {
            return TempLabels.Remove(snesAddress);
        }
        
        public Label GetLabel(int snesAddress) => 
            TempLabels.TryGetValue(snesAddress, out var label) ? label : null;
    }
}