using Diz.Core.model;

namespace Diz.Core.export
{
    public interface ILogCreatorDataSource : IReadOnlySnesRom
    {
        ITemporaryLabelProvider TemporaryLabelProvider { get; }
    }
    
    // would love to redesign so we can get rid of this class and all this temporary label stuff.
    public interface ITemporaryLabelProvider
    {
        // add a temporary label which will be cleared out when we are finished the export
        // this should not add a label if a real label already exists.
        public void AddTemporaryLabel(int snesAddress, Label label);
        public void ClearTemporaryLabels();
    }
    
    public interface ILabelProvider : ITemporaryLabelProvider, IReadOnlyLabelProvider
    {
        void AddLabel(int snesAddress, Label label, bool overwrite = false);
        void DeleteAllLabels();
        
        // if any labels exist at this address, remove them
        void RemoveLabel(int snesAddress);
    }
}