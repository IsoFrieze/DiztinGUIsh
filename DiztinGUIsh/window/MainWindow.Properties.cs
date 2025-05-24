using System.ComponentModel;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Ui.Winforms.dialogs;

namespace DiztinGUIsh.window;

public partial class MainWindow
{
    // maybe rethink how document and project are interacted with.
    private IDizDocument Document { get; }
    
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Project Project
    {
        get => Document.Project;
        set => Document.Project = value;
    }

    // not sure if this will be the final place this lives. OK for now. -Dom
    public IProjectController ProjectController { get; }

    public ILongRunningTaskHandler.LongRunningTaskHandler TaskHandler =>
        ProgressBarJob.RunAndWaitForCompletion;
        
    // sub windows
    private ILabelEditorView aliasList;
    private VisualizerForm visualForm;

    // TODO: add a handler so we get notified when CurrentViewOffset changes.
    // then, we split most of our functions up into
    // 1. things that change ViewOffset
    // 2. things that react to ViewOffset changes.
    //
    // This will allow more flexibility and synchronizing different views (i.e. main table, graphics, layout, etc)
    // and this lets us save this value with the project file itself.

    // Data offset of the "view" i.e. the top of the table
    private int ViewOffset
    {
        get => Project?.CurrentViewOffset ?? 0;
        set => Project.CurrentViewOffset = value;
    }

    private bool importerMenuItemsEnabled;

    private Util.NumberBase displayBase = Util.NumberBase.Hexadecimal;
    private FlagType markFlag = FlagType.Data8Bit;
}