using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Controllers.controllers
{
    /// <summary>
    /// Loads and caches Project objects, and stores references to the last loaded project.
    /// </summary>
    public class ProjectsManager : IProjectsManager
    {
        private readonly IProjectLoader projectLoader;

        public Dictionary<string, Project> LoadedProjects { get; } = new();
        
        // this needs some rework, it's used all over the place in odd ways.
        public static string LastOpenedProjectFilename { get; set; }

        public ProjectsManager(IProjectLoader projectLoader)
        {
            this.projectLoader = projectLoader;
        }

        /// <summary>
        /// Get a loaded project associated with the filename
        /// If it's already loaded, we'll return a copy of the loaded project. This may
        /// be shared with already opened views.  If it's not loaded, load from disk.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public Project GetProject(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;
            
            if (LoadedProjects.ContainsKey(filename))
                return LoadedProjects[filename];
            
            var project = projectLoader.LoadProject(filename);
            if (project == null)
                return null;
            
            LoadedProjects.Add(filename, project);
            return project;
        }

        /// <summary>
        /// Get a sample project, loaded from hardcoded data inside the app itself. Suitable for testing/demo purposes
        /// and doesn't need to read anything from the disk.
        /// </summary>
        /// <returns>Demo project with demo ROM loaded</returns>
        public Project GetSampleProject() => 
            GetProject(ProjectLoaderWithSampleDataDecorator.MagicSampleProjectName);

        public Project GetLastOpenedProject()
        {
            // this is kinda messy/dumb, rethink.
            Debug.Assert(LoadedProjects.Values.Count == 1);
            return LoadedProjects.Values.Select(project => project).FirstOrDefault();
        }
        
        public void OpenProjectFile(string filename)
        {
            if (filename == null)
                return;
            
            AfterProjectOpenAttempt(filename, GetProject(filename));
        }

        public void OpenLastLoadedProject() => 
            AfterProjectOpenAttempt("", GetLastOpenedProject());

        private void AfterProjectOpenAttempt(string filenameAttempted, Project loadedProject)
        {
            if (loadedProject == null)
            {
                var filenameInfo = !string.IsNullOrEmpty(filenameAttempted)
                    ? $" (filename={filenameAttempted})"
                    : "";
                
                throw new InvalidDataException($"Failed to open project{filenameInfo}");
            }
            
            OnProjectOpened?.Invoke(this, loadedProject);
        }
        
        public event EventHandler<Project> OnProjectOpened;
    }

    /// <summary>
    /// Interface to Load Project objects
    /// </summary>
    public interface IProjectLoader
    {
        public Project LoadProject(string filename);
    }

    /// <summary>
    /// A loader that reads from disk
    /// </summary>
    public class ProjectFileLoader : IProjectLoader
    {
        public IProjectOpenerHandler ProjectOpenerHandler { get; init; }

        public Project LoadProject(string filename)
        {
            return ProjectOpenerHandler?.OpenProject(filename, showPopupAlertOnLoaded: false);   
        }
    }
    
    /// <summary>
    /// A decorator for IProjectLoader that returns a project created from our internal sample data
    /// (instead of data from disk). Pass in the special string constant here to use.
    /// </summary>
    public class ProjectLoaderWithSampleDataDecorator : IProjectLoader
    {
        // pass in this magic string to load the "sample data" project.
        // great for testing and for showing sample stuff in the app.
        // 
        // ReSharper disable once MemberCanBeProtected.Global
        public const string MagicSampleProjectName = "sampleproject111111112";
        
        private readonly IProjectLoader previous;

        public ProjectLoaderWithSampleDataDecorator(IProjectLoader previous)
        {
            Debug.Assert(previous != null);
            this.previous = previous;
        } 

        public Project LoadProject(string filename)
        {
            return filename != MagicSampleProjectName 
                ? previous.LoadProject(filename)
                : CreateNewSampleProject();
        }

        private static Project CreateNewSampleProject()
        {
            var project = new Project {
                Data = SampleRomData.CreateSampleData().Data,
            };

            project.Session = new ProjectSession(project) {
                ProjectFileName = MagicSampleProjectName,
            };
            
            return project;
        }
    }
}