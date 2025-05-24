using Diz.Controllers.interfaces;
using LightInject;

namespace Diz.App.Common;

public static class DizAppCommon
{
    public static void StartApp(IServiceFactory serviceFactory, string[] args)
    {
        // platform-independent app startup 
        var dizApp = serviceFactory.GetInstance<IDizApp>();
        
        var fileToOpen = "";
        if (args.Length > 0)
            fileToOpen = args[0];
        
        dizApp.Run(fileToOpen);
    }
}