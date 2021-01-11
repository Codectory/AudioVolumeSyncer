using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using CodectoryCore.Logging;
using CodectoryCore.UI.Wpf;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Serialization;

namespace AudioVolumeSyncer
{
    public class AudioVolumeSyncerHandler : BaseViewModel
    {
        public static Logs Logs = new Logs("AudioVolumeSyncer.log", "Audio Volume Syncer", true);


        static IDisposable _deviceChangedDisposable;
        static Dictionary<CoreAudioDevice, IDisposable> _volumeDisposables = new Dictionary<CoreAudioDevice, IDisposable>();
        static private VolumeProvider _volumeProvider;
        static private AudioMasterChangedProvider _masterChangedProvider;
        static readonly object accessLock = new object();
        static readonly object masterLock = new object();
        static List<CoreAudioDevice> _audioDevices = new List<CoreAudioDevice>();

        public static event EventHandler DevicesChanged;

        public static bool Initialized { get; private set; } = false;

        public static IReadOnlyCollection<CoreAudioDevice> Devices => Tools.AudioController.GetDevices(DeviceType.Playback).ToList().AsReadOnly();



        private string SettingsPath => $"{System.AppDomain.CurrentDomain.BaseDirectory}AudioVolumeSync_Settings.xml";
        TaskbarIcon TrayMenu;
        readonly object _accessLock = new object();

        private bool started = false;
        public bool Started { get => started; private set { started = value; OnPropertyChanged(); } }

        private bool _showView = false;
        private AudioVolumeSyncSettings settings;

        public bool ShowView { get => _showView;  set { _showView = value; OnPropertyChanged(); } }

        public RelayCommand LoadingCommand { get; private set; }
        public RelayCommand ClosingCommand { get; private set; }
        public RelayCommand ShutdownCommand { get; private set; }


        public AudioVolumeSyncSettings Settings { get => settings; set { settings = value; OnPropertyChanged(); } }


        public AudioVolumeSyncerHandler()
        {
           // ChangeLanguage( new System.Globalization.CultureInfo("en-US"));
            Initialize();
        }

        private void ChangeLanguage(CultureInfo culture)
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture(culture.Name);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture(culture.Name);
        }

        #region Initialization

        private void Initialize()
        {

            lock (_accessLock)
            {
                if (Initialized)
                    return;
                Logs.Add("Initializing...", false);
                InitializeAudioObjects();
                LoadSettings();
                InitializeTrayMenu();
                CreateRelayCommands();
                SwitchTrayIcon(Settings.StartMinimizedToTray);
                ShowView = !Settings.StartMinimizedToTray;
       
                Initialized = true;
                Logs.Add("Initialized", false);

            }
        }

        public static void InitializeAudioObjects()
        {
            lock (accessLock)
            {
                if (Initialized)
                    return;
                try
                {
                    _volumeProvider = new VolumeProvider();
                    _masterChangedProvider = new AudioMasterChangedProvider();
                    _volumeProvider.VolumeChanged += Provider_VolumeChanged;
                    _masterChangedProvider.DeviceChanged += _masterChangedProvider_DeviceChanged;
                    _deviceChangedDisposable = Tools.AudioController.AudioDeviceChanged.Subscribe(_masterChangedProvider);
                }
                catch (Exception ex)
                {
                    Logs.AddException(ex);
                    throw;
                }
            }
        }


        private void LoadSettings()
        {
            Logs.Add("Loading settings..", false);
            try
            {
                Settings = AudioVolumeSyncSettings.ReadSettings(SettingsPath);
            }
            catch (Exception ex)
            {
                Logs.Add("Failed to load settings", false);
                Logs.AddException(ex);
                Settings = new AudioVolumeSyncSettings();
                SaveSettings();
                Logs.Add("Created new settings file", false);

            }
            foreach (var guid in Settings.SyncAudioDevicesGuids)
                AddAudioDevice(Tools.AudioController.GetDevice((Guid)guid));
            settings.SyncAudioDevicesGuids.CollectionChanged += SyncAudioDevicesGuids_CollectionChanged;
            settings.PropertyChanged += Settings_PropertyChanged;
            Logs.Add("Settings loaded", false);
        }



