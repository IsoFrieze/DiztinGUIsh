using Diz.Core;
using Diz.Core.commands;
using Diz.Core.model;

namespace DiztinGUIsh.controller
{
    public interface IController
    {
        
    }

    public interface IMarkManyController : IController
    {
        IDataRange DataRange { get; }
        IReadOnlySnesRomBase Data { get; }
        MarkCommand GetMarkCommand();
    }
    
    public interface IViewer {}
    
    public interface IModalDialog
    {
        /// <summary>
        /// Show the dialog to the user and wait for them to complete
        /// the steps on the view
        /// </summary>
        /// <returns>True if steps were completed and we have a valid result</returns>
        bool PromptDialog();
    }
    
    public interface IMarkManyView : IViewer, IModalDialog
    {
        int Property { get; set; }
        int Column { set; } // TODO: make enum with different types supported.
        IMarkManyController Controller { get; set; }
        object GetFinalValue();
    }
}