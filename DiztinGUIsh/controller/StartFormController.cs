using System;

namespace DiztinGUIsh.controller
{
    public class StartFormController : IFormController
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
            DizApplication.App.OpenProjectFileWithNewView(filename);
        }

        public void OpenNewViewOfLastLoadedProject()
        {
            DizApplication.App.OpenNewViewOfLastLoadedProject();
        }
    }
}
