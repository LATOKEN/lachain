namespace NeoSharp.Core.Models.OperationManager
{
    public interface IVerifier<in T>
    {
        bool Verify(T obj);
    }
}
