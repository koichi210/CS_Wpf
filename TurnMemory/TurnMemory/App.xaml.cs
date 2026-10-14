using System.Windows;

namespace TurnMemory
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
