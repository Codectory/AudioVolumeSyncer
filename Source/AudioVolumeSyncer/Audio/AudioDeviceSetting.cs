using AudioSwitcher.AudioApi.CoreAudio;
using CodectoryCore.UI.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioVolumeSyncer
{
    public class AudioDeviceSetting : BaseViewModel
    {
        private CoreAudioDevice _device = null;
        private bool _sync = false;

        public AudioDeviceSetting(CoreAudioDevice device, bool sync)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            Sync = sync;
        }

        public CoreAudioDevice Device { get => _device;  set { _device = value; OnPropertyChanged(); } }
        public bool Sync { get => _sync; set { _sync = value; OnPropertyChanged(); } }
    }
}
