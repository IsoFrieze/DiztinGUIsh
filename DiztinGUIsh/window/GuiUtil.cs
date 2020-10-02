using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiztinGUIsh.window
{
    public static class GuiUtil
    {
        public static void InvokeIfRequired(this ISynchronizeInvoke obj, MethodInvoker action)
        {
            if (obj.InvokeRequired)
            {
                var args = new object[0];
                obj.Invoke(action, args);
            }
            else
            {
                action();
            }
        }

        public static string PromptToSelectFile(string initialDirectory = null)
        {
            var open = new OpenFileDialog { InitialDirectory = initialDirectory };
            return open.ShowDialog() == DialogResult.OK ? open.FileName : null;
        }

        public static string AskIfWeShouldSelectFilename(string promptSubject, string promptText, Func<string> confirmAction)
        {
            var dialogResult = MessageBox.Show(promptText, promptSubject,
                MessageBoxButtons.YesNo, MessageBoxIcon.Error);

            return dialogResult == DialogResult.Yes ? confirmAction() : null;
        }

        public static string GetDescription(this Enum value)
        {
            var valueStr = value.ToString();
            var field = value.GetType().GetField(valueStr);
            var attribs = field.GetCustomAttributes(typeof(DescriptionAttribute), true);
            return attribs.Length > 0 ? ((DescriptionAttribute)attribs[0]).Description : valueStr;
        }
    }
}
