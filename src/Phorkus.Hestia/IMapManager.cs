namespace Phorkus.Hestia
{
    public interface IMapManager
    {
        void Checkoint(ulong root);
        void ClearCaches();
        ulong Add(ulong root, byte[] key, byte[] value);
        ulong AddOrUpdate(ulong root, byte[] key, byte[] value);
        ulong Update(ulong root, byte[] key, byte[] value);
        ulong Delete(ulong root, byte[] key, out byte[] value);
        ulong TryDelete(ulong root, byte[] key, out byte[] value);
        byte[] Find(ulong root, byte[] key);
    }
}