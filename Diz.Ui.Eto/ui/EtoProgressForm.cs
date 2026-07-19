using Diz.Controllers.interfaces;
using Eto.Drawing;
using Eto.Forms;

namespace Diz.Ui.Eto.ui;

public class EtoProgressForm : Dialog, IProgressView
{
    public event EventHandler? OnFormClosed;
    private Label mainText;

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
        // todo? needed? (Eto is slated for removal in new-ui plan step 8; not wired up.)
    }

    public void BringFormToTop()
    {
        Focus();
    }

    public void Report(int value)
    {
        // TODO: update text
        mainText.Text = $"Progress: {value}%";
    }

    public bool IsMarquee { get; set; }
    public required string TextOverride { get; set; }

    // new-ui plan step 6: IProgressView is now Close() (non-modal), not PromptDialog/IsVisible/
    // SignalJobIsDone. Close() is satisfied by the inherited Eto Dialog.Close(). Eto's
    // TaskHandler is null anyway, so this view is never actually shown at runtime.
}