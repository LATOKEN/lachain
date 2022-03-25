using System.Runtime.CompilerServices;

namespace Lachain.Storage
{
    /*
        All the trie nodes are numbered incrementally starting from 1. This number is the version
        of that node. VersionFactory keeps a global counter and generates new version by incrementing
        the counter. 
    */
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