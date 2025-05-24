#nullable enable

using System;
using Diz.App.Common;

namespace Diz.App.Winforms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var serviceFactory = DizWinformsRegisterServices.CreateServiceFactoryAndRegisterTypes();
        DizAppCommon.StartApp(serviceFactory, args);
    }
}