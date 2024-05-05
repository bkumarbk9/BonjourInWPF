using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.ComponentModel;

using NLog;
using wpfDiscoverModel;

/// <summary>
/// This is Model-View for WPFDiscover MVVM 
/// </summary>

namespace wpfDiscoverViewModel
{
    /// <summary>
    /// WPF Discover View- Model class
    /// </summary>
    public class WpfDiscoverVM : INotifyPropertyChanged
    {
        #region Bound ICommand interfaces

        // Bind to On Off switch
        public ICommand OnOffCommand { get; private set; }
        // Bind to Selected device
        public ICommand GetSelectedItemProp { get; private set; }
        // For resetting
        public ICommand ResetCommand { get; private set; }

        #endregion // Bound ICommand interfaces

        /// <summary>
        /// These Properties are bind to XAML GUI view
        /// </summary>
        #region Bound properties

        public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; private set; }
        public string DeviceDetails { get; set; }
        public DiscoveredDevice SelectedItem { get; set; }
        public bool OnOffSwitch { get; set; }
        public long TimeSpan { get; set; }
        public int RetryCount { get; set; }
        public int RetryInMs { get; set; }
        public string ErrorDetails { get; set; }
        public string ErrorPaneColor { get; set; }

        public bool ErrorPaneActive
        {
            private get
            {
                return errorPaneActive;
            }

            set
            {
                if (value == true && ErrorDetails?.Count() > 0)
                {
                    ErrorPaneColor = ERROR_ACK;
                    BubblePropertyChanged("ErrorPaneColor");
                }
                errorPaneActive = value;
            }
        }

        #endregion // Bound Properties

        #region Property Changed Event
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion // Prop changed event

        #region Private Members

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private bool errorPaneActive;
        private WpfDiscoverModel discoverModel;
        private HashSet<string> publishedDevices = new HashSet<string>();
        private readonly bool  initComplete = false;

        #endregion // Private Members

        #region Private Constants

        private const string NO_ERROR = "Green";    // When no error occured
        private const string ERROR = "Red";         // When Error occured 
        private const string ERROR_ACK = "Black";   // When Error is seen by user

        #endregion // Private Constants

        #region Model-View Construction
        public WpfDiscoverVM()
        {
            logger.Info("Construction of WpfDiscoverVM");

            // Create and initialize, subscribe to Model
            discoverModel = new WpfDiscoverModel();
            OnOffSwitch = true;
            ErrorPaneColor = NO_ERROR;
            discoverModel.PropertyChanged += DiscoverModel_PublishDevice;
            discoverModel.StateChanged += DiscoverModel_StateChanged;
            discoverModel.Unpublish += DiscoverModel_Unpublish;
            discoverModel.Error += DiscoverModel_Error;
            discoverModel.Init();

            // Bind commands
            OnOffCommand = new GeneralRelayCmd(new Action<object>(discoverModel.OnOff));
            GetSelectedItemProp = new GeneralRelayCmd(new Action<object>(UpdateSelectedItemDetails));
            ResetCommand = new GeneralRelayCmd(new Action<object>(Reset));

            // Initialize view elements
            DiscoveredDevices = new ObservableCollection<DiscoveredDevice>();
            BindingOperations.EnableCollectionSynchronization(DiscoveredDevices, this);
            initComplete = true;

            logger.Info("Construction of WpfDiscoverVM, Complete");
        }
        #endregion // Model-View construction

        #region Model-View bound Events

        /// <summary>
        /// Event for bubbling Errors to View layer
        /// </summary>
        /// <param name="error"></param>
        private void DiscoverModel_Error(string error)
        {
            if (!initComplete)
            {
                return;
            }

            ErrorDetails += error + "\r\n";
            BubblePropertyChanged("ErrorDetails");
            if (!errorPaneActive)
            {
                ErrorPaneColor = ERROR;
                BubblePropertyChanged("ErrorPaneColor");
            }
            logger.Error("DiscoverModel_Error -- Error Reported: [{0}]", error);
        }

