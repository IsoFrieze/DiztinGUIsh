using System.Diagnostics;
using Diz.Controllers.interfaces;
using Eto.Forms;

namespace Diz.App.Eto;

public class DizEtoApp(IViewFactory viewFactory) : IDizApp
{
    public void Run(string initialProjectFileToOpen = "")
    {
        var mainWindow = viewFactory.GetMainGridWindowView();

        // janky casting
        // ReSharper disable once SuspiciousTypeConversion.Global
        var window = mainWindow as Form;
        Debug.Assert(window != null);
        
        new Application().Run(window);
    }
}