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
    public struct Handle_Data
    {
        public ulong process_id;
        public IntPtr window_handle;
    }

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

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, ref IntPtr lpdwProcessId);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lpdwParam);

        private static int GWL_STYLE = -16;
        private static int WS_CHILD = 0x40000000;
        private static int WS_DLGFRAME = 0x00400000;
        private static int WS_THICKFRAME = 0x00040000;
        private static int WS_BORDER = 0x0000000;
        private static int WS_CAPTION = 0x00C00000;

        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private static int SW_SHOW = 5;

        Panel titleBarPanel;
        Panel sublimePanel;

        PictureBox minimizeControl;
        PictureBox maximizeControl;
        PictureBox closeControl;

        Metafile minimizePicture;
        Metafile maximizePicture;
        Metafile closePicture;

        Point grabLocation;

        Process sublimeProc;
        IntPtr sublimeHandle;

        bool isDragging;

        const int grabSpacing = 8; // you can rename this variable if you like

        Rectangle GrabTop { get { return new Rectangle(0, 0, this.ClientSize.Width, grabSpacing); } }
        Rectangle GrabLeft { get { return new Rectangle(0, 0, grabSpacing, this.ClientSize.Height); } }
        Rectangle GrabBottom { get { return new Rectangle(0, this.ClientSize.Height - grabSpacing, this.ClientSize.Width, grabSpacing); } }
        Rectangle GrabRight { get { return new Rectangle(this.ClientSize.Width - grabSpacing, 0, grabSpacing, this.ClientSize.Height); } }

        Rectangle GrabTopLeft { get { return new Rectangle(0, 0, grabSpacing, grabSpacing); } }
        Rectangle GrabTopRight { get { return new Rectangle(this.ClientSize.Width - grabSpacing, 0, grabSpacing, grabSpacing); } }
        Rectangle GrabBottomLeft { get { return new Rectangle(0, this.ClientSize.Height - grabSpacing, grabSpacing, grabSpacing); } }
        Rectangle GrabBottomRight { get { return new Rectangle(this.ClientSize.Width - grabSpacing, this.ClientSize.Height - grabSpacing, grabSpacing, grabSpacing); } }

        SublimeOverlayConfiguration configuration;

        public SublimeOverlay()
        {
            InitializeComponent();

            this.FormBorderStyle = FormBorderStyle.None;
            this.SetStyle(ControlStyles.ResizeRedraw, true);

            this.configuration = SublimeOverlayConfiguration.Load();

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

        /// <summary>
        /// Resizes the title bar and its controls
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
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

        /// <summary>
        /// Initializes the titlebar and all of its controls
        /// </summary>
        private void InitializeTitleBarControls()
        {
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
            this.titleBarPanel.BackColor = this.configuration.TitleBarColor;
            this.titleBarPanel.Location = new Point(this.Location.X + grabSpacing, this.Location.Y + grabSpacing);
            this.titleBarPanel.Name = "titleBarPanel";
            this.titleBarPanel.Width = this.Width - grabSpacing * 2;
            this.titleBarPanel.Height = titleBarSize;
            this.titleBarPanel.MouseUp += new MouseEventHandler(this.TitleBar_MouseUp);
            this.titleBarPanel.MouseDown += new MouseEventHandler(this.TitleBar_MouseDown);
            this.titleBarPanel.MouseMove += TitleBarPanel_MouseMove;

            // Create panel to place sublime window
            this.sublimePanel = new Panel();
            this.sublimePanel.Location = new Point(grabSpacing, this.titleBarPanel.Location.Y + this.titleBarPanel.Height);
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

            this.BackColor = this.configuration.TitleBarColor;
        }

        /// <summary>
        /// Called when the form loads
        /// </summary>
        /// <param name="sender">The form</param>
        /// <param name="e">The event args</param>
        private void SublimeOverlay_Load(object sender, EventArgs e)
        {
            this.CaptureSublimeWindow();
        }

        /// <summary>
        /// Captures the sublime window and makes us the its parent
        /// </summary>
        /// <returns>Whether the parenting succeeded or not</returns>
        private bool CaptureSublimeWindow()
        {
            // First check for existing sublime window

            if (string.IsNullOrEmpty(this.configuration.SublimeExePath)
                || !File.Exists(this.configuration.SublimeExePath))
            {
                return false;
            }

            this.sublimeProc = Process.Start(this.configuration.SublimeExePath);

            do
            {
                if (!sublimeProc.HasExited)
                {
                    this.sublimeHandle = sublimeProc.MainWindowHandle;
                }
                else if (sublimeProc.ExitCode == 0)
                {
                    this.sublimeProc = Process.GetProcessesByName("sublime_text").FirstOrDefault();

                    if (sublimeProc != null)
                    {
                        this.sublimeHandle = GetHiddenSublimeHandle((long)this.sublimeProc.Id);
                    }
                }
           } while (this.sublimeHandle == IntPtr.Zero);

            if (this.sublimeHandle == IntPtr.Zero)
            {
                MessageBox.Show("Failed to capture Sublime window!");
                return false;
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

        /// <summary>
        /// Gets a handle for a hidden sublime window
        /// </summary>
        /// <param name="processId">The process id for a sublime text process</param>
        /// <returns>hWnd of the window</returns>
        private IntPtr GetHiddenSublimeHandle(long processId)
        {
            IntPtr sublimeHWnd = IntPtr.Zero;
            var result = EnumWindows(delegate(IntPtr hWnd, IntPtr param) 
            {
                IntPtr id = new IntPtr(0);
                GetWindowThreadProcessId(hWnd, ref id);

                if(id.ToInt64() == processId)
                {
                    sublimeHWnd = hWnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return sublimeHWnd;
        }

        /// <summary>
        /// Closes all windows
        /// </summary>
        private void CloseWindow()
        {
            if(this.sublimeHandle != IntPtr.Zero)
            {
                this.sublimeHandle = IntPtr.Zero;
                this.sublimeProc.CloseMainWindow();
            }

            this.Close();
        }

        /// <summary>
        /// Maxmizes the window
        /// </summary>
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

        /// <summary>
        /// Minimizes the window
        /// </summary>
        private void MinimizeWindow()
        {
            this.WindowState = FormWindowState.Minimized;
        }

        /// <summary>
        /// Called when the close control has a mouse up event
        /// </summary>
        /// <param name="sender">The CloseControl</param>
        /// <param name="e">The event args</param>
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

        /// <summary>
        /// Called when the maximize control has a mouse up event
        /// </summary>
        /// <param name="sender">The MaximizeControl</param>
        /// <param name="e">The event args</param>
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

        /// <summary>
        /// Called when the minimize control has a mouse up event
        /// </summary>
        /// <param name="sender">The MinimizeControl</param>
        /// <param name="e">The event args</param>
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

        /// <summary>
        /// Called when the title bar has a mouse up event
        /// </summary>
        /// <param name="sender">The Title Bar</param>
        /// <param name="e">The event args</param>
        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            this.isDragging = false;
        }

        /// <summary>
        /// Called when the title bar has a mouse down event
        /// </summary>
        /// <param name="sender">The Title Bar</param>
        /// <param name="e">The event args</param>
        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (!this.isDragging)
            {
                this.isDragging = true;
                this.grabLocation = e.Location;
            }
        }

        /// <summary>
        /// Called when the title bar has a mouse move event
        /// </summary>
        /// <param name="sender">The Title Bar</param>
        /// <param name="e">The event args</param>
        private void TitleBarPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.isDragging)
            {
                this.DesktopLocation = new Point(this.DesktopLocation.X + e.Location.X - this.grabLocation.X, this.DesktopLocation.Y + e.Location.Y - this.grabLocation.Y);
            }
        }

        /// <summary>
        /// Given an image, draws it with custom colors
        /// </summary>
        /// <param name="graphics">The <see cref="System.Drawing.Graphics"/></param>
        /// <param name="image">The <see cref="System.Drawing.Image"/> to draw</param>
        /// <param name="color">The custom <see cref="System.Drawing.Color"/></param>
        /// <param name="controlSize">The control's <see cref="System.Drawing.Size"/></param>
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

        /// <summary>
        /// Called when the close control has a paint event
        /// </summary>
        /// <param name="sender">The CloseControl</param>
        /// <param name="e">The event args</param>
        private void CloseControl_Paint(object sender, PaintEventArgs e)
        {
            this.PaintTitleBarControl(e.Graphics, this.closePicture, this.configuration.CloseControlColor, this.closeControl.ClientSize);
        }

        /// <summary>
        /// Called when the maximize control has a paint event
        /// </summary>
        /// <param name="sender">The MaximizeControl</param>
        /// <param name="e">The event args</param>
        private void MaximizeControl_Paint(object sender, PaintEventArgs e)
        {
            this.PaintTitleBarControl(e.Graphics, this.maximizePicture, this.configuration.MaximizeControlColor, this.maximizeControl.ClientSize);
        }

        /// <summary>
        /// Called when the minimize control has a paint event
        /// </summary>
        /// <param name="sender">The MinimizeControl</param>
        /// <param name="e">The event args</param>
        private void MinimizeControl_Paint(object sender, PaintEventArgs e)
        {
            this.PaintTitleBarControl(e.Graphics, this.minimizePicture, this.configuration.MinimizeControlColor, this.minimizeControl.ClientSize);
        }
    }
}
