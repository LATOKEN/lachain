using System;

namespace Phorkus.Core.DI
{
    public interface IContainer
    {
        object Resolve(Type serviceType);

        TEntity Resolve<TEntity>() where TEntity : class;

        bool TryResolve(Type parameterType, out object obj);

        TEntity Factory<TEntity>() where TEntity : class;
    }
}