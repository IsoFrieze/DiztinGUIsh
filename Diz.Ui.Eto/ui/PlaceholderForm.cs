using Eto.Drawing;
using Eto.Forms;

namespace Diz.Ui.Eto.ui;

public class PlaceholderForm : Form
{
    public PlaceholderForm ()
    {
        Title = "Diz";
        ClientSize = new Size(800, 600);
        Content = new Label { Text = "Placeholder form - TODO: IMPLEMENT ME" };
    }
}