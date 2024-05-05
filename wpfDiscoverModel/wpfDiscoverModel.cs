using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;

using NLog;
using Zeroconf;

namespace wpfDiscoverModel
{
    /// <summary>
    /// This is Model class exposing discovered devices
    /// </summary>
    public class WpfDiscoverModel : IObserver<DomainService>, INotifyPropertyChanged, IObserver<IZeroconfHost>
    {
        #region Private Members
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private Dictionary<string, DeviceDetails> discoveredDevices = new Dictionary<string, DeviceDetails>();
        private IObservable<DomainService> domainObserver;
        private IObservable<ServiceAnnouncement> serviceAnnouncement;
        private List<IDisposable> subDispose = new List<IDisposable>();
        private List<IObservable<IZeroconfHost>> zObservables = new List<IObservable<IZeroconfHost>>();
        private bool isShutDown = false;
        #endregion //  Private Members

        #region Private Consts
        private const int RETRY_COUNT = 2;
        private const int RETRY_DELAY_IN_MS = 2000;
        private const string SEPERATOR = " | ";
        private const string MANGLE = "@@";
        private const int DEFAULT_WAKE_TIME = 9000;
        private const int SECONDS_TO_MILLISECOND = 1000;
        #endregion // Private Consts

        #region Model Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<bool> StateChanged;
        public event Action<string> Error;
        public event Action<string> Unpublish;
        #endregion // Model Events

        #region Public Members
        public TimeSpan ScanTime { get;  set; }
        public int RetryCount { get;  set; }
        public int RetryDelayInMs { get;  set; }
        #endregion // Public Members

        /// <summary>
        /// Model Construction
        /// </summary>
        #region Model Construction
        public WpfDiscoverModel()
        {
            logger.Info("WpfDiscoverModel -- Model Creation");
            LoadDefaultConfigs();
            isShutDown = false;
            // This is for checking TTL expiry
            ThreadPool.QueueUserWorkItem(ProcssExpiry);
            logger.Info("WpfDiscoverModel -- Model Creation Complete");
        }

        ~WpfDiscoverModel()
        {
            isShutDown = true;
        }

        #endregion // Model Constrction

        #region Public Members
        /// <summary>
        /// Initializes Model
        /// </summary>
        public void Init()
        {
            try
            {
                domainObserver = ZeroconfResolver.BrowseDomainsContinuous(ScanTime, RetryCount, RetryDelayInMs);
                serviceAnnouncement = ZeroconfResolver.ListenForAnnouncementsAsync();

                subDispose.Add(domainObserver.Subscribe(this));
                subDispose.Add(serviceAnnouncement.Subscribe(OnAnnouncement));
                BubbleStateChanged(true);
            }
            catch (Exception ex)
            {
                BubbleError(ex.Message);
                Shutdown();
            }
        }

        /// <summary>
        /// Resets Model
        /// </summary>
        /// <param name="status"></param>
        public void Reset(object status)
        {
            bool state = GetState(status);

            if (state)
            {
                OnOff(false);
            }
            else
            {
                return;
            }

            OnOff(true);
        }

        /// <summary>
        /// Switches on or off the Model
        /// </summary>
        /// <param name="status"></param>
        public void OnOff(object status)
        {
            bool state = GetState(status);

            if (state)
            {
                Init();
            }
            else
            {
                Shutdown();
            }
        }

        /// <summary>
        /// Gives details of discovered device
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public DiscoveredDevice GetDiscoveredDevice(string key)
        {
            DiscoveredDevice deviceDiscvd = null;

            lock (this)
            {
                if (discoveredDevices.TryGetValue(key, out DeviceDetails details))
                {
                    logger.Debug("GetDiscoveredDevice -- Found :{0}", key);
                    deviceDiscvd = new DiscoveredDevice(key, details.Name, details.Domain, GetServiceNameStripped(details.Service), details.Id);
                }
                else
                {
                    logger.Error("GetDiscoveredDevice -- GetDiscoveredDevice Failed for key :{0}", key);
                }
            }

            return deviceDiscvd;
        }


        /// <summary>
        /// Get detailed description of service 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetDeviceDetails(string key)
        {
            string retVal = string.Empty;
            lock (this)
            {
                if (discoveredDevices.TryGetValue(key, out DeviceDetails details))
                {
                    logger.Debug("GetDeviceDetails -- GetDeviceDetails Found for key :{0}", key);
                    retVal = details.GetDeviceDetails();
                }
                else
                {
                    logger.Error("GetDeviceDetails -- GetDeviceDetails Failed for key :{0}", key);
                }
            }

            return retVal;
        }

        #endregion // Public Members

