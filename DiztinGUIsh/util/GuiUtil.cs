using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Diz.Core.util;

namespace DiztinGUIsh.util
{
    public static class GuiUtil
    {
        public static void OpenExternalProcess(string argsToLaunch)
        {
            try
            {
                Util.OpenExternalProcess(argsToLaunch);
            }
            catch (Exception)
            {
                MessageBox.Show($"Can't launch '{argsToLaunch}', ignoring.", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

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
        
        public static string? PromptSelectFolder(this FolderBrowserDialog folderBrowserDialog, string path)
        {
            folderBrowserDialog.SelectedPath = path;
            return folderBrowserDialog.ShowDialog() == DialogResult.OK && folderBrowserDialog.SelectedPath != ""
                ? folderBrowserDialog.SelectedPath
                : null;
        }
    
        public static string? PromptSaveFileDialog(this FileDialog saveFileDialog, string path)
        {
            saveFileDialog.InitialDirectory = path;
            return saveFileDialog.ShowDialog() == DialogResult.OK && saveFileDialog.FileName != ""
                ? saveFileDialog.FileName
                : null;
        }

        public static string PromptToSelectFile(string initialDirectory = null)
        {
            var open = new OpenFileDialog {InitialDirectory = initialDirectory};
            return open.ShowDialog() == DialogResult.OK ? open.FileName : null;
        }

        // prompt the user to confirm they'd like to do something. if yes, call 'confirmAction'
        public static T PromptToConfirmAction<T>(string promptSubject, string promptText, Func<T> confirmAction)
        {
            var dialogResult =
                MessageBox.Show(promptText, promptSubject, MessageBoxButtons.YesNo, MessageBoxIcon.Error);

            return dialogResult == DialogResult.Yes ? confirmAction() : default;
        }

        private const string ScaryWarning = "BETA: Save/load in this version of Diz is unstable right now. \nDO NOT save over anything you care about without a backup.\nLoading an existing project file from previous versions of Diz is not guaranteed to work yet, it may silently fail to import data.\n\n\nEXPERTS ONLY. CONFIRM OPENING A PROJECT FILE?\n(If looking to mess around, do File -> Import ROM, much more stable for now)";
        public static bool PromptScaryUnstableBetaAreYouSure() => PromptToConfirmAction("IMPORTANT", ScaryWarning, () => true);


        /// <summary>
        /// Generate data bindings so that a combobox control will populate from an enum list
        /// Shortcut for doing these actions manually.
        /// </summary>
        /// 
        public static void BindListControlToEnum<TEnum>(ComboBox cb, object boundObject, string propertyName)
            where TEnum : Enum
        {
            // I feel like there's gotta be a better way to do all this. But, I can live with this for now.
            //
            // future improvements: would be great if we didn't have to pass in a string, and instead passed
            // in a property meta-info that we could generate the string value from.

            // take an enum type and create a list of each member, with the enum as the key, and anything in a [Description] attribute as the value
            var enumValuesAndDescriptionsKvp = Util.GetEnumDescriptions<TEnum>();
            var bs = new BindingSource(enumValuesAndDescriptionsKvp, null);

            BindListControl(cb, boundObject, propertyName, bs);
        }

        public static void BindListControl(ComboBox cb, object boundObject, string propertyName, BindingSource bs)
        {
            cb.DataBindings.Add(new Binding(
                "SelectedValue", boundObject,
                propertyName, false,
                DataSourceUpdateMode.OnPropertyChanged));

            cb.DataSource = bs;
            cb.DisplayMember = "Value";
            cb.ValueMember = "Key";
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        // call before you start any forms
        public static void SetupDpiStuff()
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }

        public static void EnableDoubleBuffering(Type type, Control obj)
        {
            // https://stackoverflow.com/a/1506066

            // Double buffering can make DGV slow in remote desktop, skip here.
            if (SystemInformation.TerminalServerSession)
                return;

            type.InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null,
                obj,
                new object[] {true});
        }

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, Int32 wMsg, bool wParam, Int32 lParam);

        // ReSharper disable once InconsistentNaming
        public const int WM_SETREDRAW = 11;
    }
}