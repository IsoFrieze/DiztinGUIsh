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
        var openFile = "";
        if (args.Length > 0)
            openFile = args[0];

        var serviceFactory = DizAppServices.CreateServiceFactoryAndRegisterTypes();

        serviceFactory.GetInstance<IDizApp>().Run(openFile);
    }
}