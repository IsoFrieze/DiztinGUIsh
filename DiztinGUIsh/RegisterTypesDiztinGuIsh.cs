using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.model.byteSources;
using DiztinGUIsh.util;
using DiztinGUIsh.window;
using DiztinGUIsh.window.dialog;
using DiztinGUIsh.window.usercontrols;
using JetBrains.Annotations;
using LightInject;

namespace DiztinGUIsh
{
    [UsedImplicitly] public class DizUiCompositionRoot : ICompositionRoot
    {
        public void Compose(IServiceRegistry serviceRegistry)
        {
            serviceRegistry.RegisterSingleton<IDizApplication, DizApplication>();
            serviceRegistry.Register<IProgressView, ProgressDialog>();
            serviceRegistry.Register<IMarkManyView, MarkManyView>();
            serviceRegistry.Register(
                typeof(IDataSubsetRomByteDataGridLoader<,>), 
                typeof(DataSubsetRomByteDataGridLoader<,>)
                );

            serviceRegistry.Register<IBytesGridViewer<ByteEntry>, DataGridEditorControl>();
            serviceRegistry.Register<IDataGridEditorForm, DataGridEditorForm>();

            serviceRegistry.Register<IStartFormViewer, StartForm>("StartForm");
            
            serviceRegistry.Register<IDataGridEditorForm, DataGridEditorForm> ("DataGridForm");
        }
    }
}