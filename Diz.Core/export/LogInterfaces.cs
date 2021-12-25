using Diz.Core.Interfaces;
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
        public void AddTemporaryLabel(int snesAddress, IAnnotationLabel label);
        public void ClearTemporaryLabels();
    }

    public interface ILabelService : 
        ILabelProvider,
        IReadOnlyLabelProvider
    {
        
    }
    
    public interface ILabelServiceWithTempLabels : 
        ILabelService,
        ITemporaryLabelProvider
    {
        
    }
}