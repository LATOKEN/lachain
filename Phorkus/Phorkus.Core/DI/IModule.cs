namespace Phorkus.Core.DI
{
    public interface IModule
    {
        void Register(IContainerBuilder containerBuilder);
    }
}