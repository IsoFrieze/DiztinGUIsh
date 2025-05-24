// using System;
// using System.Diagnostics;
// using Diz.Controllers.interfaces;
//
// namespace Diz.Controllers.controllers
// {
//     public class StartFormController : IStartFormController
//     {
//         public IStartFormViewer View { get; }
//
//
//         public StartFormController(IStartFormViewer view)
//         {
//             Debug.Assert(view != null);
//             View = view;
//             View.Closed += ViewOnClosed;
//         }
//
//         public void Show() => View.Show();
//         
//         [Obsolete]
//         public event EventHandler Closed;
//         
//         private void ViewOnClosed(object sender, EventArgs e) => Closed?.Invoke(this, e);
//     }
// }
