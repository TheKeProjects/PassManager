using System.Windows;

namespace PassManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize application data directory
            var dataManager = DataManager.Instance;
        }
    }
}
