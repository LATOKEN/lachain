namespace Lachain.Storage.DbCompact
{
    public static class DbShrinkUtils
    {
        public static int counter = DbUpdatePeriod();

        public static int DbUpdatePeriod()
        {
            return 10000; // must be a positive integer;
        }

        public static void ResetCounter()
        {
            counter = DbUpdatePeriod();
        }

        public static void UpdateCounter()
        {
            counter--;
        }

        public static bool CycleEnded()
        {
            return counter <= 0;
        }

        public static void Save(RocksDbAtomicWrite batch, byte[] key, byte[] content, bool tryCommit = true)
        {
            batch.Put(key,content);
            UpdateCounter();
            if (tryCommit && CycleEnded())
            {
                Commit(batch);
            }
        }

        public static void Delete(RocksDbAtomicWrite batch, byte[] key, bool tryCommit = true)
        {
            batch.Delete(key);
            UpdateCounter();
            if (tryCommit && CycleEnded())
            {
                Commit(batch);
            }
        }

        public static void Commit(RocksDbAtomicWrite batch)
        {
            batch.Commit();
            ResetCounter();
        }
    }
}