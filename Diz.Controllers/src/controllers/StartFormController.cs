using System;
using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.util;
using LightInject;

namespace Diz.Controllers.controllers
{
    public class StartFormController : IStartFormController
    {
        private IFormViewer view;

        public IFormViewer FormView
        {
            get => view;
            set
            {
                view = value;
                view.Closed += ViewOnClosed;
            }
        }

        IViewer IController.View => FormView;

        private void ViewOnClosed(object sender, EventArgs e) => Closed?.Invoke(this, e);

        public event EventHandler Closed;

        public void OpenFileWithNewView(string filename)
        {
            var app = Service.Container.GetInstance<IDizApplication>();
            app.OpenProjectFileWithNewView(filename);
        }

        public void OpenNewViewOfLastLoadedProject()
        {
            var app = Service.Container.GetInstance<IDizApplication>();
            app.OpenNewViewOfLastLoadedProject();
        }
    }
}
