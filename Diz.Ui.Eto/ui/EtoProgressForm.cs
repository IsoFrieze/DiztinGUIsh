using Diz.Controllers.interfaces;
using Eto.Drawing;
using Eto.Forms;

namespace Diz.Ui.Eto.ui;

public class EtoProgressForm : Dialog, IProgressView
{
    public event EventHandler? OnFormClosed;
    private Label mainText = null!;

    public EtoProgressForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Title = "Progress Update";
        ClientSize = new Size(400, 600);
        mainText = new Label { Text = "Please Wait" };
        Content = mainText;
    }

    public void Show()
    {
        // todo? needed?
    }

    public void BringFormToTop()
    {
        Focus();
    }

    public bool PromptDialog()
    {
        Show(); // unsure of equivalent in Eto.
        // probably should make this class derive from Dialog<bool> instead and rework stuff for that.
        return true;
    }

    public void Report(int value)
    {
        // TODO: update text
        mainText.Text = $"Progress: {value}%";
    }

    public bool IsMarquee { get; set; }
    public required string TextOverride { get; set; }
    public bool IsVisible() => 
        Application.Instance.Invoke(() => Visible);

    public void SignalJobIsDone() => 
        Application.Instance.Invoke(Close);
}