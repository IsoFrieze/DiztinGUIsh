using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Diz.Core.export;
using Diz.Core.model.snes;
using Diz.Core.util;

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
    public class LabelProvider : IReadOnlyLabelProvider, ITemporaryLabelProvider
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
        IEnumerable<KeyValuePair<int, IReadOnlyLabel>> IReadOnlyLabelProvider.Labels => 
            Labels.Select(l => new KeyValuePair<int, IReadOnlyLabel>(l.Key, l.Value));

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
        
        public void ImportLabelsFromCsv(string importFilename, bool replaceAll, ref int errLine)
        {
            var labelsFromCsv = ReadLabelsFromCsv(importFilename, ref errLine);
            
            if (replaceAll)
                DeleteAllLabels();
            
            foreach (var (key, value) in labelsFromCsv)
            {
                AddLabel(key, value, true);
            }
        }
        
        private static Dictionary<int, Label> ReadLabelsFromCsv(string importFilename, ref int errLine)
        {
            var newValues = new Dictionary<int, Label>();
            var lines = Util.ReadLines(importFilename).ToArray();

            var validLabelChars = new Regex(@"^([a-zA-Z0-9_\-]*)$");

            // NOTE: this is kind of a risky way to parse CSV files, won't deal with weirdness in the comments
            // section. replace with something better
            for (var i = 0; i < lines.Length; i++)
            {
                var label = new Label();

                errLine = i + 1;

                Util.SplitOnFirstComma(lines[i], out var labelAddress, out var remainder);
                Util.SplitOnFirstComma(remainder, out var labelName, out var labelComment);

                label.Name = labelName.Trim();
                label.Comment = labelComment;

                if (!validLabelChars.Match(label.Name).Success)
                    throw new InvalidDataException("invalid label name: " + label.Name);

                newValues.Add(int.Parse(labelAddress, NumberStyles.HexNumber, null), label);
            }

            errLine = -1;
            return newValues;
        }
    }

    public class NormalLabelProvider
    {
        private Data Data { get; }
        
        public NormalLabelProvider(Data data)
        {
            Data = data;
        }

        public IEnumerable<KeyValuePair<int, Label>> Labels => Data.SnesAddressSpace.GetAnnotationEnumerator<Label>();

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