using System;

namespace Phorkus.Storage.State
{
    public interface IStateManager : ISnapshotManager<IBlockchainSnapshot>
    {
        void SafeContext(Action callback);
        
        TR SafeContext<TR>(Func<TR> callback);
        
        void Acquire();

        void Release();
    }
}