using CodectoryCore;
using CodectoryCore.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioVolumeSyncer
{
    public static class Globals
    {
        public static Logs Logs = new Logs($"{System.AppDomain.CurrentDomain.BaseDirectory}AudioVolumeSyncer.log", "Audio Volume Syncer", VersionExtension.ApplicationVersion(System.Reflection.Assembly.GetExecutingAssembly()).ToString(), false);

    }
}
