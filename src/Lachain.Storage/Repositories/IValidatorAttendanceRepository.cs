namespace Lachain.Storage.Repositories
{
    public interface IValidatorAttendanceRepository
    {
        void SaveState(byte[] state);
        byte[] LoadState();
    }
}