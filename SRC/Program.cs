using System.Reflection;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Forms;
namespace FST {
    internal static class Program {
        public static string SaveDir;
        private const int Ctrl = 0x0002;
        [DllImport( "user32.dll" )]
        private static extern bool RegisterHotKey( IntPtr hWnd, int id, int fsModifiers, int vk );
        [DllImport( "user32.dll" )]
        private static extern bool UnregisterHotKey( IntPtr hWnd, int id );
        [STAThread]
        private static void Main() {
            try {
                SaveDir = File.ReadAllText( Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), "FST", "FST.conf" ) );
            }
            catch {
                SaveDir = Environment.GetFolderPath( Environment.SpecialFolder.MyPictures );
            }
            var context = new ScreenPasteApplicationContext(); // Create the context.
            var messageFilter = new WmHotkeyMessageFilter( context ); //Add previously implemented message filter to catch the hotkey.
            Application.AddMessageFilter( messageFilter );
            RegisterHotKey( new IntPtr( 0 ), context.GetHashCode(), Ctrl, (int) Keys.PrintScreen ); //Register the hotkey hook.
            Application.Run( context );
            UnregisterHotKey( new IntPtr( 0 ), context.GetHashCode() ); //Clean up after ourselves.
        }
    }
    [SecurityPermission( SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode )]
    internal class WmHotkeyMessageFilter : IMessageFilter {
        private const int WmHotkey = 0x312;
        private readonly ScreenPasteApplicationContext _appContext;
        public WmHotkeyMessageFilter( ScreenPasteApplicationContext context ) { this._appContext = context; }
        public bool PreFilterMessage( ref Message m ) {
            if ( m.Msg == WmHotkey ) {
                this._appContext.HandleHotkey();
                return true;
            }
            return false;
        }
    }

    internal static class AutoStart {
        private const string RunLocation = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "FST";
        private static readonly RegistryKey Key = Registry.CurrentUser.CreateSubKey( RunLocation );
        private static readonly string AssemblyLocation = Assembly.GetExecutingAssembly().Location;
        public static bool Enabled {
            get { return ( Key != null && ( Key.GetValue( ValueName ) as String == AssemblyLocation ) ); }
            set {
                if ( value )
                    Key.SetValue( ValueName, AssemblyLocation );
                else
                    Key.DeleteValue( ValueName );
            }
        }
    }

    internal class Screenshot {
        private readonly ImageCodecInfo _pngCodec = ImageCodecInfo.GetImageEncoders().First( a => a.MimeType == "image/png" );
        private readonly EncoderParameters _encpms = new EncoderParameters { Param = new[] { new EncoderParameter( Encoder.Quality, 100L ), new EncoderParameter( Encoder.ColorDepth, 24L ) } };
        private const string FilenameFormat = "{0:dd-MM-yyyy HH-mm-ss-FF}";
        public void TakeAndSave() {
            var screen = Screen.PrimaryScreen;
            var bitmap = new Bitmap( screen.Bounds.Width, screen.Bounds.Height, PixelFormat.Format32bppArgb );
            var v = Graphics.FromImage( bitmap );
            v.CopyFromScreen( screen.Bounds.X, screen.Bounds.Y, 0, 0, screen.Bounds.Size, CopyPixelOperation.SourceCopy );
            v.Flush();
            if ( !Directory.Exists( Program.SaveDir ) )
                try {
                    Directory.CreateDirectory( Program.SaveDir );
                }
                catch { }
            try {
                bitmap.Save( Path.Combine( Program.SaveDir, String.Format( FilenameFormat, DateTime.Now ) + ".png" ), this._pngCodec, this._encpms );
            }
            catch { }
            bitmap.Dispose();
            v.Dispose();
            GC.Collect();
        }
    }

    internal class ScreenPasteApplicationContext : ApplicationContext {
        private readonly Screenshot _scr = new Screenshot();
        private NotifyIcon _notifyIcon;
        public ScreenPasteApplicationContext() {
            #region Context Menu
            var _contextMenu = new ContextMenu();
            var openFolderMenuItem = _contextMenu.MenuItems.Add( "Open screenshots folder..." );
            openFolderMenuItem.Click += ( a, b ) => Process.Start( Program.SaveDir );
            _contextMenu.MenuItems.Add( "-" ); //Add a delimiter
            var autostartMenuItem = _contextMenu.MenuItems.Add( "Start with Windows" );
            autostartMenuItem.Checked = AutoStart.Enabled;
            autostartMenuItem.Click += this.autostartMenuItem_Click;
            var exitMenuItem = _contextMenu.MenuItems.Add( "Exit" );
            exitMenuItem.Click += ( a, b ) => this.ExitThread();
            #endregion
            #region Icon
            this._notifyIcon = new NotifyIcon { Icon = Icon.ExtractAssociatedIcon( Application.ExecutablePath ), Visible = true, ContextMenu = _contextMenu };
            #endregion
            this.ThreadExit += ( a, b ) => _notifyIcon.Dispose();
        }
        public void HandleHotkey() { ProcessScreenshot(); }
        private void ProcessScreenshot() { this._scr.TakeAndSave(); }
        private void autostartMenuItem_Click( object sender, EventArgs e ) {( (MenuItem) sender ).Checked = AutoStart.Enabled ^= true;}
    }
}