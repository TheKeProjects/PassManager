using System;
using System.Windows;
using System.Windows.Threading;

namespace PassManager
{
    public partial class InstallProgressWindow : Window
    {
        public InstallProgressWindow()
        {
            InitializeComponent();
        }

        public void UpdateStatus(string status, string detail = "", int progress = -1)
        {
            if (Dispatcher.CheckAccess())
            {
                StatusText.Text = status;
                DetailText.Text = detail;

                if (progress >= 0)
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = progress;
                }
                else
                {
                    ProgressBar.IsIndeterminate = true;
                }

                // Force the UI to update immediately
                Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
            }
            else
            {
                Dispatcher.Invoke(() => UpdateStatus(status, detail, progress));
            }
        }
    }
}
