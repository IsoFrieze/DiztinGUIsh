using Diz.Core.util;

namespace Diz.Core.model
{
    public static class DataExtensions
    {
        #region UnsafeCompatabilityHelpers
        
        // older interface never had to worry about null. new interface now we do. 
        // these are unsafe helper methods for code not yet using the new interface.
        // new code shouldn't use these if possible, and older code should migrate to checking for null directly.
        
        public static byte GetRomByteUnsafe(this IReadOnlyByteSource data, int offset)
        {
            // ReSharper disable once PossibleInvalidOperationException
            return (byte)data.GetRomByte(offset);
        }
        
        public static int GetRomWordUnsafe(this IReadOnlyByteSource data, int offset)
        {
            // ReSharper disable once PossibleInvalidOperationException
            return (int)data.GetRomWord(offset);
        }
        
        public static int GetRomLongUnsafe(this IReadOnlyByteSource data, int offset)
        {
            // ReSharper disable once PossibleInvalidOperationException
            return (int)data.GetRomLong(offset);
        }

        public static int GetRomDoubleWordUnsafe(this IReadOnlyByteSource data, int offset)
        {
            // ReSharper disable once PossibleInvalidOperationException
            return (int)data.GetRomDoubleWord(offset);
        }
        #endregion
    }
}