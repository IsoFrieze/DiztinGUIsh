﻿using System;
using System.ComponentModel;
using Diz.Core.datasubset;
using Diz.Core.model;
// using Diz.Core.model.byteSources;
using Diz.Core.model.snes;

namespace Diz.Controllers.interfaces
{
    public interface IDizApplication
    {
        public class Args
        {
            public string FileToOpen { get; set; }
        }
        
        void Run(Args args);
    }
    
    public interface IGridRow<out TItem>
    {
        Data Data { get; init; }
        TItem Item { get; }
    }
 
#if DIZ_3_BRANCH
    public interface IDataGridRow : IGridRow<ByteEntry>, INotifyPropertyChanged
    {
        
    }

    public interface IDataSubsetRomByteDataGridLoader<TRow, TItem> : IDataSubsetLoader<TRow, TItem>
    {
        // probably this needs to be refactored away, this exists for dependency injection resolution only
        // across the Controller and Gui layer
        
        public IBytesGridViewer<TItem> View { get; set; }
        public Data Data { get; set; }
    }
#endif
    
    public interface IProjectsManager : IProjects, IProjectLoadListener, ISampleProjectLoader, ILastProjectLoaded { }

    public interface IProjectLoadListener
    {
        public event EventHandler<Project> OnProjectOpened;
        void OpenProjectFile(string filename);
    }

    public interface IProjects
    {
        Project GetProject(string filename);
    }

    public interface ISampleProjectLoader
    {
        Project GetSampleProject();
    }
    
    public interface ILastProjectLoaded
    {
        Project GetLastOpenedProject();
        void OpenLastLoadedProject();
    }
}