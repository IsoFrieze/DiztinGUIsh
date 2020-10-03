using System;
using System.ComponentModel;
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

            BindListControlToEnum(cb, boundObject, propertyName, bs);
        }

        private static void BindListControlToEnum(ComboBox cb, object boundObject, string propertyName,
            BindingSource bs)
        {
            cb.DataBindings.Add(new Binding(
                "SelectedValue", boundObject,
                propertyName, false,
                DataSourceUpdateMode.OnPropertyChanged));

            cb.DataSource = bs;
            cb.DisplayMember = "Value";
            cb.ValueMember = "Key";
        }
    }
}