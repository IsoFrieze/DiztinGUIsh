using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Ui.Winforms.dialogs;
using DiztinGUIsh.window;
using DiztinGUIsh.window.dialog;
using JetBrains.Annotations;
using LightInject;

namespace DiztinGUIsh;

[UsedImplicitly] public class DizUiWinformsCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register<IDizApp, DizApp>();
        serviceRegistry.Register<ICommonGui, CommonGui>();
        
        serviceRegistry.Register<IImportRomDialogView, ImportRomDialog>("ImportRomView");
        serviceRegistry.Register<IProgressView, ProgressDialog>("ProgressBarView");
        serviceRegistry.Register<ILogCreatorSettingsEditorView, LogCreatorSettingsEditorForm>("ExportDisassemblyView");
        serviceRegistry.Register<ILabelEditorView, AliasList>("LabelEditorView");
        serviceRegistry.Register<IMainGridWindowView, MainWindow>("MainGridWindowView");

        // the (string) names of the views above will be mapped to the method names of the interface below.
        // i.e. calling IViewFactory.GetLabelEditorView() will look for somthing named "LabelEditorView"
        serviceRegistry.EnableAutoFactories();
        serviceRegistry.RegisterAutoFactory<IViewFactory>();


        // coming soon. backported from upcoming 3.0 branch
        // serviceRegistry.RegisterSingleton<IDizApplication, DizApplication>();
        // serviceRegistry.Register<IMarkManyView, MarkManyView>();
        // serviceRegistry.Register(
        //     typeof(IDataSubsetRomByteDataGridLoader<,>), 
        //     typeof(DataSubsetRomByteDataGridLoader<,>)
        //     );
        //
        // serviceRegistry.Register<IBytesGridViewer<ByteEntry>, DataGridEditorControl>();
        // serviceRegistry.Register<IDataGridEditorForm, DataGridEditorForm>();
        //
        // serviceRegistry.Register<IStartFormViewer, StartForm>("StartForm");
        //
        // serviceRegistry.Register<IDataGridEditorForm, DataGridEditorForm> ("DataGridForm");
    }
}