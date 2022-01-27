using System;

namespace Lachain.Storage.State
{
    public interface IStateManager : ISnapshotManager<IBlockchainSnapshot>
    {
        void SafeContext(Action callback);
        
        T SafeContext<T>(Func<T> callback);
        
        void Acquire();

        void Release();

        void Commit();
    }
}