        private void SyncAudioDevicesGuids_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            lock (accessLock)
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                    foreach (var guid in e.NewItems)
                        AddAudioDevice(Tools.AudioController.GetDevice((Guid)guid));
                else if (e.Action == NotifyCollectionChangedAction.Remove)
                    foreach (var guid in e.OldItems)
                        RemoveAudioDevice(Tools.AudioController.GetDevice((Guid)guid));
            }
        }

        private void InitializeTrayMenu()
        {
            Logs.Add("Initializing tray menu", false);
            try
            {
                TrayMenu = new TaskbarIcon();
                TrayMenu.Visibility = Visibility.Hidden;
                TrayMenu.ToolTipText = Locale_Texts.AudioVolumeSyncer;
                TrayMenu.Icon = Locale_Texts.Logo;
                ContextMenu contextMenu = new ContextMenu();
                MenuItem close = new MenuItem()
                {
                    Header = Locale_Texts.Shutdown
                };
                close.Click += (o, e) => Shutdown();

                MenuItem open = new MenuItem()
                {
                    Header = Locale_Texts.Open
                };
                open.Click += (o, e) => SwitchTrayIcon(false);

                contextMenu.Items.Add(open);
                contextMenu.Items.Add(close);
                TrayMenu.ContextMenu = contextMenu;
                TrayMenu.TrayLeftMouseDown += TrayMenu_TrayLeftMouseDown;
                Logs.Add("Tray menu initialized", false);

            }
            catch (Exception ex)
            {
                Logs.AddException(ex);
                throw ex;
            }
        }

        private void CreateRelayCommands()
        {

            ClosingCommand = new RelayCommand(Closing);
            ShutdownCommand = new RelayCommand(Shutdown);
        }

        #endregion Initialization

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            lock (_accessLock)
            {
                Tools.SetAutoStart(Locale_Texts.AudioVolumeSyncer, System.Reflection.Assembly.GetEntryAssembly().Location, settings.AutoStart);
                SaveSettings();
            }
        } 



        private void TrayMenu_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            Logs.Add("Open app from Tray", false);
            SwitchTrayIcon(false);
            ShowView = true;

        }

        private void SaveSettings()
        {
            Logs.Add("Saving settings..", false);
            try
            {
                Settings.SaveSettings(SettingsPath);
                Logs.Add("Settings saved", false);

            }
            catch (Exception ex)
            {
                Logs.AddException(ex);
                throw;
            }
        }




        private void Closing()
        {
            if (Settings.CloseToTray)
            {
                Logs.Add($"Minimizing to tray...", false);
                SwitchTrayIcon(true);
            }
            else
            {
                Logs.Add($"Shutting down...", false);
                Shutdown();
            }
        }

        private void Shutdown()
        {
            Clean();
            SwitchTrayIcon(false);
            Application.Current.Shutdown();
        }



        private void SwitchTrayIcon(bool showTray)
        {
            TrayMenu.Visibility = showTray ? System.Windows.Visibility.Visible : Visibility.Hidden;
        }


        private void AddAudioDevice(CoreAudioDevice device)
        {
            lock (accessLock)
            {
                if (!_audioDevices.Any(d => d.Equals(device)))
                {

                    _audioDevices.Add(device);
                    _volumeDisposables.Add(device, device.VolumeChanged.Subscribe(_volumeProvider));
                    Logs.Add($"Slave device added: {device.FullName} - {device.Id}", false);

                }
            }
            SyncVolume(device);
            SaveSettings();
        }

        private void RemoveAudioDevice(CoreAudioDevice device)
        {
            lock (accessLock)
            {
                if (_audioDevices.Any(d => d.Equals(device)))
                {
                    _volumeDisposables[device].Dispose();
                    _volumeDisposables.Remove(device);
                    _audioDevices.Remove(device);
                    Logs.Add($"Slave device removed: {device.FullName} - {device.Id}", false);
                }
            }
            SyncVolume(device);
            SaveSettings();
        }

        private static void _masterChangedProvider_DeviceChanged(object sender, DeviceChangedArgs e)
        {

            //if (e.ChangedType == DeviceChangedType.DefaultChanged)
            //{
            //    lock (masterLock)
            //    {
            //        lock (accessLock)
            //            _volumeDisposables.Remove(MasterAudioDevice);
            //        AddSlaveDevice(MasterAudioDevice);
            //        RemoveSlaveDevice((CoreAudioDevice)e.Device);
            //        MasterAudioDevice = (CoreAudioDevice)e.Device;
            //        lock (accessLock)
            //            _volumeDisposables.Add(MasterAudioDevice, MasterAudioDevice.VolumeChanged.Subscribe(_volumeProvider));
            //        SyncVolume(MasterAudioDevice);
            //        Logs.Add($"New master: {MasterAudioDevice.FullName} - {MasterAudioDevice.Id}", false);

            //        MasterChanged?.Invoke(this, EventArgs.Empty);
            //    }
            //}
            if (e.ChangedType == DeviceChangedType.DeviceAdded || e.ChangedType == DeviceChangedType.DeviceRemoved)
                DevicesChanged?.Invoke(null, EventArgs.Empty);


        }

        private static void SyncVolume(CoreAudioDevice changedDevice)
        {
            lock (accessLock)
            {
                _volumeProvider.Paused = true;
                try
                {
                    double volume = changedDevice.Volume;
                    bool muted = changedDevice.IsMuted;
                    Logs.Add($"Volume changed - Muted: {muted} New volume: {changedDevice.Volume} Device: {changedDevice.FullName} - {changedDevice.Id}", false);
                    foreach (CoreAudioDevice slaveDevice in _audioDevices.ToList())
                    {
                        if (!muted)
                            slaveDevice.Volume = volume;
                        slaveDevice.Mute(muted);
                    }
                }
                catch (Exception ex)
                {
                    Logs.AddException(ex);
                    throw;
                }
                finally
                {
                    _volumeProvider.Paused = false;
                }
            }
        }

        private static void Provider_VolumeChanged(object sender, DeviceVolumeChangedArgs e)
        {
            SyncVolume((CoreAudioDevice)e.Device);
        }
        public static void Clean()
        {
            lock (accessLock)
            {
                Logs.Add($"Cleaning...", false);
                if (Initialized)
                {
                    _deviceChangedDisposable.Dispose();
                    Tools.AudioController.Dispose();
                    foreach (var disposable in _volumeDisposables)
                        disposable.Value.Dispose();
                    foreach (var device in _audioDevices)
                        device.Dispose();
                }
                Logs.Add($"Cleaned", false);

            }
        }


    }

}
