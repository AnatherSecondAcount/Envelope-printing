using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices; // PInvoke
using System.Windows.Interop; // Imaging
using System.Windows.Media; // ImageSource

namespace Envelope_printing
{
    /// <summary>
    /// Логика взаимодействия для EditRecipientView.xaml
    /// </summary>
    public partial class EditRecipientView : Window
    {
        public EditRecipientView()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplyIcon();
            ContentRendered += (_, __) => ApplyIcon();
            DataContextChanged += (_, __) => ApplyIcon();
        }

        private void ApplyIcon()
        {
            // try stock shell icon first
            if (!TryApplySystemIcon())
            {
                // fallback to vector DrawingImage from resources
                try
                {
                    var key = (this.Title ?? string.Empty).IndexOf("Добав", StringComparison.OrdinalIgnoreCase) >=0
                        ? "WindowIcon.AddRecipient"
                        : ((this.Title ?? string.Empty).IndexOf("Редакт", StringComparison.OrdinalIgnoreCase) >=0 ? "WindowIcon.EditRecipient" : null);
                    if (key != null && Application.Current?.Resources[key] is ImageSource img)
                    {
                        this.Icon = img;
                    }
                }
                catch { }
            }
        }

        private bool TryApplySystemIcon()
        {
            try
            {
                var title = this.Title ?? string.Empty;
                var stockId = title.Contains("Добав", StringComparison.OrdinalIgnoreCase)
                    ? SHSTOCKICONID.SIID_New
                    : (title.Contains("Редакт", StringComparison.OrdinalIgnoreCase) ? SHSTOCKICONID.SIID_Edit : SHSTOCKICONID.SIID_Application);
                IntPtr hIcon = GetStockIconHandle(stockId);
                if (hIcon != IntPtr.Zero)
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    this.Icon = src;
                    DestroyIcon(hIcon);
                    return true;
                }
            }
            catch { }
            return false;
        }

        // P/Invoke helpers
        private static IntPtr GetStockIconHandle(SHSTOCKICONID id)
        {
            var info = new SHSTOCKICONINFO();
            info.cbSize = (uint)Marshal.SizeOf(typeof(SHSTOCKICONINFO));
            int hr = SHGetStockIconInfo(id, SHGSI.SHGSI_ICON | SHGSI.SHGSI_SMALLICON, ref info);
            return hr ==0 ? info.hIcon : IntPtr.Zero;
        }

        [DllImport("Shell32.dll", SetLastError = false)]
        private static extern int SHGetStockIconInfo(SHSTOCKICONID siid, SHGSI uFlags, ref SHSTOCKICONINFO psii);

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHSTOCKICONINFO
        {
            public uint cbSize;
            public IntPtr hIcon;
            public int iSysImageIndex;
            public int iIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst =260)]
            public string szPath;
        }

        private enum SHGSI : uint
        {
            SHGSI_ICON =0x000000100,
            SHGSI_DISPLAYNAME =0x000000200,
            SHGSI_TYPENAME =0x000000400,
            SHGSI_ATTRIBUTES =0x000000800,
            SHGSI_ICONLOCATION =0x000001000,
            SHGSI_LARGEICON =0x000000000,
            SHGSI_SMALLICON =0x000000001,
            SHGSI_SHELLICONSIZE =0x000000004
        }

        private enum SHSTOCKICONID : uint
        {
            SIID_Application =2,
            SIID_New =181, // generic "new"
            SIID_Edit =245 // pencil edit icon
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Здесь можно будет добавить логику валидации перед закрытием
            this.DialogResult = true;
        }
    }
}
