using System.Drawing;
using System.Windows.Forms;
using Diz.Core.model;

namespace DiztinGUIsh.window
{
    public partial class VisualizerForm : Form
    {
        private readonly MainWindow mainWindow;

        public VisualizerForm(MainWindow window)
        {
            mainWindow = window;
            InitializeComponent();
        }

        private Bitmap bitmap;

        private Color GetColorForFlag(Data.FlagType flag)
        {
            return flag switch
            {
                Data.FlagType.Unreached => Color.Black,
                Data.FlagType.Opcode => Color.Yellow,
                Data.FlagType.Operand => Color.YellowGreen,
                Data.FlagType.Graphics => Color.LightPink,
                Data.FlagType.Music => Color.PowderBlue,
                Data.FlagType.Data8Bit => Color.NavajoWhite,
                Data.FlagType.Data16Bit => Color.NavajoWhite,
                Data.FlagType.Data24Bit => Color.NavajoWhite,
                Data.FlagType.Data32Bit => Color.NavajoWhite,
                Data.FlagType.Pointer16Bit => Color.Orchid,
                Data.FlagType.Pointer24Bit => Color.Orchid,
                Data.FlagType.Pointer32Bit => Color.Orchid,
                Data.FlagType.Text => Color.Aquamarine,
                Data.FlagType.Empty => Color.DarkSlateGray,
                _ => Color.DarkSlateGray
            };
        }

        void GenerateImage()
        {
            if (bitmap != null)
                return;

            const int pixels_per_bank = 0xFFFF;
            const int bank_height = 48;
            const int bank_width = pixels_per_bank / bank_height;

            const int num_banks = 64;

            const int total_height = bank_height * num_banks;
            const int total_width = bank_width;

            bitmap = new Bitmap(total_width, total_height);

            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var romOffset = y * bank_width + x;
                    var romFlag = mainWindow.Project.Data.RomBytes[romOffset].TypeFlag;
                    var color = GetColorForFlag(romFlag);
                    bitmap.SetPixel(x, y, color);
                }
            }
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            GenerateImage();
            e.Graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
        }

        private void VisualizerForm_Load(object sender, System.EventArgs e)
        {

        }
    }
}