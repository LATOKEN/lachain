using System;
using SimpleInjector;

namespace Phorkus.Core.DI.SimpleInjector
{
    public class SimpleInjectorContainer : IContainer
    {
        private readonly Container _container;

        internal SimpleInjectorContainer(Container container)
        {
            _container = container;
        }

        public object Resolve(Type serviceType)
        {   
            return _container.GetInstance(serviceType);
        }

        public TEntity Resolve<TEntity>() where TEntity : class
        {
            return _container.GetInstance<TEntity>();
        }

        public bool TryResolve(Type parameterType, out object? obj)
        {
            var ret = _container.GetRegistration(parameterType);

            if (ret != null)
            {
                obj = ret.GetInstance();
                return true;
            }

            obj = null;
            return false;
        }

        public TEntity Factory<TEntity>() where TEntity : class
        {
            var registration = Lifestyle.Singleton.CreateRegistration<TEntity>(_container);
            _container.AddRegistration(typeof(TEntity), registration);
            return _container.GetInstance<TEntity>();
        }
    }
}
