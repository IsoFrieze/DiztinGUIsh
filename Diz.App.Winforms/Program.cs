#nullable enable

using System;
using Diz.Controllers.interfaces;
using LightInject;

namespace Diz.App.Winforms;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var serviceFactory = DizWinformsRegisterServices.CreateServiceFactoryAndRegisterTypes();
        
        var dizApp = serviceFactory.GetInstance<IDizApp>();
        
        var openFile = "";
        if (args.Length > 0)
            openFile = args[0];
        
        dizApp.Run(openFile);
    }
}