namespace Phorkus.Core.CrossChain
{
    public interface ICrossChain
    {
        bool IsWorking { get; }

        void Start();

        void Stop();
    }
}