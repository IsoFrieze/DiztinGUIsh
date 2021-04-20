using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace Diz.Core.model.byteSources
{
    // represent a node of a per-byte graph through the mappings of various ByteSources
    public class ByteGraphNode
    {
        [NotNull] public ByteSource ByteSource { get; }
        public int SourceIndex { get; }
        [CanBeNull] public IReadOnlyList<ByteGraphNode> Children => children;
        
        [CanBeNull] public ByteEntry ByteEntry => ByteSource.Bytes[SourceIndex];


        [CanBeNull] private List<ByteGraphNode> children;

        public ByteGraphNode(ByteSource byteSource, int sourceIndex)
        {
            ByteSource = byteSource;
            SourceIndex = sourceIndex;
            
            Validate();
        }
        
        // public ByteGraphNode(ByteEntry byteData) 
        //     : this(byteData?.ParentByteSource, byteData?.ParentIndex ?? -1)
        // {
        //     Validate();
        // }

        internal void Validate()
        {
            if (ByteSource == null)
                throw new InvalidDataException("No valid ByteSource set");

            if (!ByteSource.IsValidIndex(SourceIndex))
                throw new IndexOutOfRangeException("Invalid ByteSource index");
        }

        public void AttachChildNode(ByteGraphNode newChildNode)
        {
            children ??= new List<ByteGraphNode>();
            children.Add(newChildNode);
        }
    }
}