using System.Windows.Forms;
using Diz.Controllers.interfaces;
using Diz.Ui.Winforms.util;
using DiztinGUIsh.util;

namespace DiztinGUIsh.window.dialog;

public class CommonGui : ICommonGui
{
    public bool PromptToConfirmAction(string msg) => 
        GuiUtil.PromptToConfirmAction("Warning", msg, () => true);

    public void ShowError(string msg) => 
        MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    
    public void ShowWarning(string msg) => 
        MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    
    public void ShowMessage(string msg) => 
        MessageBox.Show(msg, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
}