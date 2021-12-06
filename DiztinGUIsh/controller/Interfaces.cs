using System;
using System.Collections.Generic;
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
        public Dictionary<MarkCommand.MarkManyProperty, object> Settings { get; set; }
    }
    
    public interface IViewer {}
    
    public interface ICloseable
    {
        void Close();
    }
    
    public interface IModalDialog
    {
        /// <summary>
        /// Show the dialog to the user and wait for them to complete
        /// the steps on the view
        /// </summary>
        /// <returns>True if steps were completed and we have a valid result</returns>
        bool PromptDialog();
    }

    public interface IProgressView : ICloseable, IModalDialog, IProgress<int> {
        public bool IsMarquee { get; set; }
        public string TextOverride { get; set; }
        bool Visible { get; set; }
        
        /// <summary>
        /// Signal that a job (potentially running in another task/thread) has completed.
        /// CAUTION: Implementers should use thread-safety measures, this may be called
        /// from a different thread than any other calls 
        /// </summary>
        void SignalJobIsDone();
    }
    
    public interface IMarkManyView : IViewer, IModalDialog
    {
        MarkCommand.MarkManyProperty Property { get; set; }
        object GetPropertyValue();
        IMarkManyController Controller { get; set; }

        void AttemptSetSettings(Dictionary<MarkCommand.MarkManyProperty, object> settings);
        Dictionary<MarkCommand.MarkManyProperty, object> SaveCurrentSettings();
    }
}