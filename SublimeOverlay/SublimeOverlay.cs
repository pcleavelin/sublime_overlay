using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SublimeOverlay
{
    public partial class SublimeOverlay : Form
    {
        Panel titleBarPanel;

        PictureBox minimizeControl;
        PictureBox maximizeControl;
        PictureBox closeControl;

        Metafile minimizePicture;
        Metafile maximizePicture;
        Metafile closePicture;

        Color titleBarColor;
        Color minimizeColor;
        Color maximizeColor;
        Color closeColor;

        Point grabLocation;

        bool isDragging;

        public SublimeOverlay()
        {
            InitializeComponent();
            InitializeTitleBarControls();
        }

        private void InitializeTitleBarControls()
        {
            // TODO: Load configuration file for colors
            this.titleBarColor = Color.Red;
            this.minimizeColor = Color.White;
            this.maximizeColor = Color.White;
            this.closeColor = Color.White;

            this.isDragging = false;
            this.grabLocation = this.Location;

            // Load min/max/close vector images
            this.minimizePicture = new Metafile(new MemoryStream(Properties.Resources.window_minimize));
            this.maximizePicture = new Metafile(new MemoryStream(Properties.Resources.window_maximize));
            this.closePicture = new Metafile(new MemoryStream(Properties.Resources.window_close));

            var titleBarSize = 64;
            var controlSize = titleBarSize / 2;
            var spacing = titleBarSize / 8;

            // Create Minimize/Maximze/Close buttons
            this.closeControl = new PictureBox();
            this.closeControl.Dock = DockStyle.None;
            this.closeControl.Width = controlSize;
            this.closeControl.Height = controlSize;
            this.closeControl.Location = new Point((this.Location.X + this.Width - spacing) - this.closeControl.Width, this.Location.Y + spacing);
            this.closeControl.Parent = this.titleBarPanel;

            this.maximizeControl = new PictureBox();
            this.maximizeControl.Dock = DockStyle.None;
            this.maximizeControl.Width = controlSize;
            this.maximizeControl.Height = controlSize;
            this.maximizeControl.Location = new Point((this.Location.X + this.Width - spacing) - (this.closeControl.Width + spacing + this.maximizeControl.Width), this.Location.Y + spacing);
            this.maximizeControl.Parent = this.titleBarPanel;

            this.minimizeControl = new PictureBox();
            this.minimizeControl.Dock = DockStyle.None;
            this.minimizeControl.Width = controlSize;
            this.minimizeControl.Height = controlSize;
            this.minimizeControl.Location = new Point(
                (this.Location.X + this.Width - spacing) - (this.closeControl.Width + spacing + this.maximizeControl.Width + spacing + this.minimizeControl.Width),
                this.Location.Y + spacing);
            this.minimizeControl.Parent = this.titleBarPanel;

            // Add paiting methods for SVG rendering
            this.minimizeControl.Paint += MinimizeControl_Paint;
            this.maximizeControl.Paint += MaximizeControl_Paint;
            this.closeControl.Paint += CloseControl_Paint;

            // Add mouse event handlers
            this.minimizeControl.MouseUp += MinimizeControl_MouseUp;
            this.maximizeControl.MouseUp += MaximizeControl_MouseUp;
            this.closeControl.MouseUp += CloseControl_MouseUp;

            // Create custom title bar
            this.titleBarPanel = new Panel();
            this.titleBarPanel.BackColor = this.titleBarColor;
            this.titleBarPanel.Location = this.Location;
            this.titleBarPanel.Name = "titleBarPanel";
            this.titleBarPanel.Width = this.Width;
            this.titleBarPanel.Height = titleBarSize;
            this.titleBarPanel.MouseUp += new MouseEventHandler(this.TitleBar_MouseUp);
            this.titleBarPanel.MouseDown += new MouseEventHandler(this.TitleBar_MouseDown);
            this.titleBarPanel.MouseMove += TitleBarPanel_MouseMove;

            this.Controls.Add(this.titleBarPanel);
            this.Controls.Add(this.minimizeControl);
            this.Controls.Add(this.maximizeControl);
            this.Controls.Add(this.closeControl);

            this.minimizeControl.BringToFront();
            this.maximizeControl.BringToFront();
            this.closeControl.BringToFront();

            this.BackColor = this.titleBarColor;
        }

        private void CloseWindow()
        {
            this.Close();
        }

        private void MaximizeWindow()
        {
            MessageBox.Show("Maximize");
        }

        private void MinimizeWindow()
        {
            MessageBox.Show("Minimize");
        }

        private void CloseControl_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox pict = (PictureBox)sender;
            if (e.Location.X >= 0 && e.Location.X <= pict.Width)
            {
                if (e.Location.Y >= 0 && e.Location.Y <= pict.Height)
                {
                    this.CloseWindow();
                }
            }
        }

        private void MaximizeControl_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox pict = (PictureBox)sender;
            if (e.Location.X >= 0 && e.Location.X <= pict.Width)
            {
                if (e.Location.Y >= 0 && e.Location.Y <= pict.Height)
                {
                    this.MaximizeWindow();
                }
            }
        }

        private void MinimizeControl_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox pict = (PictureBox)sender;
            if (e.Location.X >= 0 && e.Location.X <= pict.Width)
            {
                if (e.Location.Y >= 0 && e.Location.Y <= pict.Height)
                {
                    this.MinimizeWindow();
                }
            }
        }

        private void CloseControl_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImage(this.closePicture, new Rectangle(Point.Empty, this.closeControl.ClientSize));
        }

        private void MaximizeControl_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImage(this.maximizePicture, new Rectangle(Point.Empty, this.maximizeControl.ClientSize));
        }

        private void MinimizeControl_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImage(this.minimizePicture, new Rectangle(Point.Empty, this.minimizeControl.ClientSize));
        }

        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            this.isDragging = false;
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (!this.isDragging)
            {
                this.isDragging = true;
                this.grabLocation = e.Location;
            }
        }

        private void TitleBarPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.isDragging)
            {
                this.DesktopLocation = new Point(this.DesktopLocation.X + e.Location.X - this.grabLocation.X, this.DesktopLocation.Y + e.Location.Y - this.grabLocation.Y);
            }
        }
    }
}
