using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Hermes
{
    public interface IPartyBuilder
    {
        PartyState CurrentState { get; }

        void Initialize();

        BgwPublicParams GenerateShare();

        void CollectShare(IReadOnlyCollection<BgwPublicParams> shares);

        BgwnPoint GeneratePoint();

        void CollectPoint(IReadOnlyCollection<BgwnPoint> points);
        
        // other states here
        
        void Finalize();
    }
}