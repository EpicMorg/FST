using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Linq;
using Microsoft.Win32;
namespace ScreenPaste {
	static class Program {
        public static string ED;
		const int CTRL = 0x0002;
		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
		[STAThread]
		static void Main(){
            try { ED = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FST", "FST.conf")); }
            catch{ED = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));}
			ScreenPasteApplicationContext context = new ScreenPasteApplicationContext();  // Create the context.
			WmHotkeyMessageFilter messageFilter = new WmHotkeyMessageFilter(context);		//Add previously implemented message filter to catch the hotkey.
			Application.AddMessageFilter(messageFilter);
			RegisterHotKey(new IntPtr(0), context.GetHashCode(), CTRL, (int)Keys.PrintScreen);	//Register the hotkey hook.
			Application.Run(context);
			UnregisterHotKey(new IntPtr(0), context.GetHashCode());	//Clean up after ourselves.
		}
	}
    [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
    public class WmHotkeyMessageFilter :  IMessageFilter {
        const int WM_HOTKEY = 0x312;
        ScreenPasteApplicationContext appContext;
        public WmHotkeyMessageFilter(ScreenPasteApplicationContext context) { appContext = context; }
        public bool PreFilterMessage(ref  Message m) {
            if (m.Msg == WM_HOTKEY) {
                appContext.HandleHotkey();
                return true;
            }
            return false;
        }
    }
    public static class AutoStart {
        private const string RUN_LOCATION = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string VALUE_NAME = "ScreenPaste";
        private static RegistryKey key = Registry.CurrentUser.CreateSubKey(RUN_LOCATION);
        public static void EnableAutoStart() { key.SetValue(VALUE_NAME, System.Reflection.Assembly.GetExecutingAssembly().Location); }
        public static bool IsAutoStartEnabled() { return (key != null && ((string) key.GetValue(VALUE_NAME) != null) && ((string) key.GetValue(VALUE_NAME) == System.Reflection.Assembly.GetExecutingAssembly().Location)); }
        public static void DisableSetAutoStart() { key.DeleteValue(VALUE_NAME); }
    }
    class Screenshot {
        ImageCodecInfo PNGCodec = ImageCodecInfo.GetImageEncoders().First(a => a.MimeType == "image/png");
        EncoderParameters encpms = new EncoderParameters() { Param = new EncoderParameter[] { new EncoderParameter(Encoder.Quality, 100L), new EncoderParameter(Encoder.ColorDepth, 24L) } };
        const string IMGUR_API_KEY = "3d5907509d22a3130787a91bbb3c9189";
        public static string PostToImgur(Bitmap bitmap) {
            MemoryStream memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Jpeg);
            return (string)(XDocument.Load(new MemoryStream(new WebClient().UploadValues("http://imgur.com/api/upload.xml", new NameValueCollection { { "key", IMGUR_API_KEY }, { "image", Convert.ToBase64String(memoryStream.ToArray()) } }))).Root.Element("original_image"));
        }
        const string FILENAME_FORMAT = "{0:dd-MM-yyyy HH-mm-ss-FF}";
        Bitmap _bitmap;
        public Bitmap Bitmap { get { return _bitmap; } set { } }
        public void TakeAndSave() {
            var currentScreen = Screen.PrimaryScreen;
            _bitmap = new Bitmap(currentScreen.Bounds.Width, currentScreen.Bounds.Height, PixelFormat.Format32bppArgb);
            var v = Graphics.FromImage(_bitmap);
            v.CopyFromScreen(currentScreen.Bounds.X, currentScreen.Bounds.Y, 0, 0, currentScreen.Bounds.Size, CopyPixelOperation.SourceCopy);
			v.Flush();
            if (!System.IO.Directory.Exists(Program.ED)) try{System.IO.Directory.CreateDirectory(Program.ED);}catch{}
            try{_bitmap.Save(Path.Combine(Program.ED, String.Format(FILENAME_FORMAT, DateTime.Now) + ".png"), this.PNGCodec ,encpms);}catch{}
			_bitmap.Dispose();
            v.Dispose();
            _bitmap = null;
            v = null;
			GC.Collect();
        }
    }
    public class ScreenPasteApplicationContext :  ApplicationContext
    {
        private Screenshot scr = new Screenshot();
        private  NotifyIcon nIcon;
        private  ContextMenu contextMenu;
        public ScreenPasteApplicationContext() {
            CreateContextMenu();
            CreateNotificationIcon();
            this.ThreadExit += new EventHandler(OnApplicationExit);
        }
        public void HandleHotkey() { ProcessScreenshot(); }
        void CreateContextMenu() {
            contextMenu = new ContextMenu();
            MenuItem openFolderMenuItem = contextMenu.MenuItems.Add("Open screenshots folder...");
            openFolderMenuItem.Click += new EventHandler(openFolderMenuItem_Click);
            contextMenu.MenuItems.Add("-");		//Add a delimiter
            MenuItem autostartMenuItem = contextMenu.MenuItems.Add("Start with Windows");
            autostartMenuItem.Checked = AutoStart.IsAutoStartEnabled();
            autostartMenuItem.Click += new EventHandler(autostartMenuItem_Click);
            MenuItem exitMenuItem = contextMenu.MenuItems.Add("Exit");
            exitMenuItem.Click += new EventHandler(exitMenuItem_Click);
        }
        void CreateNotificationIcon() {
            nIcon = new  NotifyIcon() { Icon = Icon.ExtractAssociatedIcon( Application.ExecutablePath), Visible = true, ContextMenu = (contextMenu != null ? contextMenu : null) };
            if (contextMenu != null)  nIcon.ContextMenu = contextMenu;
        }
        void ProcessScreenshot() { scr.TakeAndSave(); }
        private void nIcon_Click(object Sender, EventArgs e) { if ((( MouseEventArgs) e).Button ==  MouseButtons.Left) ProcessScreenshot(); }
        private void exitMenuItem_Click(object Sender, EventArgs e) { this.ExitThread(); }
        private void openFolderMenuItem_Click(object Sender, EventArgs e) { Process.Start(Program.ED); }
        private void autostartMenuItem_Click(object Sender, EventArgs e) {
            if (AutoStart.IsAutoStartEnabled()) AutoStart.DisableSetAutoStart(); else AutoStart.EnableAutoStart();
            ((MenuItem) Sender).Checked = AutoStart.IsAutoStartEnabled();
        }
        private void OnApplicationExit(object sender, EventArgs e) { nIcon.Dispose(); }
    }
}
