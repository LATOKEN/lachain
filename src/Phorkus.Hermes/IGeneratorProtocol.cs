using System.Collections.Generic;
using Phorkus.Hermes.Generator;
using Phorkus.Hermes.Generator.Messages;
using Phorkus.Hermes.Generator.State;

namespace Phorkus.Hermes
{
    public interface IGeneratorProtocol
    {
        GeneratorState CurrentState { get; }

        void Initialize();

        BgwPublicParams GenerateShare();

        void CollectShare(IReadOnlyCollection<BgwPublicParams> shares);

        BGWNPoint GeneratePoint();

        void CollectPoint(IReadOnlyCollection<BGWNPoint> points);
        
        // other states here
        
        void Finalize();
    }
}