using Diz.Controllers.interfaces;
using Diz.Controllers.services;
using Diz.Core.services;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Ui.Winforms;
using DiztinGUIsh.window;
using DiztinGUIsh.window.dialog;
using JetBrains.Annotations;
using LightInject;

namespace DiztinGUIsh;

public static class DizAppServices
{
    public static void RegisterDizServiceTypes()
    {
        Service.Container.RegisterFrom<DizCoreServicesCompositionRoot>();
        Service.Container.RegisterFrom<DizCpu65816ServiceRoot>();
        Service.Container.RegisterFrom<DizControllersCompositionRoot>();
        Service.Container.RegisterFrom<DizUiCompositionRoot>();
        Service.Container.RegisterFrom<DizWinformsCompositionRoot>();
    }
}

[UsedImplicitly] public class DizUiCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register<ICommonGui, CommonGui>();
        
        serviceRegistry.Register<IImportRomDialogView, ImportRomDialog>();
        serviceRegistry.Register<IProgressView, ProgressDialog>();
        serviceRegistry.Register<ILogCreatorSettingsEditorView, LogCreatorSettingsEditorForm>();
        serviceRegistry.Register<ILabelEditorView, AliasList>();

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