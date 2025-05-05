using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Controllers.util;
using Diz.LogWriter;
using Diz.Ui.Winforms.dialogs;

namespace DiztinGUIsh.window;

public partial class MainWindow : Form, IMainGridWindowView
{
    public MainWindow(
        IProjectController projectController,
        IDizAppSettings appSettings, 
        IDizDocument document)
    {
        Document = document;
        this.appSettings = appSettings;
        ProjectController = projectController;
        ProjectController.ProjectView = this;

        aliasList = projectController.ViewFactory.GetLabelEditorView();
        aliasList.ProjectController = ProjectController;
            
        Document.PropertyChanged += Document_PropertyChanged;
        ProjectController.ProjectChanged += ProjectController_ProjectChanged;

        NavigationForm = new NavigationForm
        {
            Document = Document,
            SnesNavigation = this,
        };

        InitializeComponent();
    }
    
    
    [AttributeUsage(AttributeTargets.Method)]
    public class MenuItemAttribute(string menu, string name, Keys shortcutKeys = Keys.None, bool visible = true) : Attribute
    {
        public string Name { get; } = name;
        public string Menu { get; } = menu;
        public Keys ShortcutKeys { get; } = shortcutKeys;
        public bool Visible { get; } = visible;
    }


    void AddDynamicMenuItems()
    {
        // a lot of the Diz UI is hardcoded in the visual studio designer.
        // we want to move away from that and have the menu items dynamically populate
        // so we don't need a UI designer to add simple UI elements.
        // this is the first attempt at that.  we should migrate more of the hardcoded designer stuff into here
        // example
        
        // Use reflection to find methods in this class with the MenuItemAttribute
        var methodsWithMenuItems = this.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic) // include non-public methods
            .Where(m => m.GetCustomAttribute<MenuItemAttribute>() != null); // only methods with the MenuItemAttribute attached

        foreach (var method in methodsWithMenuItems)
        {
            // add each menu item found to the correct dropdown menu
            
            var attribute = method.GetCustomAttribute<MenuItemAttribute>();
            if (attribute == null || attribute.Visible == false) 
                continue;
            
            var targetMenu = menuStrip1.Items
                .OfType<ToolStripMenuItem>() // Cast menu items to ToolStripMenuItem
                .FirstOrDefault(menuItem =>
                    string.Equals(menuItem.Text?.Replace("&", string.Empty) ?? "", attribute.Menu, StringComparison.OrdinalIgnoreCase)
                );

            if (targetMenu == null)
            {
                // TODO: could also dynamically add a new menu here
                continue;
            }

            var newMenuItem = new ToolStripMenuItem
            {
                Size = new System.Drawing.Size(253, 22),
                Name = method.Name,
                ShortcutKeys = attribute.ShortcutKeys,
                Text = attribute.Name,
            };
            
            var callbackMethod = (Action)Delegate.CreateDelegate(typeof(Action), this, method);
            newMenuItem.Click += (_, _) => callbackMethod();
            
            targetMenu.DropDownItems.Add(newMenuItem);
        }
    }

    private void Init()
    {
        AddDynamicMenuItems();
        
        InitMainTable();

        UpdatePanels();
        UpdateUiFromSettings();

        if (appSettings.OpenLastFileAutomatically)
            OpenLastProject();
    }


    private void Document_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DizDocument.LastProjectFilename))
        {
            UpdateUiFromSettings();
        }
    }

    private void ProjectController_ProjectChanged(object sender, IProjectController.ProjectChangedEventArgs e)
    {
        switch (e.ChangeType)
        {
            case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Saved:
                OnProjectSaved();
                break;
            case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Opened:
                OnProjectOpened(e.Filename);
                break;
            case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Imported:
                OnImportedProjectSuccess();
                break;
            case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Closing:
                OnProjectClosing();
                break;
        }

        RebindProject();
    }

    private void OnProjectClosing()
    {
        CloseAndDisposeVisualizer();
        
        UpdateSaveOptionStates(saveEnabled: false, saveAsEnabled: false, closeEnabled: false);
    }

    public void OnProjectOpened(string filename)
    {
        // TODO: do this with aliaslist too.
        CloseAndDisposeVisualizer();

        UpdateSaveOptionStates(saveEnabled: true, saveAsEnabled: true, closeEnabled: true);
        RefreshUi();

        Document.LastProjectFilename = filename; // do this last.
    }

    private void CloseAndDisposeVisualizer()
    {
        visualForm?.Close();
        visualForm?.Dispose();
        visualForm = null;
    }

    public void OnProjectOpenFail(string errorMsg)
    {
        Document.LastProjectFilename = "";
        ShowError(errorMsg, "Error opening project");
    }

    public void OnProjectSaved()
    {
        UpdateSaveOptionStates(saveEnabled: true, saveAsEnabled: true, closeEnabled: true);
        UpdateWindowTitle();
    }

    public void OnExportFinished(LogCreatorOutput.OutputResult result)
    {
        ShowExportResults(result);
    }

    private void RememberNavigationPoint(int pcOffset, ISnesNavigation.HistoryArgs historyArgs)
    {
        var snesAddress = Project.Data.ConvertPCtoSnes(pcOffset);
        var history = Document.NavigationHistory;
            
        // if our last remembered offset IS the new offset, don't record it again
        // (prevents duplication)
        if (history.Count > 0 && history[history.Count-1].SnesOffset == snesAddress)
            return;

        history.Add(
            new NavigationEntry(
                snesAddress, 
                historyArgs,
                Project.Data
            )
        );
    }

    private void timer1_Tick(object sender, System.EventArgs e)
    {
        // the point of this timer is to throttle the ROM% calculator
        // since it is an expensive calculation. letting it happen attached to UI events
        // would significantly slow the user down.
        //
        // TODO: this is the kind of thing that Rx.net's Throttle function, or 
        // an async task would handle much better. For now, this is fine.
        UpdatePercentageCalculatorCooldown();
    }

    private void UpdatePercentageCalculatorCooldown()
    {
        if (_cooldownForPercentUpdate == -1)
            return;

        if (--_cooldownForPercentUpdate == -1)
            UpdatePercent(forceRecalculate: true);
    }
}