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

        private bool autoStart;
        private bool startMinimizedToTray;
        private bool _closeToTray;
        readonly object _audioDevicesLock = new object();
        private ObservableCollection<Guid> _syncAudioDevicesGuids;
        private ObservableCollection<AudioDeviceSetting> _syncAudioDevices;


        [DataMember]
        public bool AutoStart { get => autoStart; set { autoStart = value; OnPropertyChanged(); } }

        [DataMember]
        public bool StartMinimizedToTray { get => startMinimizedToTray; set { startMinimizedToTray = value; OnPropertyChanged(); }  }

        [DataMember]
        public bool CloseToTray { get => _closeToTray; set { _closeToTray = value; OnPropertyChanged(); } }

        [DataMember]
        public ObservableCollection<Guid> SyncAudioDevicesGuids { get => _syncAudioDevicesGuids; set { _syncAudioDevicesGuids = value; OnPropertyChanged(); } }

        [XmlIgnore]
        public ObservableCollection<AudioDeviceSetting> SyncAudioDevices { get => _syncAudioDevices; set { _syncAudioDevices = value; OnPropertyChanged(); } }

        [XmlIgnore]
        public RelayCommand<Tuple<object, object>> AudioSyncDeviceChangedCommand { get; private set; }




        public AudioVolumeSyncSettings()
        {
            SyncAudioDevicesGuids = new ObservableCollection<Guid>();
            SyncAudioDevicesGuids.CollectionChanged += SyncAudioDevicesGuids_CollectionChanged;
            SyncAudioDevices = new ObservableCollection<AudioDeviceSetting>();
            foreach (CoreAudioDevice device in Tools.AudioController.GetDevices(DeviceType.Playback))
                SyncAudioDevices.Add(new AudioDeviceSetting(device, false));
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
                    SyncAudioDevicesGuids.Add(((AudioDeviceSetting)deviceTuple.Item1).Device.Id);
                else
                    SyncAudioDevicesGuids.Remove(((AudioDeviceSetting)deviceTuple.Item1).Device.Id);
            }
        }


        private void SyncAudioDevicesGuids_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var guid in e.NewItems)
                        SyncAudioDevices.First(d => d.Device.Id.Equals(guid)).Sync = true;
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var guid in e.OldItems)
                        SyncAudioDevices.First(d => d.Device.Id.Equals(guid)).Sync = false;
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
