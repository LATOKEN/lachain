using System.Collections.Generic;
using Phorkus.Hermes.Generator;
using Phorkus.Hermes.Generator.Network;
using Phorkus.Hermes.Generator.State;

namespace Phorkus.Hermes
{
    public interface IGeneratorProtocol
    {
        GeneratorState CurrentState { get; }

        void Initialize();

        BgwPublicParams GenerateShare();

        void CollectShare(IReadOnlyCollection<BgwPublicParams> shares);

        Messages.BGWNPoint GeneratePoint();

        void CollectPoint(IReadOnlyCollection<Messages.BGWNPoint> points);
        
        // other states here
        
        void Finalize();
    }
}