using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh.window2
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
