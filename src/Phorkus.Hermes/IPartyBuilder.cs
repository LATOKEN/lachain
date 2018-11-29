using Phorkus.Proto;

namespace Phorkus.Hermes
{
    public interface IPartyBuilder
    {
        PartyState CurrentState { get; }

        void Initialize();

        void GenerateShare();

        void CollectShare();

        void GeneratePoint();

        void CollectPoint();
        
        void Finalize();
    }
}