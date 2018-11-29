using System.Collections.Generic;

namespace Phorkus.Hermes.Generator
{
    public class DefaultGeneratorProtocol : IGeneratorProtocol
    {
        public GeneratorState CurrentState { get; }

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