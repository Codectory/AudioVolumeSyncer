using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioVolumeSyncer
{
    public static class AudioSyncHelper
    {
        private static bool _cancelAudioDeviceUpdatesRequested = false;
        private static Thread _audioDeviceUpdates = null;
        readonly static object _lockAudioUpdateThread = new object();

        private static FastObservableCollection<AudioDeviceSetting> _syncAudioDevices = new FastObservableCollection<AudioDeviceSetting>();

        public static FastObservableCollection<AudioDeviceSetting> AudioDevices { get => _syncAudioDevices; }

        public static bool AudioDeviceUpdatesRunning { get; private set; } = false;

        public static CoreAudioController AudioController = new CoreAudioController();


        public static void StartAudioDevicesUpdates(bool updateSynchronBeforeStart = true)
        {
            lock (_lockAudioUpdateThread)
            {
                if (AudioDeviceUpdatesRunning)
                    return;
                _audioDeviceUpdates = new Thread(UpdateAudioDevicesLoop);
                _audioDeviceUpdates.IsBackground = true;
                AudioDeviceUpdatesRunning = true;
                if (updateSynchronBeforeStart)
                    UpdateAudioDevices();
                _audioDeviceUpdates.Start();
            }
        }

        public static void StopAudioDeviceUpdates()
        {
            lock (_lockAudioUpdateThread)
            {
                if (!AudioDeviceUpdatesRunning)
                    return;
                _cancelAudioDeviceUpdatesRequested = true;
                _audioDeviceUpdates.Join();
                _cancelAudioDeviceUpdatesRequested = false;
                AudioDeviceUpdatesRunning = false;
            }
        }

        private static void UpdateAudioDevicesLoop()
        {
            while (_cancelAudioDeviceUpdatesRequested)
            {
                UpdateAudioDevices();
                Thread.Sleep(100);
            }
        }

        private static void UpdateAudioDevices()
        {
            IEnumerable<CoreAudioDevice> devices = AudioController.GetDevices(DeviceType.Playback);
            foreach (CoreAudioDevice device in devices)
                if (!AudioDevices.Any(d => d.Device.Equals(device)))
                    AudioDevices.Add(new AudioDeviceSetting(device, false));
            List<AudioDeviceSetting> toRemove = new List<AudioDeviceSetting>();
            foreach (AudioDeviceSetting audioDeviceSetting in AudioDevices)
                if (!devices.Contains(audioDeviceSetting.Device))
                    AudioDevices.RemoveItems(toRemove);
        }
    }
}
