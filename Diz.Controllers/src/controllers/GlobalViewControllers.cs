using System;
using System.Collections.Generic;
using System.IO;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.util;
using LightInject;

namespace Diz.Controllers.controllers
{
    public interface IControllersManager : IProjectOpenRequester, IRootControllerProvider
    {
        public event EventHandler AllFormsClosed;
    }

    public interface IRootControllerProvider
    {
        void ShowNewStartForm();
        void ShowNewProjectEditorForm(Project project);
    }
    
    public class ControllersManager : IControllersManager
    {
        public List<IFormController> Controllers { get; } = new();
        public event EventHandler AllFormsClosed;
        public event EventHandler<ProjectOpenEventArgs> ProjectOpenRequested;

        private void TrackFormController(IFormController controller)
        {
            Controllers.Add(controller);
            controller.Closed += OnControllerClosed;
        }

        private void OnControllerClosed(object sender, EventArgs e)
        {
            Controllers.RemoveAll(c => ReferenceEquals(c, sender));
            if (Controllers.Count == 0)
                AllFormsClosed?.Invoke(this, new EventArgs());
        }
        
        private void RegisterAndShowController(IFormController controller)
        {
            TrackFormController(controller);
            controller.Show();
        }
        
        #region Public GUI methods, for opening new types of forms etc
        
        public void ShowNewProjectEditorForm(Project project)
        { 
            if (project == null)
                throw new InvalidDataException("project must not be null");
            
            void BeforeFormShow(IMainFormController controller) => 
                controller.SetProject(project.Session?.ProjectFileName ?? "", project);
            
            ShowNewProjectEditorForm(BeforeFormShow);
        }

        public void ShowNewStartForm()
        {
            var controller = Service.Container.GetInstance<IStartFormController>();
            
            // controller.View.ProjectOpenRequested += (sender, e) => ProjectOpenRequested?.Invoke(sender, e);
            if (ProjectOpenRequested != null)
                controller.View.ProjectOpenRequested += ProjectOpenRequested;
            
            RegisterAndShowController(controller);
        }

        public void ShowNewProjectEditorForm(Action<IMainFormController> beforeShow = null)
        {
            var controller = Service.Container.GetInstance<IMainFormController>();
            beforeShow?.Invoke(controller);
            RegisterAndShowController(controller);
        }
        
        #endregion
    }
}