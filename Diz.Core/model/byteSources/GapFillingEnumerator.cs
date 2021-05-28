using System.Collections.Generic;
using System.Diagnostics;

namespace Diz.Core.model.byteSources
{
    public class GapFillingEnumerator<T> : IEnumerator<T>
    {
        public IShouldReallyBeAListButIAmLazy<T> Collection { get; protected set; }
        public int Position { get; set; } = -1;

        public GapFillingEnumerator(IShouldReallyBeAListButIAmLazy<T> collection)
        {
            Debug.Assert(collection != null);
            Collection = collection;
        }
        public bool MoveNext()
        {
            Position++;
            return Position < Collection.Count;
        }

        public void Reset()
        {
            Position = -1;
        }

        T IEnumerator<T>.Current => Collection[Position];
        public object Current => Collection[Position];
        public void Dispose()
        {
            Position = -1;
            Collection = null;
        }
    }
}