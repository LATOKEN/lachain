using System.Runtime.CompilerServices;

namespace Lachain.Storage
{
    public class VersionFactory
    {
        public VersionFactory(ulong initial)
        {
            CurrentVersion = initial;
        }

        public ulong CurrentVersion { get; private set; }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong NewVersion()
        {
            CurrentVersion++;
            return CurrentVersion;
        }
    }
}