using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Global

namespace Diz.Core.export
{
    public interface IAssemblyPartialGenerator
    {
        string Emit(int? offset, int? lengthOverride);
    }

    public interface ILogCreatorForGenerator
    { 
        public LogWriterSettings Settings { get; }
        ILogCreatorDataSource Data { get; }
        List<int> LabelsWeVisited { get; }
        public DataErrorChecking DataErrorChecking { get; }
        
        int GetLineByteLength(int offset);
        string GetFormattedBytes(int offset, int step, int bytes);
        string GeneratePointerStr(int offset, int bytes);
        string GetFormattedText(int offset, int bytes);
    }
    
    public abstract class AssemblyPartialLineGenerator : IAssemblyPartialGenerator
    {
        public ILogCreatorForGenerator LogCreator { get; set; }
        protected ILogCreatorDataSource Data => LogCreator.Data;
        
        public string Token { get; init; } = "";
        public int DefaultLength { get; init; }
        public bool RequiresToken { get; init; } = true;
        public bool UsesOffset { get; init; } = true;

        public string Emit(int? offset, int? lengthOverride)
        {
            var finalLength = lengthOverride ?? DefaultLength;
            
            Validate(offset, finalLength);

            if (!UsesOffset) 
                return Generate(finalLength);
            
            Debug.Assert(offset != null);
            return Generate(offset.Value, finalLength);
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
        
        // call Validate() before doing anything in each Emit()
        // if length is non-zero, use that as our length, if not we use the default length
        protected virtual void Validate(int? offset, int finalLength)
        {
            if (finalLength == 0)
                throw new InvalidDataException("Assembly output component needed a length but received none.");

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