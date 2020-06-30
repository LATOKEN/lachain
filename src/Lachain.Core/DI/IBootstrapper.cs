using System;

namespace Lachain.Core.DI
{
    public interface IBootstrapper
    {
        void Start(string[] args);
    }
}