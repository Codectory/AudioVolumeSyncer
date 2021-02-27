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

namespace AudioVolumeSyncer
{
    [DataContract]
    public class AudioVolumeSyncSettings : BaseViewModel
    {
        private bool logging = false;
        private bool autoStart;
        private bool startMinimizedToTray;
        private bool _closeToTray;
        readonly object _audioDevicesLock = new object();
        private ObservableCollection<Guid> _syncAudioDevicesGuids;


        [DataMember]
        public bool AutoStart { get => autoStart; set { autoStart = value; OnPropertyChanged(); } }
        
        [DataMember]
        public bool Logging { get => logging; set { logging = value; OnPropertyChanged(); } }


        [DataMember]
        public bool StartMinimizedToTray { get => startMinimizedToTray; set { startMinimizedToTray = value; OnPropertyChanged(); }  }

        [DataMember]
        public bool CloseToTray { get => _closeToTray; set { _closeToTray = value; OnPropertyChanged(); } }

        [DataMember]
        public ObservableCollection<Guid> GuidsOfSyncedAudioDevices { get => _syncAudioDevicesGuids; set { _syncAudioDevicesGuids = value; OnPropertyChanged(); } }


        [XmlIgnore]
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
                        var audioDevice = AudioSyncHelper.AudioDevices.First(d => d.Device.Id.Equals(guid));
                            if (audioDevice != null)
                            audioDevice.Sync = true;
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var guid in e.OldItems)
                    {
                        var audioDevice = AudioSyncHelper.AudioDevices.First(d => d.Device.Id.Equals(guid));
                        if (audioDevice != null)
                            audioDevice.Sync = false;
                    }
                    break;

            }
        }

        public static AudioVolumeSyncSettings ReadSettings(string path)
        {
            AudioVolumeSyncSettings settings = null;
            XmlSerializer serializer = new XmlSerializer(typeof(AudioVolumeSyncSettings));
            using (TextReader reader = new StreamReader(path))
            {
                settings = (AudioVolumeSyncSettings)serializer.Deserialize(reader);
            }
            List<Guid> toRemove = new List<Guid>();
            foreach (var guidToSync in settings.GuidsOfSyncedAudioDevices)
                if (!AudioSyncHelper.AudioDevices.Any(d => d.Device.Id.Equals(guidToSync)))
                    toRemove.Add(guidToSync);
            foreach (Guid guid in toRemove)
                settings.GuidsOfSyncedAudioDevices.Remove(guid);

            return settings;
        }
    }

    public static class AudioVolumeSyncSettingsExtension
    {

        public static void SaveSettings(this AudioVolumeSyncSettings setting, string path)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(AudioVolumeSyncSettings));
            using (TextWriter writer = new StreamWriter(path))
            {
                serializer.Serialize(writer, setting);
            }
        }

    }

}
