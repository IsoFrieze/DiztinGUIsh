using Diz.Controllers.interfaces;
using Eto.Forms;

namespace Diz.Ui.Eto;

public class EtoCommonGui : ICommonGui
{
    public bool PromptToConfirmAction(string msg) => 
        MessageBox.Show(msg, "Confirm", MessageBoxButtons.OKCancel, MessageBoxType.Question) == DialogResult.Ok;

    public void ShowError(string msg) => 
        MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxType.Error);

    public void ShowWarning(string msg) => 
        MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxType.Warning);

    public void ShowMessage(string msg) => 
        MessageBox.Show(msg, "Info", MessageBoxButtons.OK);
}