        #region IOberver<DomainService> Implementations
        /// <summary>
        /// This method is called by ZeroConf Library giving domain and service details
        /// </summary>
        /// <param name="value"></param>
        public void OnNext(DomainService value)
        {
            try
            {
                logger.Info("OnNext -- testDiscover::OnNext (DomainService) Serivce: {0}, Domain: {1}", value.Service, value.Domain);
                lock (this)
                {
                    IObservable<IZeroconfHost> zObservable = ZeroconfResolver.ResolveContinuous(value.Domain, ScanTime, RetryCount, RetryDelayInMs);
                    zObservables.Add(zObservable);
                    subDispose.Add(zObservable.Subscribe(this));
                }
            }
            catch (Exception ex)
            {
                BubbleError(ex.Message);
                Shutdown();
            }
        }

        /// <summary>
        /// This method is called by Zero conf library saying completed
        /// </summary>
        public void OnCompleted()
        {
            logger.Info("Completed");
            BubbleError("Completed Browsing");
        }

        /// <summary>
        /// This method is called by Zeroconf library when an error occurs
        /// </summary>
        /// <param name="error"></param>
        public void OnError(Exception error)
        {
            logger.Error(error.ToString());
            BubbleError(error.Message);
        }

        /// <summary>
        /// This is to load default settings
        /// </summary>
        public void LoadDefaultConfigs()
        {
            ScanTime = default(TimeSpan);
            RetryCount = RETRY_COUNT;
            RetryDelayInMs = RETRY_DELAY_IN_MS;
        }
        #endregion

        #region IObserver<IZeroconfHost> Members
        /// <summary>
        /// This method is csalled by Zeroconf library with resolved details.
        /// </summary>
        /// <param name="value"></param>
        public void OnNext(IZeroconfHost value)
        {
            AddNewDevices(value);
        }
        #endregion

        #region Private Members
        /// <summary>
        /// Formats Service Announcement to be displayed well
        /// </summary>
        /// <param name="sa"></param>
        /// <returns></returns>
        private string FormatServiceAnnouncement(ServiceAnnouncement sa)
        {
            StringBuilder svcAnnouncement = new StringBuilder();
            svcAnnouncement.AppendFormat("Name: {0}", sa.AdapterInformation.Name);
            svcAnnouncement.AppendFormat("\n Address: {0}", sa.AdapterInformation.Address);
            svcAnnouncement.AppendFormat("\n {0}", FormatIZeroConfHost(sa.Host));

            return svcAnnouncement.ToString();
        }

        /// <summary>
        /// Serivce announcement callback
        /// </summary>
        /// <param name="sa"></param>
        private void OnAnnouncement(ServiceAnnouncement sa)
        {
            string svcAnnounceDetails = FormatServiceAnnouncement(sa);
            BubbleError(svcAnnounceDetails);
            logger.Info(svcAnnounceDetails);
        }

        /// <summary>
        /// Gets current state of Model
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private bool GetState(object status)
        {
            bool state = false;
            if (status is bool)
            {
                state = (bool)status;
            }

            return state;
        }

        /// <summary>
        /// Propagate changed state to layers above
        /// </summary>
        /// <param name="state"></param>
        private void BubbleStateChanged(bool state)
        {
            StateChanged?.Invoke(state);
        }

        /// <summary>
        /// Disposes and shuts down
        /// </summary>
        private void Shutdown()
        {
            try
            {
                lock (this)
                {
                    for (int item = subDispose.Count() - 1; item >= 0; item--)
                    {
                        subDispose[item].Dispose();
                    }

                    zObservables.Clear();
                    subDispose.Clear();
                    discoveredDevices.Clear();
                    domainObserver = null;
                    serviceAnnouncement = null;
                    BubbleStateChanged(false);
                }
            }
            catch(Exception ex)
            {
                BubbleError(ex.Message);
                Shutdown();
            }
        }

        /// <summary>
        /// Gets Service Name 
        /// </summary>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        private string GetServiceNameStripped(string serviceName)
        {
            int lastIndex = serviceName.LastIndexOf(":");
            if (lastIndex > 0)
            {
                return serviceName.Substring(0, lastIndex);
            }
            else
            {
                return serviceName;
            }
        }

