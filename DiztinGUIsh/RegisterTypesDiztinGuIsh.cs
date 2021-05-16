using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.model.byteSources;
using DiztinGUIsh.util;
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
            serviceRegistry.Register<IDizApplication, DizApplication>(new PerContainerLifetime());
            serviceRegistry.Register<IProgressView, ProgressDialog>();
            serviceRegistry.Register<IMarkManyView, MarkManyView>();
            serviceRegistry.Register(
                typeof(IDataSubsetRomByteDataGridLoader<,>), 
                typeof(DataSubsetRomByteDataGridLoader<,>)
                );

            serviceRegistry.Register<IBytesGridViewer<ByteEntry>, DataGridEditorControl>();
        }
    }
}