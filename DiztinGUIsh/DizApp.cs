using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using DiztinGUIsh.util;

namespace DiztinGUIsh;

public class DizApp : IDizApp
{
    private readonly IViewFactory viewFactory;
    public DizApp(IViewFactory viewFactory)
    {
        this.viewFactory = viewFactory;
    }

    public void Run(string initialProjectFileToOpen = "")
    {
        // TODO: do less weird janky casting here.
        
        GuiUtil.SetupDpiStuff();
        var mainWindow = viewFactory.GetMainGridWindowView();

        // TODO: fix
        // if (!string.IsNullOrEmpty(initialProjectFileToOpen))
        //      mainWindow.ProjectController.OpenProject(initialProjectFileToOpen);

        Application.Run(mainWindow as Form);
    }
}