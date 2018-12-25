namespace Phorkus.Storage
{
    public class VersionFactory
    {
        public VersionFactory(ulong initial)
        {
            CurrentVersion = initial;
        }

        public ulong CurrentVersion { get; private set; }

        public ulong NewVersion()
        {
            lock (this)
            {
                CurrentVersion++;
                return CurrentVersion;
            }
        }
    }
}