using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Phorkus.Hermes.Generator.Messages;
using Phorkus.Hermes.Generator.State;
using Phorkus.Hermes.Math;
using Phorkus.Proto;


namespace Phorkus.Hermes.Generator
{
    public class DefaultGeneratorProtocol : IGeneratorProtocol
    {
        public static int N_PARTIES = 10; // Current implementation: works for 3 to 30 
        public static int T_THRESHOLD = 4; // Should be less than n/2
        public static int KEY_SIZE = 128; // Tested up to 512
        private SecureRandom sr = new SecureRandom();
        
        private Data participants;
        private static PublicKey publicKey; // todo: attention: analogue master in Java code
        private static ProtocolParameters protoParam;
        private BGWData bgwData;
        

        public DefaultGeneratorProtocol(Data participants)
        {
            IDictionary<PublicKey, int> indexMap = new Dictionary<PublicKey, int>(N_PARTIES);
            for(var i=1; i<=N_PARTIES; i++) {
                // пересылка по сети каждой стороне параметры протокола protoParam
                // from Java: indexMap.Add(system.actorOf(Props.create(ProtocolActor.class, protoParam),"Actor"+i),i);
            }
                        
            this.participants = participants;
            
            protoParam = ProtocolParameters.gen(KEY_SIZE, N_PARTIES, T_THRESHOLD, new SecureRandom()); 
        }
        
        public GeneratorState CurrentState { get; }

        public BiprimalityTestData Initialize()
        {
            BiprimalityTestData.init();
            //IDictionary<PublicKey, int> actors = participants;
            BigInteger gp = getGp(candidateN.N, 0);
            BigInteger Qi = getQi(gp,candidateN.N, candidateN.bgwPrivateParameters.pi, candidateN.bgwPrivateParameters.qi, actors.get(this.master));

        }

        public BgwPublicParams GenerateShare()
        {
            // 2. BGW
            {
                // 2.1 Generating private and public params
                INITILIZATION();               
            }
            
            {
                // 2.2 Generating and broadcasting public params for each participant
                // разослать всем кроме меня BGWPublicParameters    
            }
            
            {
                //2.3 Collecting public params from other participants
                // Collect the pji and qji shares and compute its own Ni share
                BGW_COLLECTING_PjQj(newShare,  sender);
                
                return GoTo(States.BGW_COLLECTING_Nj).using(dataWithNewShare.withNewNi(Ni, actors.get(this.master)));
                
            }
            throw new System.NotImplementedException();
        }

        public void CollectShare(IReadOnlyCollection<BgwPublicParams> shares)
        {
            throw new System.NotImplementedException();
        }

        public BGWNPoint GeneratePoint()
        {
            throw new System.NotImplementedException();
        }

        public void CollectPoint(IReadOnlyCollection<BGWNPoint> points)
        {
            throw new System.NotImplementedException();
        }

        public void Finalize()
        {
            throw new System.NotImplementedException();
        }

        private void INITILIZATION()
        {
            bgwData = BGWData.Init();
            
            // Generates new p, q and all necessary sharings
            var parties = participants.GetParticipants();
            var bgwPrivateParameters = BgwPrivateParams.genFor(parties[publicKey], protoParam, sr);
            var bgwSelfShare = BgwPublicParams.genFor(parties[publicKey], bgwPrivateParameters);
                
            var nextStateData = bgwData.WithPrivateParameters(bgwPrivateParameters)
                .WithNewShare(bgwSelfShare, parties[publicKey])
                .WithParticipants(parties); 
            
            // call transition between two states: INITILIZATION -> BGW_COLLECTING_PjQj
            //return goTo(States.BGW_COLLECTING_PjQj).using(nextStateData);
        } 
        private void BGW_COLLECTING_PjQj(BgwPublicParams newShare, PublicKey sender)
        {
            var actors = participants.GetParticipants();

            //2.3 Collecting public params from other participants
            // Collect the pji and qji shares and compute its own Ni share

            var dataWithNewShare = bgwData.WithNewShare(newShare, actors[sender]);

            if (!dataWithNewShare.HasShareOf(actors.Values))
                // return Stay().Using(dataWithNewShare); todo Stay() - это Akka - убрать!
                ;
            else
            {
                var badActors = dataWithNewShare.shares()
                    .Where(e => !e.Value.isCorrect(protoParam))
                    .Select(e => e.Key); // Check the shares (not implemented yet)

                if (badActors.Any())
                {
                    // todo: отправка сообщений пример ниже - переписать
//                    badActors.ForEach(id => broadCast(new Messages.Complaint(id), actors.Keys));
//                    return Stop(new Failure("A BGW share was invalid."));
                }

                var sumPj = dataWithNewShare.shares().Select(e => e.Value.pij)
                    .Aggregate(BigInteger.Zero, (p1, p2) => p1.Add(p2));
                var sumQj = dataWithNewShare.shares().Select(e => e.Value.qij)
                    .Aggregate(BigInteger.Zero, (p1, p2) => p1.Add(p2));
                var sumHj = dataWithNewShare.shares().Select(e => e.Value.hij)
                    .Aggregate(BigInteger.Zero, (p1, p2) => p1.Add(p2));
                var Ni = sumPj.Multiply(sumQj).Add(sumHj).Mod(protoParam.P);
                
                // todo: отправка сообщений пример ниже - переписать
                //return GoTo(States.BGW_COLLECTING_Nj).using (dataWithNewShare.withNewNi(Ni, actors.get(this.master))) ;
            }
        }

        private void BGW_COLLECTING_Nj(BGWNPoint newNi)
        {
            // Collect the Nj shares and compute N using Lagrangian interpolation
            var actors = participants.GetParticipants();

            var a1 = participants as BGWData;
            
                
                
                
            BGWData dataWithNewNi = a1.withNewNi(newNi.point, actors.get(sender()));
            if (!dataWithNewNi.hasNiOf(actors.values())) {
                return stay().using(dataWithNewNi);
            } else {
                List<BigInteger> Nis = dataWithNewNi.nis()
                    .map(e -> e.getValue())
                    .collect(Collectors.toList());
                BigInteger N = IntegersUtils.getIntercept(Nis, protocolParameters.P);

                if (this.master != self())
                    this.master.tell(new Messages.CandidateN(N, data.bgwPrivateParameters), self());
            }

            return goTo(States.INITILIZATION).using(BGWData.init().withParticipants(data.getParticipants()));

        }
        
        
        

        private BigInteger getGp(BigInteger N, int round) {
		
            int hash = N.GetHashCode()*(round+1);
		
            BigInteger candidateGp = BigInteger.ValueOf(hash).Abs();
		
            while(IntegersUtils.Jacobi(candidateGp, N) != 1)
                candidateGp = candidateGp.Add(BigInteger.One);
		
            return candidateGp;
        }
        
        private BigInteger getQi(BigInteger gp, BigInteger N, BigInteger pi, BigInteger qi, int i) {
            BigInteger four = BigInteger.ValueOf(4);
            BigInteger exp;
            if (i == 1) {
                exp = N.Add(BigInteger.One).Subtract(pi).Subtract(qi).Divide(four);
            } else {
                exp = pi.Add(qi).Divide(four);
            }
            return gp.ModPow(exp, N);
        }
    }
}