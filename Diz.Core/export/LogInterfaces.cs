using Diz.Core.model;

namespace Diz.Core.export
{
    public interface ILogCreatorDataSource : IReadOnlySnesRom
    {
        ITemporaryLabelProvider TemporaryLabelProvider { get; }
    }
    
    public interface ITemporaryLabelProvider
    {
        // add a temporary label which will be cleared out when we are finished the export
        // this should not add a label if a real label already exists.
        public void AddTemporaryLabel(int address, Label label);
        public void ClearTemporaryLabels();
    }
}