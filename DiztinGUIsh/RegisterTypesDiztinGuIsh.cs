using Diz.Controllers.interfaces;
using Diz.Core;
using Diz.Core.util;
using DiztinGUIsh.util;
using DiztinGUIsh.window.dialog;
using JetBrains.Annotations;
using LightInject;

namespace DiztinGUIsh
{
    public static class RegisterTypesDiztinGuIsh
    {
        // example for scanning manually in an assembly. caution: can be slow
        // var rootNamespacesToInclude = new List<Assembly>
        // {
        //     typeof(RegisterTypesDiztinGuIsh).Assembly,      // DiztinGUIsh.dll
        //     typeof(IDizService).Assembly,                   // Diz.Core.dll
        //     typeof(ILogCreatorForGenerator).Assembly,       // Diz.LogWriter.dll
        //     typeof(IController).Assembly,                   // Diz.Controllers.dll
        // };
        //
        // var ourAssemblies = AppDomain.CurrentDomain.GetAssemblies()
        //     .Where(assembly => rootNamespacesToInclude.Contains(assembly));
        //
        // RegisterTypesInLoadedAssemblies(ourAssemblies);
        //
        // private static void RegisterTypesInLoadedAssemblies(IEnumerable<Assembly> ourAssemblies)
        // {
        //     var timing = Stopwatch.StartNew();
        //
        //     Trace.WriteLine("Type registration: Starting loaded assembly scan");
        //     foreach (var assemblyToScan in ourAssemblies)
        //     {
        //         Trace.WriteLine($"Type registration: Scanning {assemblyToScan.GetName()}");
        //         Service.Container.RegisterAssembly(assemblyToScan, ShouldRegister);
        //     }
        //     Trace.WriteLine($"Type registration: Done assembly scan ({timing.Elapsed.TotalSeconds:0.00} seconds)" );
        // }

        // private static bool ShouldRegister(Type interfaceType, Type implementingType)
        // {
        //     if (!implementingType.IsClass || interfaceType.IsClass)
        //         return false;
        //
        //     if (!implementingType.GetInterfaces().Contains(interfaceType))
        //         return false;
        //
        //     // example filter: only allow things that implement IDizService 
        //     // if (!implementingType.GetInterfaces().Contains(typeof(IDizService)))
        //     //    return false;
        //
        //     Trace.WriteLine($"Registering type: {implementingType.Name} for {interfaceType.Name} [found in '{implementingType.Assembly.GetName().Name}'");
        //     return true;
        // }
    }
    
    [UsedImplicitly] public class DizUiCompositionRoot : ICompositionRoot
    {
        public void Compose(IServiceRegistry serviceRegistry)
        {
            serviceRegistry.Register<IDizApplication, DizApplication>(new PerContainerLifetime());
            serviceRegistry.Register<IProgressView, ProgressDialog>();
            serviceRegistry.Register(
                typeof(IDataSubsetRomByteDataGridLoader<,>), 
                typeof(DataSubsetRomByteDataGridLoader<,>)
                );
        }
    }
}