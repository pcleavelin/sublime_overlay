using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SublimeOverlay
{
    public partial class SublimeOverlay : Form
    {
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewlong);
        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        public static extern int SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, ref Rectangle lpRect);
        [DllImport("user32.dll")]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWndChild, int nCmdShow);

        public static int GWL_STYLE = -16;
        public static int WS_CHILD = 0x40000000;
        public static int WS_DLGFRAME = 0x00400000;
        public static int WS_THICKFRAME = 0x00040000;
        public static int WS_BORDER = 0x0000000;
        public static int WS_CAPTION = 0x00C00000;

        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        public static int SW_SHOW = 5;

        Panel titleBarPanel;
        Panel sublimePanel;

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

        Process sublimeProc;
        IntPtr sublimeHandle;

        const int grabSpacing = 4; // you can rename this variable if you like

        Rectangle GrabTop { get { return new Rectangle(0, 0, this.ClientSize.Width, grabSpacing); } }
        Rectangle GrabLeft { get { return new Rectangle(0, 0, grabSpacing, this.ClientSize.Height); } }
        Rectangle GrabBottom { get { return new Rectangle(0, this.ClientSize.Height - grabSpacing, this.ClientSize.Width, grabSpacing); } }
        Rectangle GrabRight { get { return new Rectangle(this.ClientSize.Width - grabSpacing, 0, grabSpacing, this.ClientSize.Height); } }

        Rectangle GrabTopLeft { get { return new Rectangle(0, 0, grabSpacing, grabSpacing); } }
        Rectangle GrabTopRight { get { return new Rectangle(this.ClientSize.Width - grabSpacing, 0, grabSpacing, grabSpacing); } }
        Rectangle GrabBottomLeft { get { return new Rectangle(0, this.ClientSize.Height - grabSpacing, grabSpacing, grabSpacing); } }
        Rectangle GrabBottomRight { get { return new Rectangle(this.ClientSize.Width - grabSpacing, this.ClientSize.Height - grabSpacing, grabSpacing, grabSpacing); } }

        string SublimeExePath = "";

        public SublimeOverlay()
        {
            InitializeComponent();
            
            this.FormBorderStyle = FormBorderStyle.None;
            this.SetStyle(ControlStyles.ResizeRedraw, true);

            // TODO: load config file

            InitializeTitleBarControls();
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);

            if (this.WindowState == FormWindowState.Normal)
            {
                if (message.Msg == 0x84)
                {
                    var cursor = this.PointToClient(Cursor.Position);

                    if (GrabTopLeft.Contains(cursor)) message.Result = (IntPtr)HTTOPLEFT;
                    else if (GrabTopRight.Contains(cursor)) message.Result = (IntPtr)HTTOPRIGHT;
                    else if (GrabBottomLeft.Contains(cursor)) message.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (GrabBottomRight.Contains(cursor)) message.Result = (IntPtr)HTBOTTOMRIGHT;

                    else if (GrabTop.Contains(cursor)) message.Result = (IntPtr)HTTOP;
                    else if (GrabLeft.Contains(cursor)) message.Result = (IntPtr)HTLEFT;
                    else if (GrabRight.Contains(cursor)) message.Result = (IntPtr)HTRIGHT;
                    else if (GrabBottom.Contains(cursor)) message.Result = (IntPtr)HTBOTTOM;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            this.ResizeControls(this.Width, this.Height);
        }

        private void ResizeControls(int width, int height)
        {
            if (this.titleBarPanel != null && this.sublimePanel != null)
            {
                this.titleBarPanel.Width = width - grabSpacing * 2;

                this.sublimePanel.Width = width - grabSpacing * 2;
                this.sublimePanel.Height = height - this.titleBarPanel.Height - grabSpacing;

                if (this.sublimeHandle != null)
                {
                    MoveWindow(this.sublimeHandle, 0, 0, width, height - this.titleBarPanel.Height, true);
                }

                if (this.minimizeControl != null && this.maximizeControl != null && this.closeControl != null)
                {
                    var spacing = 8;
                    this.closeControl.Location = new Point((this.titleBarPanel.Width - spacing) - this.closeControl.Width, spacing);
                    this.maximizeControl.Location = new Point((this.titleBarPanel.Width - spacing) - (this.closeControl.Width + spacing + this.maximizeControl.Width), spacing);
                    this.minimizeControl.Location = new Point((this.titleBarPanel.Width - spacing) - (this.closeControl.Width + spacing + this.maximizeControl.Width + spacing + this.minimizeControl.Width), spacing);
                }
            }
        }

        private void InitializeTitleBarControls()
        {
            // TODO: Load configuration file for colors
            this.titleBarColor = Color.FromArgb(38, 50, 56);
            this.minimizeColor = Color.White;
            this.maximizeColor = Color.White;
            this.closeColor = Color.White;

            this.isDragging = false;
            this.grabLocation = this.Location;

            // Load min/max/close vector images
            this.minimizePicture = new Metafile(new MemoryStream(Properties.Resources.window_minimize));
            this.maximizePicture = new Metafile(new MemoryStream(Properties.Resources.window_maximize));
            this.closePicture = new Metafile(new MemoryStream(Properties.Resources.window_close));

            var titleBarSize = 32;
            var controlSize = 24;
            var spacing = titleBarSize / 8;

            // Create custom title bar
            this.titleBarPanel = new Panel();
            this.titleBarPanel.BackColor = this.titleBarColor;
            this.titleBarPanel.Location = new Point(this.Location.X + grabSpacing, this.Location.Y + grabSpacing);
            this.titleBarPanel.Name = "titleBarPanel";
            this.titleBarPanel.Width = this.Width - grabSpacing * 2;
            this.titleBarPanel.Height = titleBarSize;
            this.titleBarPanel.MouseUp += new MouseEventHandler(this.TitleBar_MouseUp);
            this.titleBarPanel.MouseDown += new MouseEventHandler(this.TitleBar_MouseDown);
            this.titleBarPanel.MouseMove += TitleBarPanel_MouseMove;

            // Create panel to place sublime window
            this.sublimePanel = new Panel();
            this.sublimePanel.Location = new Point(grabSpacing, titleBarSize);
            this.sublimePanel.Width = this.Width - grabSpacing * 2;
            this.sublimePanel.Height = this.Height - titleBarSize - grabSpacing;

            // Create Minimize/Maximze/Close buttons
            this.closeControl = new PictureBox();
            this.closeControl.Dock = DockStyle.None;
            this.closeControl.Width = controlSize;
            this.closeControl.Height = controlSize;
            this.closeControl.Parent = this.titleBarPanel;
            this.closeControl.Location = new Point((this.Location.X + this.titleBarPanel.Width - spacing) - this.closeControl.Width, this.Location.Y + spacing);

            this.maximizeControl = new PictureBox();
            this.maximizeControl.Dock = DockStyle.None;
            this.maximizeControl.Width = controlSize;
            this.maximizeControl.Height = controlSize;
            this.maximizeControl.Parent = this.titleBarPanel;
            this.maximizeControl.Location = new Point((this.Location.X + this.titleBarPanel.Width - spacing) - (this.closeControl.Width + spacing + this.maximizeControl.Width), this.Location.Y + spacing);

            this.minimizeControl = new PictureBox();
            this.minimizeControl.Dock = DockStyle.None;
            this.minimizeControl.Width = controlSize;
            this.minimizeControl.Height = controlSize;
            this.minimizeControl.Parent = this.titleBarPanel;
            this.minimizeControl.Location = new Point(
                (this.Location.X + this.titleBarPanel.Width - spacing) - (this.closeControl.Width + spacing + this.maximizeControl.Width + spacing + this.minimizeControl.Width),
                this.Location.Y + spacing);

            // Add paiting methods for SVG rendering
            this.minimizeControl.Paint += MinimizeControl_Paint;
            this.maximizeControl.Paint += MaximizeControl_Paint;
            this.closeControl.Paint += CloseControl_Paint;

            // Add mouse event handlers
            this.minimizeControl.MouseUp += MinimizeControl_MouseUp;
            this.maximizeControl.MouseUp += MaximizeControl_MouseUp;
            this.closeControl.MouseUp += CloseControl_MouseUp;

            this.Controls.Add(this.titleBarPanel);
            this.Controls.Add(this.sublimePanel);
            this.Controls.Add(this.minimizeControl);
            this.Controls.Add(this.maximizeControl);
            this.Controls.Add(this.closeControl);

            this.minimizeControl.BringToFront();
            this.maximizeControl.BringToFront();
            this.closeControl.BringToFront();

            this.BackColor = this.titleBarColor;
        }

        private void SublimeOverlay_Load(object sender, EventArgs e)
        {
            this.CaptureSublimeWindow();
        }

        private bool CaptureSublimeWindow()
        {
            this.sublimeProc = Process.Start(this.SublimeExePath);
            
            var captureTask = new Task(() =>
            {
                Thread.Sleep(500);

                if (sublimeProc != null && !sublimeProc.HasExited)
                {
                    this.sublimeHandle = sublimeProc.MainWindowHandle;
                }
            });

            captureTask.Start();
            captureTask.Wait();

            if (this.sublimeHandle == IntPtr.Zero)
            {
                MessageBox.Show("Failed to capture Sublime window!");
            }
            else
            {
                SetWindowLong(this.sublimeHandle, GWL_STYLE, GetWindowLong(this.sublimeHandle, GWL_STYLE) & ~WS_CAPTION & ~WS_THICKFRAME);
                SetParent(this.sublimeHandle, this.sublimePanel.Handle);

                var rect = new Rectangle();
                GetWindowRect(this.sublimeHandle, ref rect);

                this.ResizeControls(rect.Width, rect.Height);
                this.Width = rect.Width;
                this.Height = rect.Height;

                MoveWindow(this.sublimeHandle, 0, 0, this.Width, this.Height - this.titleBarPanel.Height, true);
            }

            return true;
        }

        private void CloseWindow()
        {
            if(this.sublimeHandle != null)
            {
                this.sublimeHandle = IntPtr.Zero;
                this.sublimeProc.CloseMainWindow();
            }

            this.Close();
        }

        private void MaximizeWindow()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
            }
        }

        private void MinimizeWindow()
        {
            this.WindowState = FormWindowState.Minimized;
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

        private void PaintTitleBarControl(Graphics graphics, Image image, Color color, Size controlSize)
        {
            var r = ((float)color.R) / 255.0f;
            var g = ((float)color.G) / 255.0f;
            var b = ((float)color.B) / 255.0f;

            ImageAttributes attr = new ImageAttributes();
            ColorMatrix matrix = new ColorMatrix(new float[][]
                {
                    new float[]{ 1.0f, 0.0f, 0.0f, 0.0f, 0.0f },
                    new float[]{ 0.0f, 1.0f, 0.0f, 0.0f, 0.0f },
                    new float[]{ 0.0f, 0.0f, 1.0f, 0.0f, 0.0f },
                    new float[]{ 0.0f, 0.0f, 0.0f, 1.0f, 0.0f },
                    new float[]{ r,    g,    b,    1.0f, 1.0f }
                });
            attr.SetColorMatrix(matrix);

            var rect = new Rectangle(Point.Empty, controlSize);
            graphics.DrawImage(image, rect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attr);
        }

        private void CloseControl_Paint(object sender, PaintEventArgs e)
        {
            this.PaintTitleBarControl(e.Graphics, this.closePicture, this.closeColor, this.closeControl.ClientSize);
        }

        private void MaximizeControl_Paint(object sender, PaintEventArgs e)
        {
            this.PaintTitleBarControl(e.Graphics, this.maximizePicture, this.maximizeColor, this.maximizeControl.ClientSize);
        }

        private void MinimizeControl_Paint(object sender, PaintEventArgs e)
        {
            this.PaintTitleBarControl(e.Graphics, this.minimizePicture, this.minimizeColor, this.minimizeControl.ClientSize);
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
