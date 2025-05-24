using Diz.App.Common;

namespace Diz.App.Eto;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var serviceFactory = DizEtoRegisterServices.CreateServiceFactoryAndRegisterTypes();
        DizAppCommon.StartApp(serviceFactory, args);
    }
}