using Telerik.Windows.Controls;
using NLog;

namespace wpfDiscover
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class DeviceDiscoveryMainWindow : RadWindow
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// wpfDiscover View class
        /// </summary>
        public DeviceDiscoveryMainWindow()
        {
            logger.Info("Init DeviceDiscoveryMainWindow");

            InitializeComponent();
            menuPane.ContextMenuTemplate = null;
            errorPane.ContextMenuTemplate = null;
            devicesPane.ContextMenuTemplate = null;
            detailsPane.ContextMenuTemplate = null; 

            logger.Info("Init DeviceDiscoveryMainWindow complete");
        }
    }

    
}
