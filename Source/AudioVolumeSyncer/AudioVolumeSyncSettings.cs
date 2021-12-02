using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using CodectoryCore.UI.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace AudioVolumeSyncer
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AudioVolumeSyncSettings : BaseViewModel
    {
        public static readonly object _settingsLock = new object();
        private bool logging = false;
        private bool autoStart;
        private bool startMinimizedToTray;
        private bool _closeToTray;
        readonly object _audioDevicesLock = new object();
        private ObservableCollection<Guid> _syncAudioDevicesGuids;


        [JsonProperty]
        public bool AutoStart { get => autoStart; set { autoStart = value; OnPropertyChanged(); } }

        [JsonProperty]
        public bool Logging { get => logging; set { logging = value; OnPropertyChanged(); } }


        [JsonProperty]
        public bool StartMinimizedToTray { get => startMinimizedToTray; set { startMinimizedToTray = value; OnPropertyChanged(); }  }

        [JsonProperty]
        public bool CloseToTray { get => _closeToTray; set { _closeToTray = value; OnPropertyChanged(); } }

        [JsonProperty]
        public ObservableCollection<Guid> GuidsOfSyncedAudioDevices { get => _syncAudioDevicesGuids; set { _syncAudioDevicesGuids = value; OnPropertyChanged(); } }

        public RelayCommand<Tuple<object, object>> AudioSyncDeviceChangedCommand { get; private set; }




        public AudioVolumeSyncSettings()
        {
            GuidsOfSyncedAudioDevices = new ObservableCollection<Guid>();
            GuidsOfSyncedAudioDevices.CollectionChanged += SyncAudioDevicesGuids_CollectionChanged;
            InitializeRelayCommands();
        }

        private void InitializeRelayCommands()
        {
            AudioSyncDeviceChangedCommand = new RelayCommand<Tuple<object, object>>(AudioSyncDeviceChanged);

        }

        private void AudioSyncDeviceChanged(Tuple<object, object> deviceTuple)
        {
            lock (_audioDevicesLock)
            {
                if ((bool)deviceTuple.Item2)
                    GuidsOfSyncedAudioDevices.Add(((AudioDeviceSetting)deviceTuple.Item1).Device.Id);
                else
                    GuidsOfSyncedAudioDevices.Remove(((AudioDeviceSetting)deviceTuple.Item1).Device.Id);
            }
        }


        private void SyncAudioDevicesGuids_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var guid in e.NewItems)
                    {
                        var audioDevice = AudioSyncHelper.AudioDevices.FirstOrDefault(d => d.Device.Id.Equals(guid));
                            if (audioDevice != null)
                            audioDevice.Sync = true;
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var guid in e.OldItems)
                    {
                        var audioDevice = AudioSyncHelper.AudioDevices.FirstOrDefault(d => d.Device.Id.Equals(guid));
                        if (audioDevice != null)
                            audioDevice.Sync = false;
                    }
                    break;
            }
        }

        public static AudioVolumeSyncSettings ReadSettings(string path)
        {
            AudioVolumeSyncSettings settings = null;

            lock (_settingsLock)
            {

                try
                {
                    string serializedJson = File.ReadAllText(path);
                    settings = (AudioVolumeSyncSettings)JsonConvert.DeserializeObject<AudioVolumeSyncSettings>(serializedJson, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Objects,
                        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
                    });
                }
                catch (Exception ex)
                {
                    Globals.Logs.AddException(ex);
                    throw;
                }
            }
            return settings;
        }

        public static void SaveSettings(AudioVolumeSyncSettings settings, string path)
        {
            lock (_settingsLock)
            {
                try
                {
                    string serializedJson = JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Objects,
                        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
                    });
                    File.WriteAllText(path, serializedJson);
                }
                catch (Exception ex)
                {
                    Globals.Logs.AddException(ex);
                    throw;
                }
            }
        }
    }

    public static class AudioVolumeSyncSettingsExtension
    {

        public static void SaveSettings(this AudioVolumeSyncSettings settings, string path)
        {

            AudioVolumeSyncSettings.SaveSettings(settings, path);

        }

    }

}
