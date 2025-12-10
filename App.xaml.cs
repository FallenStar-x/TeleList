using System.Text;
using System.Windows;

namespace TeleList
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Register code pages encoding provider for Windows-1252 and other encodings
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            base.OnStartup(e);
        }
    }
}
