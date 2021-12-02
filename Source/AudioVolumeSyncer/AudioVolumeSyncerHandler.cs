using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using CodectoryCore;
using CodectoryCore.Logging;
using CodectoryCore.UI.Wpf;
using CodectoryCore.Windows;
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
using System.IO;
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


        static IDisposable _deviceChangedDisposable;
        static Dictionary<CoreAudioDevice, IDisposable> _volumeDisposables = new Dictionary<CoreAudioDevice, IDisposable>();
        static private VolumeProvider _volumeProvider;
        static private AudioMasterChangedProvider _masterChangedProvider;
        static readonly object accessLock = new object();
        static readonly object masterLock = new object();
        static List<CoreAudioDevice> _audioDevices = new List<CoreAudioDevice>();

        public static event EventHandler DevicesChanged;

        public static bool Initialized { get; private set; } = false;

        public static IReadOnlyCollection<CoreAudioDevice> Devices => AudioSyncHelper.AudioController.GetDevices(DeviceType.Playback).ToList().AsReadOnly();



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

        public string Version
        {
            get
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string version = assembly.GetName().Version.ToString();
                version = version.Substring(0, version.LastIndexOf('.'));
                return version;
            }
        }


        public AudioVolumeSyncerHandler()
        {
            // ChangeLanguage( new System.Globalization.CultureInfo("en-US"));
            Logs.LoggingEnabled = true;
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
                Globals.Logs.Add("Initializing...", false);
                AudioSyncHelper.StartAudioDevicesUpdates();

                InitializeAudioObjects();

                LoadSettings();
                InitializeTrayMenu();
                CreateRelayCommands();
                SwitchTrayIcon(Settings.StartMinimizedToTray);
                ShowView = !Settings.StartMinimizedToTray;
       
                Initialized = true;
                Globals.Logs.Add("Initialized", false);

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
                    _deviceChangedDisposable = AudioSyncHelper.AudioController.AudioDeviceChanged.Subscribe(_masterChangedProvider);
                }
                catch (Exception ex)
                {
                    Globals.Logs.AddException(ex);
                    throw;
                }
            }
        }


        private void LoadSettings()
        {
            Globals.Logs.Add("Loading settings..", false);
            try
            {
                if (File.Exists(SettingsPath))
                {
                    Globals.Logs.Add("Loading settings...", false);
                    Settings = AudioVolumeSyncSettings.ReadSettings(SettingsPath);
                }
                else
                {
                    Globals.Logs.Add("Creating settings file", false);

                    Settings = new AudioVolumeSyncSettings();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                string backupFile = $"{System.AppDomain.CurrentDomain.BaseDirectory}AudioVolumeSyncer_Settings_{DateTime.Now.ToString("yyyyMMddHHmmss")}.xml.bak";
                File.Move(SettingsPath, backupFile);
                Globals.Logs.Add($"Created backup of invalid settings file: {backupFile}", false);
                File.Delete(SettingsPath);
                Globals.Logs.Add("Failed to load settings", false);
                Globals.Logs.AddException(ex);
                Settings = new AudioVolumeSyncSettings();
                SaveSettings();
                Globals.Logs.Add("Created new settings file", false);


            }
            List<CoreAudioDevice> devicesToDelete = new List<CoreAudioDevice>();
            foreach (var guid in Settings.GuidsOfSyncedAudioDevices)
            {
                var audioDevice = AudioSyncHelper.AudioController.GetDevice((Guid)guid);
                if (audioDevice != null)
                    AddAudioDevice(audioDevice);
                else
                    devicesToDelete.Add(audioDevice);

            }
            if (devicesToDelete.Count > 0)
            {
                foreach (var device in devicesToDelete)
                    Settings.GuidsOfSyncedAudioDevices.Remove(device.Id);
                SaveSettings();
            }
            Logs.LoggingEnabled = Settings.Logging;
            settings.GuidsOfSyncedAudioDevices.CollectionChanged += SyncAudioDevicesGuids_CollectionChanged;
            settings.PropertyChanged += Settings_PropertyChanged;
            Globals.Logs.Add("Settings loaded", false);
        }



        private void SyncAudioDevicesGuids_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            lock (accessLock)
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                    foreach (var guid in e.NewItems)
                        AddAudioDevice(AudioSyncHelper.AudioController.GetDevice((Guid)guid));
                else if (e.Action == NotifyCollectionChangedAction.Remove)
                    foreach (var guid in e.OldItems)
                        RemoveAudioDevice(AudioSyncHelper.AudioController.GetDevice((Guid)guid));
            }
        }

        private void InitializeTrayMenu()
        {
            Globals.Logs.Add("Initializing tray menu", false);
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
                Globals.Logs.Add("Tray menu initialized", false);

            }
            catch (Exception ex)
            {
                Globals.Logs.AddException(ex);
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
                if (settings.AutoStart)
                    AutoStart.Activate(Locale_Texts.AudioVolumeSyncer, System.Reflection.Assembly.GetEntryAssembly().Location);
                else
                    AutoStart.Deactivate(Locale_Texts.AudioVolumeSyncer, System.Reflection.Assembly.GetEntryAssembly().Location);
                Logs.LoggingEnabled = Settings.Logging;
                SaveSettings();
            }
        } 



        private void TrayMenu_TrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            Globals.Logs.Add("Open app from Tray", false);
            SwitchTrayIcon(false);
            ShowView = true;

        }

        private void SaveSettings()
        {
            Globals.Logs.Add("Saving settings..", false);
            try
            {
                Settings.SaveSettings(SettingsPath);
                Globals.Logs.Add("Settings saved", false);

            }
            catch (Exception ex)
            {
                Globals.Logs.AddException(ex);
                throw;
            }
        }




        private void Closing()
        {
            if (Settings.CloseToTray)
            {
                Globals.Logs.Add($"Minimizing to tray...", false);
                SwitchTrayIcon(true);
            }
            else
            {
                Globals.Logs.Add($"Shutting down...", false);
                Shutdown();
            }
        }

        private void Shutdown()
        {
            Clean();
            SwitchTrayIcon(false);
            AudioSyncHelper.StopAudioDeviceUpdates();
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
                    Globals.Logs.Add($"Slave device added: {device.FullName} - {device.Id}", false);

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
                    Globals.Logs.Add($"Slave device removed: {device.FullName} - {device.Id}", false);
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
            //        Globals.Logs.Add($"New master: {MasterAudioDevice.FullName} - {MasterAudioDevice.Id}", false);

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
                    Globals.Logs.Add($"Volume changed - Muted: {muted} New volume: {changedDevice.Volume} Device: {changedDevice.FullName} - {changedDevice.Id}", false);
                    foreach (CoreAudioDevice slaveDevice in _audioDevices.ToList())
                    {
                        if (!muted)
                            slaveDevice.SetVolumeAsync(volume).Wait();
                        slaveDevice.SetMuteAsync(muted).Wait();
                    }
                }
                catch (Exception ex)
                {
                    Globals.Logs.AddException(ex);
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
                Globals.Logs.Add($"Cleaning...", false);
                if (Initialized)
                {
                    _deviceChangedDisposable.Dispose();
                    AudioSyncHelper.AudioController.Dispose();
                    foreach (var disposable in _volumeDisposables)
                        disposable.Value.Dispose();
                }
                Globals.Logs.Add($"Cleaned", false);

            }
        }


    }

}
