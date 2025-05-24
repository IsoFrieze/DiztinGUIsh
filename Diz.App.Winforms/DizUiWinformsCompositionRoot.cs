using Diz.Controllers.interfaces;
using Diz.Ui.Winforms;
using Diz.Ui.Winforms.dialogs;
using JetBrains.Annotations;
using LightInject;

namespace DiztinGUIsh;

[UsedImplicitly] public class DizUiWinformsCompositionRoot : ICompositionRoot
{
    public void Compose(IServiceRegistry serviceRegistry)
    {
        serviceRegistry.Register<IDizApp, DizWinformsApp>();
        serviceRegistry.Register<ICommonGui, WinFormsCommonGui>();

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