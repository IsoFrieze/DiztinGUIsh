using System;
using System.Reflection;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    partial class About : Form
    {
        public About()
        {
            InitializeComponent();
            Text = $"About {AssemblyTitle}";
            labelProductName.Text = AssemblyProduct;
            labelVersion.Text = $"Version {AssemblyVersion}";
            labelCopyright.Text = AssemblyCopyright;
            labelCompanyName.Text = AssemblyCompany;
            textBoxDescription.Text = AssemblyDescription;
        }

        #region Assembly Attribute Accessors

        public string AssemblyTitle
        {
            get
            {
                var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length <= 0)
                    return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
                
                var titleAttribute = (AssemblyTitleAttribute)attributes[0];
                return titleAttribute.Title != "" 
                    ? titleAttribute.Title 
                    : System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
            }
        }

        public string AssemblyVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public string AssemblyDescription
        {
            get
            {
                var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                var assembly = attributes.Length == 0 
                    ? null 
                    : ((AssemblyDescriptionAttribute)attributes[0]);

                if (assembly == null)
                    return "";

                var description =
                    assembly.Description + "\r\n" + "\r\n" + "Build info:\r\n" +
                    "Git branch: " + ThisAssembly.Git.Branch + "\r\n" +
                    "Git commit: " + ThisAssembly.Git.Commit + "\r\n" +
                    "Git repo URL: " + ThisAssembly.Git.RepositoryUrl + "\r\n" +
                    "Git tag: " + ThisAssembly.Git.Tag + "\r\n" +
                    "Git last commit date: " + ThisAssembly.Git.CommitDate + "\r\n";

                return description;
            }
        }

        public string AssemblyProduct
        {
            get
            {
                var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                return attributes.Length == 0 ? "" : ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                return attributes.Length == 0 ? "" : ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                return attributes.Length == 0 ? "" : ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }
        #endregion

        private void About_Load(object sender, EventArgs e)
        {

        }

        private void okButton_Click(object sender, EventArgs e)
        {

        }
    }
}
