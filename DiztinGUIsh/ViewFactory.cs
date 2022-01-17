using Diz.Controllers.interfaces;
using LightInject;

namespace DiztinGUIsh;

// public class ViewFactory : IViewFactory
// {
//     private readonly IServiceFactory serviceFactory;
//         
//     public ViewFactory(IServiceFactory serviceFactory) => 
//         this.serviceFactory = serviceFactory;
//         
//     public IFormViewer Get(string name)
//     {
//         return serviceFactory.GetInstance<IFormViewer>(name);
//     }
// }