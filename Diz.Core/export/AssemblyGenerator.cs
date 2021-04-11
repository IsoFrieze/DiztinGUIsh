using System.Diagnostics;
using System.IO;

namespace Diz.Core.export
{
    public abstract class AssemblyPartialLineGenerator
    {
        public LogCreator LogCreator { get; protected internal set; }
        public ILogCreatorDataSource Data => LogCreator.Data;
        
        public string Token { get; protected init; } = "";
        public int DefaultLength { get; protected init; }
        
        public bool RequiresToken { get; protected init; } = true;
        public bool UsesOffset { get; protected init; } = true;

        public string Emit(int? offset, int length)
        {
            var finalLength = length;
            Prep(offset, ref finalLength);

            if (UsesOffset)
            {
                Debug.Assert(offset != null);
                return Generate(offset.Value, finalLength);
            }

            return Generate(finalLength);
        }

        protected virtual string Generate(int length)
        {
            throw new InvalidDataException("Invalid Generate() call: Can't call without an offset.");
        }
        
        protected virtual string Generate(int offset, int length)
        {
            // NOTE: if you get here (without an override in a derived class)
            // it means the client code should have instead been calling the other Generate(length) overload
            // directly. for now, we'll gracefully handle it, but client code should try and be better about it
            // eventually.
            
            return Generate(length);
            // throw new InvalidDataException("Invalid Generate() call: Can't call with offset.");
        }
        
        // call Prep() before doing anything in each Emit()
        // if length is non-zero, use that as our length, if not we use the default length
        protected virtual void Prep(int? offset, ref int length)
        {
            if (length == 0 && DefaultLength == 0)
                throw new InvalidDataException("Assembly output component needed a length but received none.");
            
            // set the length
            length = length != 0 ? length : DefaultLength;
            
            if (RequiresToken && string.IsNullOrEmpty(Token))
                throw new InvalidDataException("Assembly output component needed a token but received none.");

            // we should throw exceptions both ways, for now though we'll let it slide if we were passed in
            // an offset and we don't need it.
            var hasOffset = offset != null;
            if (UsesOffset && UsesOffset != hasOffset)
                throw new InvalidDataException(UsesOffset 
                    ? "Assembly output component needed an offset but received none."
                    : "Assembly output component doesn't use an offset but we were provided one anyway.");
        }
    }
}