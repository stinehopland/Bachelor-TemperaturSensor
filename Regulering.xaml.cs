using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BLE_program
{
    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };

    //This code has taken elements from scenario1 & scenario2 of the microsoft sample code.
    //Referance: https://learn.microsoft.com/en-us/samples/microsoft/windows-universal-samples/bluetoothle/

    public sealed partial class MainPage : Page
    {
        private BluetoothLEDevice bluetoothLeDevice = null;
        private GattCharacteristic selectedCharacteristic;

        //only one characteristic at a time
        private GattCharacteristic registeredCharacteristic;
        private GattPresentationFormat presentationFormat;

        public string SelectedBleDeviceId;
        public string SelectedBleDeviceName = "No device selected";

        public int TemperatureSetValue;
        public decimal CurrentTemperature;
        public decimal Difference;

        public bool HeatElement;

        private ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();
        private List<DeviceInformation> UnknownDevices = new List<DeviceInformation>();

        private DeviceWatcher deviceWatcher;

        public GattDeviceService SelectedGattService { get; private set; }

        public MainPage()
        {
            InitializeComponent();
        }

        ////Display a message to the user.
        public void NotifyUser(string strMessage, NotifyType type, TextBlock targetBlock, Border targetBorder)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatus(strMessage, type, targetBlock, targetBorder);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type, targetBlock, targetBorder));
            }
        }

        private void UpdateStatus(string strMessage, NotifyType type, TextBlock targetBlock, Border targetBorder)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    targetBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    targetBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }

            targetBlock.Text = strMessage;

            // Collapse the target block if it has no text to conserve real estate.
            targetBlock.Visibility = (targetBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;

            // Raise an event if necessary to enable a screen reader to announce the status update.
            var peer = FrameworkElementAutomationPeer.FromElement(targetBlock);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }




        //This method is called when a user clicks on the enumerate button. 
        //If the BLE watcher is not running, the method starts, and the text changes to "Stop Enumerating"
        private void EnumerateButton_Click()
        {
            if (deviceWatcher == null)
            {
                StartBleDeviceWatcher();
                EnumerateButton.Content = "Stop enumerating";
                NotifyUser($"Device watcher started.", NotifyType.StatusMessage, StatusBlock, StatusBorder);
            }
            else
            {
                StopBleDeviceWatcher();
                EnumerateButton.Content = "Start enumerating";
                NotifyUser($"Device watcher stopped.", NotifyType.StatusMessage, StatusBlock, StatusBorder);
            }
        }

        private void StartEnumerater()
        {
            if (deviceWatcher == null)
            {
                StartBleDeviceWatcher();
                NotifyUser($"Device watcher started.", NotifyType.StatusMessage, StatusBlock, StatusBorder);
            }
        }

        private bool Not(bool value) => !value;

        //This method starts a device watcher that looks for all nearby Bluetooth devices.
        private void StartBleDeviceWatcher()
        {
            // Additional properties we would like about the device.
            // Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };

            // BT_Code: Example showing paired and non-paired in a single query.
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

            deviceWatcher =
                    DeviceInformation.CreateWatcher(
                        aqsAllBluetoothLEDevices,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            //Register eventhandlers before starting the watcher
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            //Start over with an empty collection
            KnownDevices.Clear();

            //Start the watcher. Active enumeration last for approximately 30s.
            deviceWatcher.Start();
        }

        //Stops watching for all nearby Bluetooth devices
        private void StopBleDeviceWatcher()
        {
            if (deviceWatcher != null)
            {
                //Unregister the event handlers
                deviceWatcher.Added -= DeviceWatcher_Added;
                deviceWatcher.Updated -= DeviceWatcher_Updated;
                deviceWatcher.Removed -= DeviceWatcher_Removed;
                deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
                deviceWatcher.Stopped -= DeviceWatcher_Stopped;

                //Stop the watcher
                deviceWatcher.Stop();
                deviceWatcher = null;
            }
        }
        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in KnownDevices)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }
        private DeviceInformation FindUnknownDevices(string id)
        {
            foreach (DeviceInformation bleDeviceInfo in UnknownDevices)
            {
                if (bleDeviceInfo.Id == id)
                {
                    return bleDeviceInfo;
                }
            }
            return null;
        }

        //This method is called when a BLE device is discovered by a DeviceWatcher object.
        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            //Updates the UI thread with info about a newly discovered BLE device
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Added {0}{1}", deviceInfo.Id, deviceInfo.Name));
                    if (sender == deviceWatcher)
                    {
                        if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                        {
                            if (deviceInfo.Name != string.Empty)
                            {
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                            }
                            else
                            {
                                UnknownDevices.Add(deviceInfo);
                            }
                        }
                    }
                }
            });
        }

        //Defines the event handler for DeviceWatcher.Updated, which is raised if the BLE devices properties have been updated.
        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // Updates the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Updated {0}{1}", deviceInfoUpdate.Id, ""));
                    if (sender == deviceWatcher)
                    {
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            // Device is already being displayed - update UX.
                            bleDeviceDisplay.Update(deviceInfoUpdate);
                            return;
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            deviceInfo.Update(deviceInfoUpdate);
                            // If device has been updated with a friendly name it's no longer unknown.
                            if (deviceInfo.Name != String.Empty)
                            {
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                                UnknownDevices.Remove(deviceInfo);
                            }
                        }
                    }
                }
            });
        }


        //This method handles the DeviceWatcher.Removed event, which is raised when a BLE device is no longer available.
        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // Updates the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Removed {0}{1}", deviceInfoUpdate.Id, ""));
                    if (sender == deviceWatcher)
                    {
                        // Find the corresponding DeviceInformation in the collection and remove it.
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            KnownDevices.Remove(bleDeviceDisplay);
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            UnknownDevices.Remove(deviceInfo);
                        }
                    }
                }
            });
        }

        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {
            // Updates the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (sender == deviceWatcher)
                {
                    NotifyUser($"{KnownDevices.Count} devices found. Enumeration completed.",
                        NotifyType.StatusMessage, StatusBlock, StatusBorder);
                }
            });
        }

        private async void DeviceWatcher_Stopped(DeviceWatcher sender, object e)
        {
            //Update the UI thread
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (sender == deviceWatcher)
                {
                    NotifyUser($"No longer watching for devices.",
                        sender.Status == DeviceWatcherStatus.Aborted ? NotifyType.ErrorMessage : NotifyType.StatusMessage, StatusBlock, StatusBorder);
                }
            });
        }

        //Connect Button
        private async void ConnectButton_Click()
        {
            ConnectButton.IsEnabled = false;
            NotifyUser($"Connecting to Bluetooth device ...", NotifyType.StatusMessage, StatusBlock, StatusBorder);
            var bleDeviceDisplay = ResultsListView.SelectedItem as BluetoothLEDeviceDisplay;
            if (bleDeviceDisplay != null)
            {
                SelectedBleDeviceId = bleDeviceDisplay.Id;
                SelectedBleDeviceName = bleDeviceDisplay.Name;
            }
            bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(SelectedBleDeviceId);

            if (bluetoothLeDevice != null)
            {
                GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesForUuidAsync(GattServiceUuids.DeviceInformation, BluetoothCacheMode.Uncached);
                if (result.Status == GattCommunicationStatus.Success)
                {
                    var deviceInformationService = result.Services.FirstOrDefault();
                    if (deviceInformationService != null)
                    {
                        SelectedGattService = deviceInformationService;
                        NotifyUser("Connected to device information service", NotifyType.StatusMessage, StatusBlock, StatusBorder);

                        //Get the temperature characteristic
                        var characteristicResult = await SelectedGattService.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.TemperatureMeasurement, BluetoothCacheMode.Uncached);
                        if (characteristicResult != null)
                        {
                            var temperatureInCelsiusCharacteristic = characteristicResult.Characteristics.FirstOrDefault();
                            if (temperatureInCelsiusCharacteristic != null)
                            {
                                selectedCharacteristic = temperatureInCelsiusCharacteristic;
                                NotifyUser("Connected to TemperatureInCelsius characteristic", NotifyType.StatusMessage, StatusBlock, StatusBorder);
                            }
                            else
                            {
                                NotifyUser("characteristic not found", NotifyType.ErrorMessage, StatusBlock, StatusBorder);
                            }
                        }
                        else
                        {
                            NotifyUser("Error accessing characteristic", NotifyType.ErrorMessage, StatusBlock, StatusBorder);
                        }
                    }
                    else
                    {
                        NotifyUser("Device Info not found", NotifyType.ErrorMessage, StatusBlock, StatusBorder);
                    }
                    ConnectButton.Content = "Connected";
                }
                else
                {
                    NotifyUser("Device unreachable", NotifyType.ErrorMessage, StatusBlock, StatusBorder);
                }
            }
            ConnectButton.IsEnabled = true;
        }

        private void SetVisibility(UIElement element, bool visible)
        {
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }


        //This method is called when a user clicks the UI button named 'CharacteristicReadButton'
        private async void CharacteristicReadButton_Click()
        {
            // Prompt the user to choose a file name and location
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Text file", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = "output";
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file == null)
            {
                return;
            }

            // Get the file path and create the file
            _ = file.Path;

            //Open the stream for writing
            StreamWriter writer = new StreamWriter(await file.OpenStreamForWriteAsync());

            try
            {
                // BT_Code: Read the actual value from the device by using Uncached.
                while (true)
                {
                    GattReadResult result = await selectedCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        CurrentTemperature = Convert.ToDecimal(result.Value);
                        string formattedResult = FormatValueByPresentation(result.Value, presentationFormat);

                        //add timestamp
                        string valueWithTimeStamp = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} - {formattedResult}";

                        await writer.WriteLineAsync(valueWithTimeStamp);
                        await writer.FlushAsync();

                        NotifyUser($"Read result: {formattedResult}", NotifyType.StatusMessage, StatusBlock, StatusBorder);
                    }
                    else
                    {
                        NotifyUser($"Read failed: {result.Status}", NotifyType.ErrorMessage, StatusBlock, StatusBorder);
                    }
                    await Task.Delay(1000);
                }
            }
            finally
            {
                writer.Dispose();
            }
        }

        private string FormatValueByPresentation(IBuffer buffer, GattPresentationFormat format)
        {
            // BT_Code: For the purpose of this sample, this function converts only UInt32 and
            // UTF-8 buffers to readable text. It can be extended to support other formats if your app needs them.
            CryptographicBuffer.CopyToByteArray(buffer, out byte[] data);
            if (format != null)
            {
                if (format.FormatType == GattPresentationFormatTypes.UInt32 && data.Length >= 4)
                {
                    return BitConverter.ToDouble(data, 0).ToString();
                }
                else if (format.FormatType == GattPresentationFormatTypes.Utf8)
                {
                    try
                    {
                        return Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "(error: Invalid UTF-8 string)";
                    }
                }
                else
                {
                    // Add support for other format types as needed.
                    return "Unsupported format: " + CryptographicBuffer.EncodeToHexString(buffer);
                }
            }
            else if (data != null)
            {
                // This is our custom calc service Result UUID. Format it like an Int
                if (selectedCharacteristic.Uuid.Equals(Constants.ResultCharacteristicUuid))
                {
                    return BitConverter.ToDouble(data, 0).ToString();
                }
                // No guarantees on if a characteristic is registered for notifications.
                else if (registeredCharacteristic != null)
                {
                    // This is our custom calc service Result UUID. Format it like an Int
                    if (registeredCharacteristic.Uuid.Equals(Constants.ResultCharacteristicUuid))
                    {
                        return BitConverter.ToDouble(data, 0).ToString();
                    }
                }
                else
                {
                    try
                    {
                        return "Temperatur = " + BitConverter.ToSingle(data, 0) + " Grader Celsius";
                    }
                    catch (ArgumentException)
                    {
                        return "Unknown format";
                    }
                }
            }
            else
            {
                return "Empty data received";
            }
            return "Unknown format";
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void StartComparing_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(tbGivenValue.Text, out int givenValue))
            {
                TemperatureSetValue = givenValue;
            }
            else
            {
                NotifyUser($"Please enter a valid integer number.", NotifyType.ErrorMessage, StatusBlockRegulator, StatusBorderRegulator);
            }

            Difference = TemperatureSetValue - CurrentTemperature;

            if (Difference > 1)
            {
                HeatElement = true;
                NotifyUser($"Set temperature is higher than current temperature. Heating element turned on.", NotifyType.StatusMessage, StatusBlockRegulator, StatusBorderRegulator);
            }
            else if (Difference < 1)
            {
                HeatElement = false;
                NotifyUser($"Set temperature is lower than current temperature. Heating element turned off.", NotifyType.StatusMessage, StatusBlockRegulator, StatusBorderRegulator);
            }
            else
            {
                HeatElement = false;
                NotifyUser($"Current temperature is within 1 degree celsius of set temperature.", NotifyType.StatusMessage, StatusBlockRegulator, StatusBorderRegulator);
            }
        }

        private void StopComparing_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