        /// <summary>
        /// Adds new device 
        /// </summary>
        /// <param name="zconfHost"></param>
        private void AddNewDevices(IZeroconfHost zconfHost)
        {
             List<string> devicesToPublish = new List<string>();
            List<string> devicesToUnpublish = new List<string>();

            if (zconfHost == null || zconfHost.IPAddress == null || zconfHost.Services == null)
            {
                logger.Error("Received Null values, so disregarding");
                return;
            }

            string key = zconfHost.IPAddress;

            foreach (var service in zconfHost.Services)
            {
                if ( service.Key == null)
                { 
                    logger.Error("Received Null values for service.key, so disregarding");
                    continue;
                }

                string serviceKey = key;
                serviceKey += MANGLE + service.Key;
                serviceKey += MANGLE + service.Value.Port.ToString();

                DeviceDetails newDevice = new DeviceDetails(this, serviceKey, zconfHost.IPAddress,
                                                            service.Value.Port.ToString(), zconfHost.DisplayName,
                                                            zconfHost.IPAddress, service.Key, service.Value.Ttl,
                                                            FormatIZeroConfHost(zconfHost));

                if (service.Value.Ttl <= 0)
                {
                    devicesToUnpublish.Add(serviceKey);
                }
                else
                {
                    devicesToPublish.Add(serviceKey);
                    lock (this)
                    {
                        if (discoveredDevices.ContainsKey(serviceKey))
                            discoveredDevices.Remove(serviceKey);

                        discoveredDevices.Add(serviceKey, newDevice);
                    }
                }
            }

            foreach(string newDevice in devicesToPublish)
            {
                Publish(newDevice);
            }

            foreach(string removeDevice in devicesToUnpublish)
            {
                Unpublish(removeDevice);
            }

            foreach (string removeDevice in devicesToUnpublish)
            {
                if (discoveredDevices.ContainsKey(removeDevice))
                    discoveredDevices.Remove(removeDevice);
                else
                    logger.Error("Device Not found in list: {0}", removeDevice);
            }

        }

        /// <summary>
        /// This thread processes expiry of TTl
        /// </summary>
        /// <param name="state"></param>
        private void ProcssExpiry(object state)
        {
            int nextWakeup;
            List<string> toPublish = new List<string>();

            while (!isShutDown)
            {
                nextWakeup = DEFAULT_WAKE_TIME;

                lock (this)
                {

                    foreach (var device in discoveredDevices)
                    {
                        logger.Info("ProcssExpiry -- Checking Device for expiration: {0}", device.Key);
                        if (device.Value.TTL == DeviceDetails.INVALID_TTL)
                        {
                            continue;
                        }

                        var secondsElapsed = (DateTime.Now - device.Value.ReceivedTimestamp).TotalSeconds;

                        if (device.Value.Expired == false
                            && secondsElapsed > device.Value.TTL)
                        {
                            device.Value.SetExpired();
                            logger.Info("ProcssExpiry -- Device Expired: {0}", device.Key);
                            toPublish.Add(device.Key);
                            continue;
                        }

                        int minWakeup = (int)(device.Value.TTL - secondsElapsed) * SECONDS_TO_MILLISECOND;

                        if (minWakeup <= 0)
                        {
                            nextWakeup = 0;
                        }
                        else if (nextWakeup > minWakeup)
                        {
                            nextWakeup = minWakeup;
                        }
                    }
                }

                foreach (string key in toPublish)
                {
                    Publish(key);
                }

                toPublish.Clear();
                Thread.Sleep(nextWakeup);
            }
        }

        #endregion // Private Members

        #region Internal Members
        /// <summary>
        ///  Formats device
        /// </summary>
        /// <param name="zconfHost"></param>
        /// <returns></returns>
        internal string FormatIZeroConfHost(IZeroconfHost zconfHost)
        {
            if (zconfHost == null)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Display Name: {0}", zconfHost.DisplayName);
            sb.AppendFormat("\nId: {0}", zconfHost.Id);
            sb.AppendFormat("\nIP Address: {0}", zconfHost.IPAddress);

            sb.AppendFormat("\nIP Addresses:");
            foreach (var ipAddrs in zconfHost.IPAddresses)
            {
                sb.AppendFormat(" {0} ", ipAddrs);
            }

            sb.AppendFormat("\n\nServices:");
            foreach (var service in zconfHost.Services)
            {
                sb.AppendFormat("\n\n ----- Begin -----");
                sb.AppendFormat("\n Service Key: {0}", service.Key);
                sb.AppendFormat("\n Service Name: {0}", service.Value.Name);
                sb.AppendFormat("\n Service Port: {0}", service.Value.Port);
                sb.AppendFormat("\n Service TTL: {0}", service.Value.Ttl);
                sb.AppendFormat("\n Service Properties:");

                foreach (var servicePropertyList in service.Value.Properties)
                {
                    foreach (var serviceProperty in servicePropertyList)
                    {
                        sb.AppendFormat("\n   {0}: {1}", serviceProperty.Key, serviceProperty.Value);
                    }
                }

                sb.AppendFormat("\n ----- End -----");

            }

            return sb.ToString();
        }

        internal void BubbleError(string error)
        {
            Error?.Invoke(DateTime.Now.ToString() + SEPERATOR + error);
        }

        internal void Publish(string key)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(key));
        }

        internal void UnpublishDevice(string key)
        {
            Unpublish?.Invoke(key);
        }
        #endregion // Internal Members

    }
}
