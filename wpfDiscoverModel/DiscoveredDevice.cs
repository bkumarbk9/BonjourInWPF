using System;
using System.IO;

namespace wpfDiscoverModel
{
    public class DiscoveredDevice
    {
        #region Private Constants
        private const string PRINTER_IMG = @"\images\printer.png";
        private const string HTTP_IMG = @"\images\http.png";
        private const string UNKNOWN_IMG = @"\images\unknown.png";
        private const string SCANNER_IMG = @"\images\scan.png";
        private const string NVIDEA_IMG = @"\images\nvidea.png";
        private const string APPLE_IMG = @"\images\apple.png";
        private const string CLOUD_PRINTER_IMG = @"\images\print-cloud.png";
        private const string SMB_IMG = @"\images\smbShare.png";

        private const string PRINTER_SIGNATURE = "_printer.";
        private const string PRINTER_SIGNATURE_ALT = "_pdl-datastream.";
        private const string PRINTER_SIGNATURE_ALT1 = "_ipp.";
        private const string PRINTER_SIGNATURE_ALT2 = "_ipps.";

        private const string HTTP_SIGNATURE = "_http.";
        private const string SCANNER_SIGNATURE = "_scanner.";
        private const string SCANNER_SIGNATURE_ALT = "_uscans.";
        private const string SCANNER_SIGNATURE_ALT1 = "_uscan.";
        private const string NVIDIA_SIGNATURE = "_nvstream_dbd.";
        private const string APPLE_PRODUCT_SIGNATURE = "_companion-link.";
        private const string CLOUD_PRINTER_SIGNATURE = "_privet.";
        private const string SMB_SHARE_SIGNATURE = "_smb.";
        #endregion// Private Constants

        private static string currentDir = Directory.GetCurrentDirectory();

        #region Exposed public properties
        public string Key { get; private set; }
        public string Name { get; private set; }
        public string Domain { get; private set; }
        public string Service { get; private set; }
        public string DeviceImage { get; private set; }
        #endregion // Public propeties

        #region Construction
        public DiscoveredDevice(string key, string name, string domain, string service, string id)
        {
            this.Key = key;
            this.Name = name;
            this.Domain = domain;
            this.Service = service;
            SetDefaultImage(id);
        }

        #endregion // Construction

        #region Private Method
        private void SetDefaultImage(string Id)
        {
            if (Id.StartsWith(PRINTER_SIGNATURE) || Id.StartsWith(PRINTER_SIGNATURE_ALT)
             || Id.StartsWith(PRINTER_SIGNATURE_ALT1) || Id.StartsWith(PRINTER_SIGNATURE_ALT2))
            {
                DeviceImage = currentDir + PRINTER_IMG;
            }
            else if (Id.StartsWith(HTTP_SIGNATURE))
            {
                DeviceImage = currentDir + HTTP_IMG;
            }
            else if (Id.StartsWith(SCANNER_SIGNATURE) || Id.StartsWith(SCANNER_SIGNATURE_ALT)
                || Id.StartsWith(SCANNER_SIGNATURE_ALT1))
            {
                DeviceImage = currentDir + SCANNER_IMG;
            }
            else if (Id.StartsWith(NVIDIA_SIGNATURE))
            {
                DeviceImage = currentDir + NVIDEA_IMG;
            }
            else if (Id.StartsWith(APPLE_PRODUCT_SIGNATURE))
            {
                DeviceImage = currentDir + APPLE_IMG;
            }
            else if (Id.StartsWith(CLOUD_PRINTER_SIGNATURE))
            {
                DeviceImage = currentDir + CLOUD_PRINTER_IMG;
            }
            else if (Id.StartsWith(SMB_SHARE_SIGNATURE))
            {
                DeviceImage = currentDir + SMB_IMG;
            }
            else
            {
                DeviceImage = currentDir + UNKNOWN_IMG;
            }
        }
        #endregion // Private Method
    }
}
