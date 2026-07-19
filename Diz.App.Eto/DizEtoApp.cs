using System.Diagnostics;
using Diz.Controllers.interfaces;
using Diz.Ui.Eto.ui;
using Eto.Forms;

namespace Diz.App.Eto;

public class DizEtoApp(IViewFactory viewFactory) : IDizApp
{
    public void Run(string initialProjectFileToOpen = "")
    {
        var application = new Application();
        
        var mainWindow = viewFactory.GetMainGridWindowView();

        // janky casting
        // ReSharper disable once SuspiciousTypeConversion.Global
        var window = mainWindow as EtoMainGridForm;
        Debug.Assert(window != null);
        
        if (initialProjectFileToOpen != "") {
            // Eto's TaskHandler is null, so OpenProjectAsync runs inline/synchronously (no
            // message pump needed); block here at the app-startup edge. (Eto is deprecated;
            // new-ui plan step 8.)
            window.ProjectController.OpenProjectAsync(initialProjectFileToOpen).GetAwaiter().GetResult();
        }

        application.Run(window);
    }
}