        /// <summary>
        /// When new changes are applied, Application is reset
        /// </summary>
        /// <param name="resetState"></param>
        private void Reset(object resetState)
        {
            logger.Info("Reset -- Reset started");

            if (false == UpdateModelConfig())
            {
                return;
            }

            discoverModel.Reset(resetState);

            logger.Info("Reset -- Reset Complete");
        }

        /// <summary>
        /// When User or System changes from On to Off 
        /// </summary>
        /// <param name="stateChanged"></param>
        private void DiscoverModel_StateChanged(bool stateChanged)
        {
            if (!initComplete)
            {
                return;
            }

            if (!stateChanged)
            {
                // Erace all devices from view as switch off action is triggered
                DeviceDetails = string.Empty;
                DiscoveredDevices.Clear();
                lock (publishedDevices)
                {
                    publishedDevices.Clear();
                }
                BubblePropertyChanged("DeviceDetails");
                logger.Info("DiscoverModel_StateChanged -- State changed to Off");
            }

            // Propogate On/Off switch sstatus to view
            OnOffSwitch = stateChanged;
            BubblePropertyChanged("OnOffSwitch");
        }

        /// <summary>
        /// When a new device is discoverd,this event is triggered
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DiscoverModel_PublishDevice(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!initComplete)
            {
                return;
            }

            if (e == null)
            {
                return;
            }

            DiscoveredDevice device = discoverModel.GetDiscoveredDevice(e.PropertyName);

            lock (publishedDevices)
            {
                if (device == null || publishedDevices.Contains(device.Key))
                {
                    if (SelectedItem != null && SelectedItem.Key == e.PropertyName)
                    {
                        UpdateSelectedItemDetails(device);
                    }

                    return;
                }
                publishedDevices.Add(device.Key);
            }
            // Publish Device
            logger.Info("DiscoverModel_PublishDevice -- New Device discovered: {0} Device Name: ", device.Key, device.Name);

            DiscoveredDevices.Add(device);
            BubblePropertyChanged("DiscoveredDevices");

            lock (publishedDevices)
            {
                if (publishedDevices.Count() > 1)
                {
                    return;
                }
            }

            // Update PUblished device details
            logger.Info("DiscoverModel_PublishDevice -- Publishing New Device details: {0} Device Name: ", device.Key, device.Name);
            SelectedItem = device;
            BubblePropertyChanged("SelectedItem");
            UpdateSelectedItemDetails(device);
        }

        private void DiscoverModel_Unpublish(string deviceKey)
        {
            logger.Info("DiscoverModel_Unpublish -- Unpublishing New Device details: {0} Device Name: ", deviceKey);

            lock (publishedDevices)
            {
                if (publishedDevices.Contains(deviceKey))
                {
                    publishedDevices.Remove(deviceKey);
                }
                else
                {
                    logger.Error("DiscoverModel_Unpublish -- Device not found to unpublish: {0}", deviceKey);
                }
            }

            DiscoveredDevice deviceToRemove = DiscoveredDevices.FirstOrDefault(item => item.Key == deviceKey);
            if (deviceToRemove == null)
            {
                logger.Error("DiscoverModel_Unpublish -- Device not found to remove from GUI: {0}", deviceKey);
                return;
            }

            DiscoveredDevices.Remove(deviceToRemove);
            BubblePropertyChanged("DiscoveredDevices");
        }

        #endregion // Model-View bound Events

        #region Private Methods

        private void BubblePropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        private bool UpdateModelConfig()
        {
            if (this.TimeSpan == 0 && RetryCount == 0 && RetryInMs == 0)
            {
                return false;
            }

            discoverModel.ScanTime = new TimeSpan(this.TimeSpan);
            discoverModel.RetryCount = RetryCount;
            discoverModel.RetryDelayInMs = RetryInMs;

            return true;
        }

        private void UpdateSelectedItemDetails(object selectedDevice)
        {
            if (!initComplete)
            {
                return;
            }

            if (selectedDevice is DiscoveredDevice)
            {
                DeviceDetails = discoverModel.GetDeviceDetails(((DiscoveredDevice)selectedDevice).Key);
                BubblePropertyChanged("DeviceDetails");
            }
        }

        #endregion // Private Methods
    }
}
