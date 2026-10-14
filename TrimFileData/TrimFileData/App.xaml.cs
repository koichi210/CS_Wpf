using System.Windows;

namespace TrimFileData
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            StcWpfExceptionGuard.Install(this);
            base.OnStartup(e);
        }
    }
}
