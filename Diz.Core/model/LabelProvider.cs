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
            NormalProvider = new NormalLabelProvider(data);
            TemporaryProvider = new TemporaryLabelProvider(data);
        }
        
        public Data Data { get; }

        private NormalLabelProvider NormalProvider { get; }
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
            // we should only operate on real (not temporary) labels here
            
            NormalProvider.RemoveLabel(snesAddress);
        }

        public void AddLabel(int snesAddress, Label labelToAdd, bool overwrite = false)
        {
            // we should only operate on real (not temporary) labels here. use AddTemporaryLabel() for temp stuff.
            
            NormalProvider.AddLabel(snesAddress, labelToAdd, overwrite);
        }
    }

    public class NormalLabelProvider
    {
        private Data Data { get; }
        
        public NormalLabelProvider(Data data)
        {
            Data = data;
        }

        public IEnumerable<KeyValuePair<int, Label>> Labels => 
            Data.SnesAddressSpace.GetAnnotationsIncludingChildrenEnumerator<Label>();

        private static bool IsLabel(Annotation annotation) => annotation.GetType() == typeof(Label);
        
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
    
    public class TemporaryLabelProvider
    {
        private Data Data { get; }
        
        public TemporaryLabelProvider(Data data)
        {
            Data = data;
        }
        
        private Dictionary<int, Label> TempLabels { get; } = new();  // NEVER serialize
        
        public IEnumerable<KeyValuePair<int, Label>> Labels => TempLabels;
        
        public void AddLabel(int snesAddress, Label label)
        {
            if (TempLabels.ContainsKey(snesAddress))
                return;
            
            TempLabels.Add(snesAddress, label);
        }

        public void ClearTemporaryLabels()
        {
            TempLabels.Clear();
        }
        
        public Label GetLabel(int snesAddress) => TempLabels.TryGetValue(snesAddress, out var label) ? label : null;
    }
}