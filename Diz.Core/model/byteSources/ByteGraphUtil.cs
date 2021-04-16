namespace Diz.Core.model.byteSources
{
    public static class ByteGraphUtil
    {
        // special function. attempts to recurse the graph and collapse all the nodes into
        // one ByteEntry which represents all of the information about our offset.
        //
        // this works in simple cases where you'd only expect i.e. one offset, or bytes are ok 
        // to be overwritten.  This doesn't work for more complex stuff like mirrored offsets/etc.
        // In those cases, you should more manually walk the graph node yourself in whatever manner
        // is appropriate for the calling code.
        public static ByteEntry BuildFlatDataFrom(ByteSource byteSource, int index)
        {
            if (!byteSource.IsValidIndex(index))
                return null;

            var node = BuildFullGraph(byteSource, index);
            var finalData = BuildFlatDataFrom(node);

            return finalData;
        }

        // Simplified graph traversal utility.
        //
        // after graph traversal has happened, collapse the graph
        // (of which this node is the root node) into one object.
        //
        // Annotations will be combined together into one list.
        // If there are multiple 'byte' at different children, then we'll pick the most recent one.
        //
        // If you need anything more advanced than this, parse it yourself.
        public static ByteEntry BuildFlatDataFrom(ByteGraphNode byteGraphNode)
        {
            return CreateByteEntryByFlatteningGraph(byteGraphNode, null);
        }
        
        private static ByteEntry CreateByteEntryByFlatteningGraph(
            ByteGraphNode byteGraphNode, ByteEntry dataBeingConstructed = null
        )
        {
            byteGraphNode.Validate();
            EnsureRootEntryExists();
            PopulateFromChildNodes(); // use child data first
            PopulateFromRootNode(); // override/append our data second as the priority
            return dataBeingConstructed;

            void EnsureRootEntryExists()
            {
                dataBeingConstructed ??= new ByteEntry()
                {
                    ByteStorageContainer = byteGraphNode.ByteSource.Bytes,
                    ContainerOffset = byteGraphNode.ByteData?.ContainerOffset ?? byteGraphNode.SourceIndex,
                };
            }

            void PopulateFromChildNodes()
            {
                // traverse any child nodes first.
                if (byteGraphNode.Children == null)
                    return;

                foreach (var childNode in byteGraphNode.Children)
                {
                    CreateByteEntryByFlatteningGraph(childNode, dataBeingConstructed);
                }
            }

            void PopulateFromRootNode()
            {
                // annotations are concatenated together
                if (byteGraphNode.ByteData?.Annotations?.Count > 0)
                    dataBeingConstructed.GetOrCreateAnnotationsList().AddRange(byteGraphNode.ByteData.Annotations);

                // only change the byte if we're non-null and overriding something underneath.
                // we hide any bytes lower in the graph than us.
                if (byteGraphNode.ByteData?.Byte != null)
                    dataBeingConstructed.Byte = byteGraphNode.ByteData.Byte;
            }
        }


        // return a directed graph with all possible values for this offset including all child regions
        public static ByteGraphNode BuildFullGraph(ByteSource byteSource, int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= byteSource.Bytes.Count)
                return null;

            var node = new ByteGraphNode(byteSource, sourceIndex);

            BuildChildGraph(byteSource, sourceIndex, ref node);

            return node;
        }

        // caution: recursion
        private static void BuildChildGraph(ByteSource byteSource, int sourceIndex, ref ByteGraphNode nodeToPopulate)
        {
            foreach (var childSourceToTraverse in byteSource.ChildSources)
            {
                var childIndex = childSourceToTraverse.RegionMapping
                    .ConvertIndexFromParentToChild(sourceIndex, childSourceToTraverse.ByteSource);

                var newChildNode = BuildFullGraph(childSourceToTraverse.ByteSource, childIndex);
                if (newChildNode == null)
                    continue;

                nodeToPopulate.AttachChildNode(newChildNode);
            }
        }
    }
}