using System.Windows;

namespace wpfDiscover
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            new DeviceDiscoveryMainWindow().Show();
            base.OnStartup(e);
        }
    }
}
