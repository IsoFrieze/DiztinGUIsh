using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Controllers.util;
using Diz.Core.services;
using Diz.Core.util;
using DiztinGUIsh.window;
using DiztinGUIsh.window.dialog;
using JetBrains.Annotations;
using LightInject;
using LightInject.AutoFactory;

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
        // serviceProvider.RegisterFrom<DizWinformsCompositionRoot>();
        
        // scan ourselves last
        serviceProvider.RegisterFrom<DizUiCompositionRoot>();
        
        return serviceProvider;
    }
}

[UsedImplicitly] public class DizUiCompositionRoot : ICompositionRoot
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