using System;

namespace Lachain.Core.RPC
{
    public interface IMetricsService : IDisposable
    {
        void Start();
    }
}