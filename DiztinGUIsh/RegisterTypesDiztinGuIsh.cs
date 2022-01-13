using Diz.Controllers.interfaces;
using Diz.Core.services;
using Diz.Core.util;
using DiztinGUIsh.window;
using DiztinGUIsh.window.dialog;
using JetBrains.Annotations;
using LightInject;

namespace DiztinGUIsh;

public static class DizAppServices
{
    public static IServiceFactory CreateServiceFactoryAndRegisterTypes()
    {
        var serviceProvider = DizServiceProvider.CreateServiceContainer();
        
        // register services in any Diz*dll's present
        DizCoreServicesDllRegistration.RegisterServicesInDizDlls(serviceProvider);
        
        // alternatively, we can be explicit like below, no DLL scanning required
        // serviceProvider.RegisterFrom<DizCoreServicesCompositionRoot>();
        // serviceProvider.RegisterFrom<DizCpu65816ServiceRoot>();
        // serviceProvider.RegisterFrom<DizControllersCompositionRoot>();
        // serviceProvider.RegisterFrom<DizUiCompositionRoot>();
        // serviceProvider.RegisterFrom<DizWinformsCompositionRoot>();
        
        return serviceProvider;
    }
}

[UsedImplicitly] public class DizUiCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register<ICommonGui, CommonGui>();
        
        serviceRegistry.Register<IImportRomDialogView, ImportRomDialog>();
        serviceRegistry.Register<IProgressView, ProgressDialog>("ProgressBarView");
        serviceRegistry.Register<ILogCreatorSettingsEditorView, LogCreatorSettingsEditorForm>();
        serviceRegistry.Register<ILabelEditorView, AliasList>();
        serviceRegistry.Register<IFormViewer, MainWindow>();
        
        serviceRegistry.Register<IDizApp, DizApp>();

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