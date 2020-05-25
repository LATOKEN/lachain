using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.Messages;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Crypto.TPKE;
using Lachain.Proto;
using Phorkus.Consensus.ReliableBroadcast;

namespace Lachain.Consensus.ReliableBroadcast
{
    public class ReliableBroadcast : AbstractProtocol
    {
        private readonly ReliableBroadcastId _broadcastId;
        private readonly CommonSubsetId _commonSubsetId;
        
        
        private Dictionary<int, List<int>> _receivedBatchesOfBlocks;
        private Dictionary<int, bool> _receivedCorrectEchoFrom;
        private Dictionary<int, bool> _receivedCorrectReadyFrom;
        private  List<int> _fromWhomReceived;

        private List<int> _store;
        private EncryptedShare? _result;
        private List<UInt256> _receivedRoots;
        private ResultStatus _requested;
        //private BoolSet? _result;
        private ErasureCoding _erasureCoding;

        private readonly bool[] _sentReadyMsg;
        private int _countValMsg;
        private int _countCorrectECHOMsg;
        private int _countReadyMsg;

        private static int _cntRBC;

        // debug flags and variables
        private readonly bool _flagRBCCountThreadDebug = true;

        private static void CntRbc()
        {
            _cntRBC++;
        }

        public ReliableBroadcast(
            ReliableBroadcastId broadcastId, IPublicConsensusKeySet wallet, IConsensusBroadcaster broadcaster) :
            base(wallet, broadcastId, broadcaster)
        {
            if (_flagRBCCountThreadDebug)
            {
                CntRbc();
                Console.Error.WriteLine(
                    "Thread {0} ID {1} _cntRBC = {2}", Thread.CurrentThread.ManagedThreadId, GetMyId(), _cntRBC);
            }

            _broadcastId = broadcastId;

            _receivedBatchesOfBlocks = new Dictionary<int, List<int>>();
            _receivedCorrectEchoFrom = new Dictionary<int, bool>();
            _receivedCorrectEchoFrom[GetMyId()] = true;
            _receivedCorrectReadyFrom = new Dictionary<int, bool>();
            for (var i = 0; i < N; i++)
            {
                _receivedBatchesOfBlocks[i] = new List<int>();
                _receivedCorrectEchoFrom[i] = false;
                _receivedCorrectReadyFrom[i] = false;
            }
            
            // It's store for tips
            _fromWhomReceived = new List<int>();

            _store = new List<int>();

            // additional bits in Erasure Coding = the count of players (N)
            var coeff = 1; // may be var to decrease 0.5 
            _erasureCoding = new ErasureCoding(N * coeff);

            _receivedRoots = new List<UInt256>();
            _sentReadyMsg = new bool[N];
            _result = null;

            _countValMsg = 0;
            _countCorrectECHOMsg = 0;
            _countReadyMsg = 0;
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;

                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.ValMessage:
                        HandleValMessage(message.ValMessage);
                        break;
                    case ConsensusMessage.PayloadOneofCase.EchoMessage:
                        HandleEchoMessage(message.EchoMessage, envelope.ValidatorIndex);
                        break;
                    case ConsensusMessage.PayloadOneofCase.ReadyMessage:
                        HandleReadyMessage(message.ReadyMessage, envelope.ValidatorIndex);
                        break;
                    default:
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to ReliableBroadcast protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {
                    case ProtocolRequest<ReliableBroadcastId, EncryptedShare> broadcastRequested:
                        HandleInputMessage(broadcastRequested);
                        break;
                    case ProtocolResult<ReliableBroadcastId, EncryptedShare> _:
                        Terminate();
                        break;
                    default:    
                        throw new InvalidOperationException(
                            "Binary broadcast protocol handles not any internal messages");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<ReliableBroadcastId, EncryptedShare> broadcastRequested)
        {
            if (N == 1)
            {
                _result = broadcastRequested.Input;
                _requested = ResultStatus.Requested;
                CheckResult();
            }
            
            Console.Error.WriteLine(
                "Thread {0} ID {1} was called HandleInputMessage()", Thread.CurrentThread.ManagedThreadId, GetMyId());
            
            if (broadcastRequested.Input == null) 
                return;
            
            var realInput = broadcastRequested.Input.ToByte();
            for (var indexAddressee = 0; indexAddressee < N; indexAddressee++)
            {
                Broadcaster.SendToValidator(CreateValMessage(RBTools.ByteToInt(realInput), indexAddressee), indexAddressee);
                Console.Error.WriteLine("Thread {0} ID {1} created VAL_TYPE and sent to ID {2}",
                    Thread.CurrentThread.ManagedThreadId, GetMyId(), indexAddressee);
            }
        }

        private UInt256 RecalculateMerkleRoot(List<int> goodBlocks)
        {
            var newHashes = new List<UInt256>();
            foreach (var block in goodBlocks)
            {
                newHashes.Add(BitConverter.GetBytes(block).Keccak());
            }
            return MerkleTree.ComputeRoot(newHashes);
        }

        private void HandleValMessage(ValMessage val)
        {
            Console.Error.WriteLine("Thread {0} ID {1} broadcast ECHO_TYPE",
                Thread.CurrentThread.ManagedThreadId, GetMyId());
            Broadcaster.Broadcast(CreateECHOMessage(val));
        }

        private void HandleEchoMessage(ECHOMessage echo, int validator)
        {   
            Console.Error.WriteLine(
                "Thread # {0} ID {1} mark1",
                Thread.CurrentThread.ManagedThreadId, GetMyId());
            if (_receivedCorrectEchoFrom[validator])
            {
                Console.Error.WriteLine(
                    "Thread # {0} ID {1} return from HandleEchoMessage",
                    Thread.CurrentThread.ManagedThreadId, GetMyId());
                return;    
            }
            
            
            var (flag, goodBatchOfBlocks, receivedRoots) = CheckEchoMsgNew(echo);

            if (flag)
            {   
                _receivedBatchesOfBlocks[echo.IndexAddressee] = goodBatchOfBlocks;
                _receivedRoots = receivedRoots;
                if (!_receivedCorrectEchoFrom[validator])
                {
                    _receivedCorrectEchoFrom[validator] = true;
                    _countCorrectECHOMsg++;
                }

                Console.Error.WriteLine(
                    "Thread # {0} ID {1} _countCorrectECHOMsg = {2} from {3}",
                    Thread.CurrentThread.ManagedThreadId, GetMyId(), _countCorrectECHOMsg, validator);
            }
            else
            {
                Console.Error.WriteLine(
                    "Thread # {0} ID {1} received BadBlock",
                    Thread.CurrentThread.ManagedThreadId, GetMyId());
            }
            
            if (_countCorrectECHOMsg == N - F)
            {
                var recalculatedRootsToSend = new List<UInt256>();
                var segmentSize = echo.Settings.LengthSegment;
                var countStripes = echo.Settings.CountStripes;

                foreach (var VARIABLE in _receivedBatchesOfBlocks.Where(
                    VARIABLE => VARIABLE.Value.Count == 0))
                {
                    for (var i = 0; i < segmentSize; i++)
                    {
                        _fromWhomReceived.Add(VARIABLE.Key * segmentSize + i);    
                    }
                }
                
                for (var numberStripes = 0; numberStripes < countStripes; numberStripes++)
                {
                    var accumulateCurrentStripe = new List<int>();
                    foreach (var batch in _receivedBatchesOfBlocks)
                    {
                        for (var j = 0; j < segmentSize; j++)
                        {
                            if (batch.Value.Count == 0)
                            {
                                accumulateCurrentStripe.Add(-11);
                            }
                            else
                            {
                                var indSelectBlock = numberStripes * segmentSize + j;
                                accumulateCurrentStripe.Add(batch.Value[indSelectBlock]);
                            }
                        }
                    }

                    var accumulateCurrentStripeArray = accumulateCurrentStripe.ToArray();
                    RBTools.Print(accumulateCurrentStripeArray);
                    
                    var currentDecodeStripe = DecodeNew(accumulateCurrentStripeArray.ToList(), _fromWhomReceived.ToArray());
                    var againEncodeCurrentStripe = Encode(currentDecodeStripe.Take(currentDecodeStripe.Count - F).ToList());
                    var currentNewHashes = RecalculateMerkleRoot(againEncodeCurrentStripe);

                    if (receivedRoots[numberStripes].Equals(currentNewHashes))
                    {
                        recalculatedRootsToSend.Add(currentNewHashes);
                    }
                    else
                    {
                        Abort();
                    }
                    
                    foreach (var item in currentDecodeStripe.Take(N - F))
                    {
                        _store.Add(item);
                    }
                }

                // debug
                Console.Error.WriteLine(
                    "Thread # {0} ID {1} mark2",
                    Thread.CurrentThread.ManagedThreadId, GetMyId());
                if (echo.Plaintext.SequenceEqual(_store))
                {
                    Console.Error.WriteLine(
                        "Thread # {0} ID {1} plaintext == _store into HandleEchoMessage()",
                        Thread.CurrentThread.ManagedThreadId, GetMyId());
                }
                else
                {
                    Console.Error.WriteLine(
                        "Thread # {0} ID {1} plaintext != _store into HandleEchoMessage() -- PROBLEM",
                        Thread.CurrentThread.ManagedThreadId, GetMyId());
                    
                }
                // debug

                if (receivedRoots.SequenceEqual(recalculatedRootsToSend))
                {
                    if (!_sentReadyMsg[GetMyId()])
                    {
                        Broadcaster.Broadcast(CreateReadyMessage(recalculatedRootsToSend, echo.Plaintext.ToArray()));
                        _sentReadyMsg[GetMyId()] = true;
                        Console.Error.WriteLine(
                            "Thread #{0} ID {1} broadcast ReadyMessageType from HandleEchoMessage()",
                            Thread.CurrentThread.ManagedThreadId, GetMyId());
                    }
                }
                else
                {
                    Abort();
                }
            }
        }

        private void HandleReadyMessage(ReadyMessage readyMessage, int validator)
        {
            if (_receivedCorrectReadyFrom[validator]) 
                return;
            
            var rootsToCheck = readyMessage.RootMerkleTree.ToList();
            var plaintext = readyMessage.Plaintext.ToArray();
            if (CheckReadyMsg(rootsToCheck))
            {
                if (!_receivedCorrectReadyFrom[validator])
                {
                    _receivedCorrectReadyFrom[validator] = true;
                    _countReadyMsg++;
                    Console.Error.WriteLine(
                        "Thread # {0} ID {1} _countReadyMsg = {2}",
                        Thread.CurrentThread.ManagedThreadId, GetMyId(), _countReadyMsg);
                }
                if (_countReadyMsg == F + 1)
                {
                    if (!_sentReadyMsg[GetMyId()])
                    {
                        Broadcaster.Broadcast(CreateReadyMessage(rootsToCheck, plaintext));
                        _sentReadyMsg[GetMyId()] = true;
                        Console.Error.WriteLine(
                            "Thread #{0} ID {1} broadcast ReadyMessageType HandleReadyMessage()",
                            Thread.CurrentThread.ManagedThreadId, GetMyId());
                    }
                }
                // else if (_countReadyMsg == 2 * F + 1)
                // {
                //     if (_countCorrectECHOMsg == N - 2 * F)
                //     {
                //         // todo: может влияет на silent case
                //         //Here should be decode if operations of interpolation
                //         //from blocks and decode could be make in different place
                //     }
                // }

                
                
                // todo: debug
                if (plaintext.SequenceEqual(_store))
                {   
                    
                    Console.Error.WriteLine(
                        "Thread # {0} ID {1} plaintext = _store into HandleReadyMessage()",
                        Thread.CurrentThread.ManagedThreadId, GetMyId());
                }
                else
                {
                    Console.Error.WriteLine(
                        "Thread # {0} ID {1} plaintext != _store into HandleReadyMessage() -- PROBLEM _store.Count {2} ",
                        Thread.CurrentThread.ManagedThreadId, GetMyId(), _store.Count);
                }
                // debug

                if (_store.Count != 0)
                {
                    var originalInput = RBTools.GetOriginalInput(_store.ToArray());
                    _result = EncryptedShare.FromByte(RBTools.IntToByte(originalInput));
                }
                else
                {
                    _result = null;
                }
                _requested = ResultStatus.Requested;
                CheckResult();
            }
        }
        
        private void CheckResult()
        {
            // if (_result == null) 
            //     return;
            if (_requested != ResultStatus.Requested)
                return;
            _requested = ResultStatus.Sent;
            Broadcaster.InternalResponse(
                new ProtocolResult<ReliableBroadcastId, EncryptedShare>(_broadcastId, _result));
        }
        
        
        
        private List<int> DecodeNew(List<int> toDecode, int[] tips)
        {
            //var correctInput = RBTools.GetOriginalInputWithoutSize(toDecode.ToArray(), extraNumbers);
            var tmp = toDecode.ToArray();
            //_erasureCoding.Decode(tmp, tips.Length, tips);
            if (tips.Length == F)
            {
                _erasureCoding.Decode(tmp, F, tips);
            }
            else
            {// because I can restore from any (N - 2F) parts
                var reducedTips = tips.Take(F).ToArray();
                _erasureCoding.Decode(tmp, F, reducedTips);
            }
                
            return tmp.Take(toDecode.Count).ToList();
        }
        // private List<int> Decode(List<int> toDecode)
        // {
        //     var additionalSpot = toDecode.Count / 2;
        //     var storeLength = toDecode.Count / 2;
        //     //var _erasureCoding = new ErasureCoding(additionalSpot);
        //     var tmp = toDecode.ToArray();
        //     _erasureCoding.Decode(tmp, additionalSpot,null);
        //     return tmp.Take(storeLength).ToList();
        // }

        private List<int> Encode(List<int> toEncode)
        {
            return _erasureCoding.Encoder(toEncode.ToArray(), F).ToList();
        }

        private void Abort()
        {
            Console.Error.WriteLine("Thread {0} ID {1} call abort()",
                Thread.CurrentThread.ManagedThreadId, GetMyId());

            _result = null;
            CheckResult();
        }

        private Tuple<bool, List<int>, List<UInt256>> CheckEchoMsgNew(ECHOMessage msg)
        {
            var goodBlocks = new List<int>();
            var roots = new List<UInt256>();
            var segmentSize = msg.Settings.LengthSegment;
            var numberRegion = 0;
            foreach (var currentRegion in msg.Batch)
            {
                var destinationRoot = new UInt256();

                foreach (var currentFingerPrint in currentRegion.FingerPrints.ToArray())
                {
                    var branch = currentFingerPrint.Branch.BranchOfHashes.ToArray();
                    destinationRoot = currentFingerPrint.DestinationRoot;
                    var sourceBlock = currentFingerPrint.SourceBlock;
                    var reCalcRootNew = BitConverter.GetBytes(sourceBlock).Keccak();
                    foreach (var sibling in branch)
                    {
                        if (sibling.Side == 0)
                        {
                            reCalcRootNew = MerkleTree.ComputeRoot(new[] {sibling.Node, reCalcRootNew});
                        }
                        else
                        {
                            reCalcRootNew = MerkleTree.ComputeRoot(new[] {reCalcRootNew, sibling.Node});
                        }
                    }

                    if (reCalcRootNew.Equals(destinationRoot))
                    {
                        // Console.Error.WriteLine(
                        //     "Thread {0} ID {1} Into checkECHOMsgNew() received messages with index addressee {2} : TRUE",
                        //     Thread.CurrentThread.ManagedThreadId, GetMyId(), msg.IndexAddressee);
                        goodBlocks.Add(sourceBlock);
                    }
                    else
                    {
                        Console.Error.WriteLine(
                            "Thread {0} ID {1} Into checkECHOMsgNew() received messages with index addressee {2} : FALSE",
                            Thread.CurrentThread.ManagedThreadId, GetMyId(), msg.IndexAddressee);
                        return new Tuple<bool, List<int>, List<UInt256>>(false, new List<int>(), new List<UInt256>());
                    }

                    numberRegion++;
                }

                if (numberRegion % segmentSize == 0)
                {
                    roots.Add(destinationRoot);
                }
            }

            return new Tuple<bool, List<int>, List<UInt256>>(true, goodBlocks, roots);
        }


        private bool CheckReadyMsg(List<UInt256> toCheck)
        {
            return _receivedRoots.SequenceEqual(toCheck);
        }

        private bool testConsistenceOf(List<FingerPrint> fingerPrints)
        {
            foreach (var currentFingerPrint in fingerPrints.ToArray())
            {
                var branch = currentFingerPrint.Branch.BranchOfHashes.ToArray();
                var destinationRoot = currentFingerPrint.DestinationRoot;
                var sourceBlock = currentFingerPrint.SourceBlock;

                var reCalcRootNew = BitConverter.GetBytes(sourceBlock).Keccak();
                foreach (var sibling in branch)
                {
                    if (sibling.Side == 0)
                    {
                        reCalcRootNew = MerkleTree.ComputeRoot(new[] {sibling.Node, reCalcRootNew});
                    }
                    else
                    {
                        reCalcRootNew = MerkleTree.ComputeRoot(new[] {reCalcRootNew, sibling.Node});
                    }
                }

                if (!reCalcRootNew.Equals(destinationRoot))
                {
                    return false;
                }
            }

            return true;
        }

        private bool checkConsistencyOfSiblings(List<Sibling> branch, UInt256 destinationRoot, int sourceBlock)
        {
            var reCalcRootNew = BitConverter.GetBytes(sourceBlock).Keccak();
            foreach (var sibling in branch)
            {
                if (sibling.Side == 0)
                {
                    reCalcRootNew = MerkleTree.ComputeRoot(new[] {sibling.Node, reCalcRootNew});
                }
                else
                {
                    reCalcRootNew = MerkleTree.ComputeRoot(new[] {reCalcRootNew, sibling.Node});
                }
            }

            return reCalcRootNew.Equals(destinationRoot);
        }

        private (List<Region>, List<UInt256>, int, int, int[]) GetBatch(int[] input, int indexAddressee)
        {
            var (correctInputForDebug, Gs) = f1(input);

            var batch = new List<Region>();
            var roots = new List<UInt256>();
            var indexStripe = 0;
            var sizeSegment = 0;
            var countStripes = Gs.Count;

            foreach (var G in Gs)
            {
                var hashes = new List<UInt256>();

                foreach (var currentInt in G)
                {
                    hashes.Add(BitConverter.GetBytes(currentInt).Keccak());
                }

                var destinationRoot = MerkleTree.ComputeRoot(hashes);

                var currentRegion = new Region();
                sizeSegment = G.Length / N;

                for (var positionIntoSegment = 0; positionIntoSegment < sizeSegment; positionIntoSegment++)
                {
                    var currentFingerPrint = new FingerPrint();
                    var indexSelectedBlock = indexAddressee * sizeSegment + positionIntoSegment;
                    var destinationBlock = G[indexSelectedBlock];
                    var destinationHash = hashes[indexSelectedBlock];

                    var testingBranch = CreateBranch(hashes, destinationHash);
                    if (checkConsistencyOfSiblings(testingBranch, destinationRoot, destinationBlock))
                    {
                        // Console.WriteLine(" CONSISTENT: Block {0} Root {1} Branch {2}", 
                        //     destinationBlock, destinationRoot, testingBranch.ToArray());

                        currentFingerPrint.Branch = new Branch
                        {
                            BranchOfHashes = {testingBranch}
                        };
                        currentFingerPrint.DestinationRoot = destinationRoot;
                        currentFingerPrint.IndexAddressee = indexAddressee;
                        currentFingerPrint.IndexSegment = positionIntoSegment;
                        currentFingerPrint.IndexStripe = indexStripe;
                        currentFingerPrint.SourceBlock = destinationBlock;
                    }
                    else
                    {
                        Console.WriteLine("NOT CONSIST: Block {0} Root {1} Branch {2}", destinationBlock,
                            destinationRoot, testingBranch);
                    }

                    currentRegion = new Region
                    {
                        FingerPrints = {currentFingerPrint}
                    };
                    batch.Add(currentRegion);
                }

                roots.Add(destinationRoot);
                //batch.Add(currentRegion);
                indexStripe++;
            }
            return (batch, roots, sizeSegment, countStripes, correctInputForDebug);
        }

        private ConsensusMessage CreateValMessage(int[] input, int indexAddressee)
        {   
            
            var (batch, roots, segment, countStripes, correctInputForDebug) = GetBatch(input, indexAddressee);
            
            var message = new ConsensusMessage
            {
                ValMessage = new ValMessage
                {
                    RootMerkleTree = {roots},
                    AssociatedValidatorId = _broadcastId.AssociatedValidatorId,
                    IndexAddressee = indexAddressee,
                    Batch = {batch},
                    Plaintext = {correctInputForDebug}, // todo: fordebug
                    Settings = new Settings()
                    {
                        LengthSegment = segment,
                        CountStripes = countStripes, 
                        ExtraNumbers = 0 // todo: depricated 
                    }
                }
            };
            return message;
        }

        private ConsensusMessage CreateReadyMessage(List<UInt256> roots, int[] pt)
        {
            var message = new ConsensusMessage
            {
                ReadyMessage = new ReadyMessage
                {
                    RootMerkleTree = {roots},
                    AssociatedValidatorId = _broadcastId.AssociatedValidatorId,
                    Plaintext = {pt}
                }
            };
            return message;
        }

        private ConsensusMessage CreateECHOMessage(ValMessage valMessage)
        {
            
            var message = new ConsensusMessage
            {
                EchoMessage = new ECHOMessage
                {
                    RootMerkleTree = {valMessage.RootMerkleTree},
                    AssociatedValidatorId = _broadcastId.AssociatedValidatorId,
                    IndexAddressee = valMessage.IndexAddressee,
                    Batch = {valMessage.Batch},
                    Plaintext = {valMessage.Plaintext},
                    Settings = new Settings()
                    {
                        CountStripes = valMessage.Settings.CountStripes,
                        LengthSegment = valMessage.Settings.LengthSegment,
                        ExtraNumbers = valMessage.Settings.ExtraNumbers
                        
                    }
                }
            };
            return message;
        }

        // blocks - list of block on which create MT
        // playerIndex - it is the player index for which create branch (i.e. path)
        private static List<Sibling> CreateBranch(List<UInt256> hashes, UInt256 destinationHash)
        {
            var mt = MerkleTree.ComputeTree(hashes.ToArray());

            var branch = new List<Sibling>();

            var currentNode = mt.Search(destinationHash);
            while (!currentNode.IsRoot)
            {
                var parent = currentNode.Parent;
                var left = parent.LeftChild;
                var right = parent.RightChild;
                var tmpSibling = new Sibling()
                {
                    Side = currentNode.Hash.Equals(left.Hash) ? 1 : 0,
                    Node = currentNode.Hash.Equals(left.Hash) ? right.Hash : left.Hash
                };
                branch.Add(tmpSibling);
                currentNode = currentNode.Parent;
            }

            //branch.Add(currentNode.Hash); // todo: if need to add root value  as like last notice in the branch
            /*
             * todo: можно оптимизировать по объему пересылке, так как изменяются только ближайшие к листьям значения хешей
             */
            return branch;
        }

        public static int[] GetInput(int length)
        {
            var rnd = new Random();
            var input = new int[length];
            for (var i = 0; i != length; i++)
            {
                //input[i] = (i + 10 + i*i) % 255; // for create permanent values
                input[i] = rnd.Next() % 255;
            }

            for (var i = length; i != length; i++)
                input[i] = 0;
            return input;
        }

        private Tuple<int[], List<int[]>> f1(int[] input)
        {
            // изменяем input так что бы его длина делилась на k N - F
            // размер store (объем хранения) записываем в начало
            var K = 1; // это значение в дальнейшем равноценно segmentLength
            var stripeLength = K * N - F;
            var supplementedInput = RBTools.GetCorrectInput(input, stripeLength, false, 13);
           
            
            var countStripes = supplementedInput.Count / stripeLength;
            var GsV2 = new List<int[]>();
            for (var i = 0; i < countStripes; i++)
            {
                var currentStripe = supplementedInput.Skip(i * stripeLength).Take(stripeLength).ToArray();
                var GV2 = _erasureCoding.Encoder(currentStripe, F);
                GsV2.Add(GV2);
            }

            // отправляю сам себe свою шару ставлю флаг, что она отправлена
            // if (!_receivedCorrectEchoFrom[GetMyId()])
            // {
            //     var selfShare = GsV2.Select(G => G[GetMyId()]).ToList();
            //     _receivedBatchesOfBlocks[GetMyId()] = selfShare;
            //     _receivedCorrectEchoFrom[GetMyId()] = true;
            //     Console.Error.WriteLine(                                // <----  debug
            //         "Thread {0} ID {1} wrote selfshare", 
            //         Thread.CurrentThread.ManagedThreadId, GetMyId());
            // }
            
            var res = new Tuple<int[], List<int[]>>(supplementedInput.ToArray(), GsV2);
            return res;
        }

        // private List<UInt256> recalculateHashes()
        // {
        //     var numberStripes = _receivedBatchesOfBlocks[0].Capacity;
        //     var recalculatedRoots = new List<UInt256>();
        //     for (var i = 0; i < numberStripes; i++)
        //     {
        //         var hashes = new List<UInt256>();
        //         for (var j = 0; j < N; j++)
        //         {
        //             hashes.Add(BitConverter.GetBytes(_receivedBatchesOfBlocks[j][i]).Keccak());
        //         }
        //
        //         recalculatedRoots.Add(MerkleTree.ComputeRoot(hashes));
        //     }
        //
        //     return recalculatedRoots;
        // }


        // private void GeneralDivide(int[] source, List<int[]> result, int parts, int fillSymbol=0)
        // {
        //     var bufferLength = source.Length;
        //     var partLength = bufferLength / parts;
        //     var partsCount = bufferLength / partLength;
        //     var tmp = new int[partLength];
        //     if (bufferLength % partLength != 0)
        //     {
        //         throw new ArgumentException("The length of source parameter should be divided by the length of part ", nameof(source));
        //     }
        //     for (int i = 0; i < partsCount; i++)
        //     {
        //         tmp = source.Skip(partLength * i).Take(partLength).ToArray();
        //         result.Add(tmp);
        //     }
        // }

        // private List<List<int>> f2(List<int[]> Gs)
        // {
        //     var batches = new List<List<int>>();
        //     var segment = Gs[0].Length / N;
        //     for (var i = 0; i < N; i++)
        //     {
        //         //batches.Add(new int[segment * Gs.Count]);
        //         batches.Add(new List<int>());
        //     }
        //
        //     foreach (var G in Gs)
        //     {
        //         var j = 0;
        //         foreach (var batch in batches)
        //         {
        //             for (int i = 0; i < segment; i++)
        //             {
        //                 batch.Add(G[j]);
        //                 j++;    
        //             }
        //         }
        //     }
        //     return batches;
        // }


        // private List<List<int[]>> CreateBlocksNew(int[] input)
        // {
        //     var coeffAdditionalPositions = 1;
        //     var additionalPositions = N * coeffAdditionalPositions;
        //     var ecBigInput = new ErasureCoding(additionalPositions);
        //     
        //     var Gs = new List<int[]>();
        //     var cntLines = input.Length / N;
        //     for (var i = 0; i < cntLines; i++)
        //     {
        //         var peice = input.Skip(i * N).Take(N).ToArray();
        //         var G = ecBigInput.Encoder(peice, additionalPositions);
        //         Gs.Add(G);
        //     }
        //     // =================================================================================
        //     // prepare batches for players
        //     var batches = new List<List<int[]>>();
        //     /*
        //      * {G11,G21,...,GR1} , {G12,G22,...,GR2}, {G13,G23,...,GR3}, ... , {G1N-1,G21N-1,...,GR1N-1}, {G1N,G2N,...,GRN}
        //      */
        //     for (int i = 0; i < N; i++)
        //     {
        //         batches.Add(new List<int[]>());
        //     }
        //     
        //     //var segment = Gs[0].Count / N; 
        //     foreach (var arr in Gs)
        //     {
        //         for (var i = 0; i < N; i++)
        //         {
        //             //var tmp = new byte[segment];
        //             //Array.Copy(arr.ToArray(), i * segment, tmp, 0, segment);
        //             batches[i].Add(arr);
        //         }
        //     }
        //     return batches;
        // }
        //
        // private List<UInt256> CreateBlocks(byte[] input)
        // {
        //     var additionalBits = 16;
        //     
        //     var initDataBuffer = ByteToInt(input);
        //
        //     var field = new GenericGF(285, byte.MaxValue, 0);
        //     var rse = new ReedSolomonEncoder(field);
        //
        //     var encodeArray = new List<int[]>();
        //     if (initDataBuffer.Length >= byte.MaxValue)
        //     {
        //         GeneralDivide(initDataBuffer, encodeArray, N);
        //     }
        //     foreach (var t in encodeArray)
        //     {
        //         rse.Encode(t, additionalBits);
        //     }
        //     
        //     return BuildShares(initDataBuffer, N);
        // }
        //
        //
        // private static UInt256 CalculateRoot(List<UInt256> blocks)
        // {   
        //     return MerkleTree.ComputeRoot(blocks);
        // }
        //
        // private List<UInt256> BuildShares(Int32[] v, int playersCount)
        // {
        //     if (!v.Any())
        //     {
        //         return new List<UInt256>();
        //     }
        //     var shareSize = v.Length / playersCount + 1;
        //     
        //     var a = new List<UInt256>();
        //     
        //     for (var i = 0; i < playersCount; i++)
        //     {
        //         var buffer = new byte[shareSize];
        //         for (var j = 0; j < shareSize; j++)
        //         {
        //             buffer[j] = BitConverter.GetBytes(v[i * playersCount + j])[0];
        //         }
        //         a.Add(buffer.ToUInt256());
        //         a.Add(buffer.Keccak256().ToUInt256());
        //     }
        //     return a;
        // }
        //
        // private byte[] GetTestVector(int lenData, int additionalPlaces)
        // {
        //     var a = lenData;
        //     var additionalBits = additionalPlaces;
        //     var dataSz = a - additionalBits;
        //     var vector = new byte[additionalBits + dataSz];
        //     var rnd = new Random();
        //     rnd.NextBytes(vector);
        //     for (var i = 0; i < a / 2; i++)
        //         vector[i + dataSz] = 0;
        //     return vector;
        // }
    }
}