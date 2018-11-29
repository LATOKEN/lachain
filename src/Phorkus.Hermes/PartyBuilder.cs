using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Hermes
{
    public class PartyBuilder : IPartyBuilder
    {
        public PartyState CurrentState { get; }
        
        public void Initialize()
        {
            throw new System.NotImplementedException();
        }

        public BgwPublicParams GenerateShare()
        {
            throw new System.NotImplementedException();
        }

        public void CollectShare(IReadOnlyCollection<BgwPublicParams> shares)
        {
            throw new System.NotImplementedException();
        }

        public BgwnPoint GeneratePoint()
        {
            throw new System.NotImplementedException();
        }

        public void CollectPoint(IReadOnlyCollection<BgwnPoint> points)
        {
            throw new System.NotImplementedException();
        }

        public void Finalize()
        {
            throw new System.NotImplementedException();
        }
    }
}