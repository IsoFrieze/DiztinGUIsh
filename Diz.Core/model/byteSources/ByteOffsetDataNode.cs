using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using JetBrains.Annotations;

namespace Diz.Core.model.byteSources
{
    // represent a node of a per-byte graph through the mappings of various ByteSources
    public class ByteOffsetDataNode
    {
        [NotNull] public ByteSource ByteSource { get; }
        public int SourceIndex { get; }
        [CanBeNull] public IReadOnlyList<ByteOffsetDataNode> Children => children;
        
        [CanBeNull] public ByteOffsetData ByteData => ByteSource.Bytes[SourceIndex];


        [CanBeNull] private List<ByteOffsetDataNode> children;

        public ByteOffsetDataNode(ByteSource byteSource, int sourceIndex)
        {
            ByteSource = byteSource;
            SourceIndex = sourceIndex;
            
            Validate();
        }
        
        public ByteOffsetDataNode(IReadOnlyByteOffsetData byteData) 
            : this(byteData?.Container, byteData?.ContainerOffset ?? -1)
        {
            Validate();
        }

        private void Validate()
        {
            if (ByteSource == null)
                throw new InvalidDataException("No valid ByteSource set");

            if (!ByteSource.IsValidIndex(SourceIndex))
                throw new IndexOutOfRangeException("Invalid ByteSource index");
        }

        public void AttachChildNode(ByteOffsetDataNode newChildNode)
        {
            children ??= new List<ByteOffsetDataNode>();
            children.Add(newChildNode);
        }

        // Simplified graph traversal utility.
        //
        // after graph traversal has happened, collapse the graph (of which this node is the root node) into
        // one ByteOffsetData object.
        //
        // Annotations will be combined together into one list.
        // If there are multiple 'byte' at different children, then we'll pick the most recent one.
        //
        // If you need anything more advanced than this, parse it yourself.
        public ByteOffsetData CreateByteByFlatteningGraph(ByteOffsetData dataBeingConstructed = null)
        {
            Validate();

            dataBeingConstructed ??= new ByteOffsetData
            {
                ByteStorageContainer = ByteSource.Bytes,
                ContainerOffset = ByteData?.ContainerOffset ?? SourceIndex,
            };

            // traverse any child nodes first.
            if (Children != null)
            {
                foreach (var childNode in Children)
                {
                    childNode.CreateByteByFlatteningGraph(dataBeingConstructed);
                }
            }

            // now, add in any of our own changes/overrides AFTER children populated.
            //
            // remember: WE can be null even if lower nodes in the graph aren't.

            // annotations are concatenated together
            if (ByteData?.Annotations?.Count > 0)
                dataBeingConstructed.GetOrCreateAnnotationsList().AddRange(ByteData.Annotations);
        
            // only change the byte if we're non-null and overriding something underneath.
            // we hide any bytes lower in the graph than us.
            if (ByteData?.Byte != null)
                dataBeingConstructed.Byte = ByteData.Byte;

            return dataBeingConstructed;
        }
    }
}