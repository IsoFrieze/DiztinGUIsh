using System.ComponentModel;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.LogWriter;
using Eto.Drawing;
using Eto.Forms;
using Label = Eto.Forms.Label;

namespace Diz.Ui.Eto.ui;

public class SampleMenuItem : Command
{
    public SampleMenuItem()
    {
        MenuText = "C&lick Me, Command";
        ToolBarText = "Click Me";
        ToolTip = "This shows a dialog for no reason";
        //Image = Icon.FromResource ("MyResourceName.ico");
        //Image = Bitmap.FromResource ("MyResourceName.png");
        Shortcut = Application.Instance.CommonModifier | Keys.M;  // control+M or cmd+M
    }

    protected override void OnExecuted(EventArgs e)
    {
        base.OnExecuted(e);
        MessageBox.Show(Application.Instance.MainForm, "You clicked me!", "Tutorial 2", MessageBoxButtons.OK);
    }
}

public class EtoMainGridForm : Form, IMainGridWindowView
{
    public event EventHandler? OnFormClosed;

    private readonly IDizDocument document;
    private readonly IDizAppSettings appSettings;
    private readonly IViewFactory viewFactory;
    private readonly IProjectController projectController;


    public EtoMainGridForm(
        IProjectController projectController,
        IDizAppSettings appSettings,
        IDizDocument document,
        IViewFactory viewFactory)
    {
        CreateGui();

        this.document = document;
        this.appSettings = appSettings;
        this.viewFactory = viewFactory;
        this.projectController = projectController;
        this.projectController.ProjectView = this;

        // TODO
        // aliasList = viewFactory.GetLabelEditorView();
        // aliasList.ProjectController = this.projectController;

        this.document.PropertyChanged += Document_PropertyChanged;
        this.projectController.ProjectChanged += ProjectController_ProjectChanged;

        // NavigationForm = new NavigationForm // TODO
        // {
        //     Document = this.document,
        //     SnesNavigation = this,
        // };

        Closed += (sender, args) => OnFormClosed?.Invoke(sender, args);
    }

    private void ProjectController_ProjectChanged(object sender, IProjectController.ProjectChangedEventArgs e)
    {
    }

    private void Document_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    private void CreateMenu()
    {
        Menu = new MenuBar
        {
            Items =
            {
                new ButtonMenuItem
                {
                    Text = "&File",
                    Items =
                    {
                        // you can add commands or menu items
                        new SampleMenuItem(),
                        // another menu item, not based off a Command
                        new ButtonMenuItem { Text = "Click Me, MenuItem" }
                    }
                }
            }
        };
    }
    
    private GridView gridView;
    
    private void CreateGui()
    {
        Title = "Diz";
        CreateMenu();

        // Create the grid view
        gridView = new GridView
        {
            AllowMultipleSelection = false,
            ShowHeader = true,
        };

        // Add columns to the grid
        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "Label",
            Width = 200,
            DataCell = new TextBoxCell("Label")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "PC",
            Width = 58,
            DataCell = new TextBoxCell("PC")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "@",
            Width = 26,
            DataCell = new TextBoxCell("Char")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "#",
            Width = 26,
            DataCell = new TextBoxCell("Hex")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "<*>",
            Width = 34,
            DataCell = new TextBoxCell("Points")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "Instruction",
            Width = 125,
            DataCell = new TextBoxCell("Instruction")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "IA",
            Width = 58,
            DataCell = new TextBoxCell("IA")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "Flag",
            Width = 86,
            DataCell = new TextBoxCell("Flag")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "B",
            Width = 26,
            DataCell = new TextBoxCell("DB")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "D",
            Width = 42,
            DataCell = new TextBoxCell("DP")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "M",
            Width = 26,
            DataCell = new TextBoxCell("M")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "X",
            Width = 26,
            DataCell = new TextBoxCell("X")
        });

        gridView.Columns.Add(new GridColumn
        {
            HeaderText = "Comment",
            Expand = true, // Auto-size remaining space
            DataCell = new TextBoxCell("Comment")
        });
        
        // needed to set the initial size of the grid or things seem to get weird.
        gridView.Size = new Size(1000, 600);

        var bottomFooter = new Label
        {
            Text = "[% Complete etc TODO]",
            Size = new Size { Height = 20 }
        };
        
        var layout = new DynamicLayout();
        layout.BeginVertical(yscale: true);
        layout.AddRow (gridView);
        layout.EndVertical ();
        
        layout.BeginVertical ();
        layout.AddRow (bottomFooter);
        layout.EndVertical ();
        
        // Apply the container as the content of the form
        Content = layout;
        
        // hacky nonsense: need to show the form, set the data, then invalidate, or we get weird rendering bugs in
        // Winforms with black unpainted grid cells etc
        Show();
        gridView.DataStore = GetGridData();
        Invalidate(true);
    }

    private IEnumerable<object> GetGridData()
    {
        return new List<object>
        {
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
            new { Label = "Item1", PC = "0x001", Char = "@", Hex = "0xFF", Points = "10", Instruction = "NOP", IA = "Yes", Flag = "OK", DB = "12", DP = "34", M = "Yes", X = "No", Comment = "Test Row 1" },
            new { Label = "Item2", PC = "0x002", Char = "&", Hex = "0xFA", Points = "15", Instruction = "ADD", IA = "No", Flag = "WARN", DB = "34", DP = "12", M = "No", X = "Yes", Comment = "Test Row 2" },
        };
    }


    public ILongRunningTaskHandler.LongRunningTaskHandler TaskHandler =>
        ProgressBarJob.RunAndWaitForCompletion;

    public void SelectOffset(int pcOffset, ISnesNavigation.HistoryArgs? historyArgs = null)
    {
    }

    public void SelectOffsetWithOvershoot(int pcOffset, int overshootAmount = 0)
    {
    }

    public Project Project { get; set; }

    public void OnProjectOpenFail(string errorMsg)
    {
    }

    public void OnProjectSaved()
    {
    }

    public void OnExportFinished(LogCreatorOutput.OutputResult result)
    {
    }

    public string AskToSelectNewRomFilename(string promptSubject, string promptText)
    {
        return "";
    }

    public void OnProjectOpenWarnings(IEnumerable<string> warnings)
    {
    }

    public void BringFormToTop() => Focus();
}