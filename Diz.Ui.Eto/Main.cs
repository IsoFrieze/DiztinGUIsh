using System;
using Eto.Drawing;
using Eto.Forms;

namespace Diz.Ui.Eto;

public class AssemblyGridForm : Form
{
    public AssemblyGridForm ()
    {
        Title = "Diz";
        ClientSize = new Size(800, 600);
        Content = new Label { Text = "Welcome to DIZ" };
    }
}