using System.Collections.Generic;

namespace Diz.Core.model.byteSources
{
    // represent a node of a per-byte graph through the mappings of various ByteSources
    public class ByteOffsetDataNode
    {
        public ByteOffsetData Data { get; set; }
        public List<ByteOffsetDataNode> Children { get; set; }

        // Simplified graph traversal utility.
        //
        // after graph traversal has happened, collapse the graph (of which this node is the root node) into
        // one ByteOffsetData object.
        //
        // Annotations will be combined together into one list.
        // If there are multiple 'byte' at different children, then we'll pick the most recent one.
        //
        // If you need anything more advanced than this, parse it yourself.
        public ByteOffsetData ResolveToOne(ByteOffsetData dataBeingConstructed = null)
        {
            dataBeingConstructed ??= new ByteOffsetData
            {
                ContainerOffset = Data.ContainerOffset,
                Container = Data.Container,
            };

            // traverse any child nodes first.
            if (Children != null)
            {
                foreach (var childNode in Children)
                {
                    childNode.ResolveToOne(dataBeingConstructed);
                }
            }

            // now, add in any of our own changes/overrides AFTER children populated.
            
            // annotations are concatenated together
            if (Data.Annotations != null && Data.Annotations.Count > 0)
                dataBeingConstructed.GetOrCreateAnnotationsList().AddRange(Data.Annotations);
            
            // only change the byte if we're non-null and overriding something underneath
            if (Data.Byte != null)
                dataBeingConstructed.Byte = Data.Byte;

            return dataBeingConstructed;
        }
    }
}