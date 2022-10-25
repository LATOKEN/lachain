using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Benchmark;
using MCL.BLS12_381.Net;

namespace Lachain.Crypto.ThresholdEncryption
{
    public class ThresholdEncryptor : IThresholdEncryptor
    {
        private static readonly ILogger<ThresholdEncryptor> Logger = LoggerFactory.GetLoggerForClass<ThresholdEncryptor>();
        public static readonly TimeBenchmark FullDecryptBenchmark = new TimeBenchmark();
        public static readonly TimeBenchmark DecryptBenchmark = new TimeBenchmark();
        public static readonly TimeBenchmark EncryptBenchmark = new TimeBenchmark();
        public static readonly TimeBenchmark VerifyBenchmark = new TimeBenchmark();
        private readonly PrivateKeyShare _privateKeyShare;
        private readonly PublicKeySet _publicKeySet;
        private readonly EncryptedShare?[] _receivedShares;
        private readonly IRawShare?[] _shares;
        private readonly List<PartiallyDecryptedShare>[] _decryptedShares;
        private readonly bool[][] _receivedShareFrom;
        private readonly bool[] _taken;
        private bool _takenSet;
        private bool _skipShareValidation;
        private int _myIdx;

        public ThresholdEncryptor(PrivateKeyShare privateKey, PublicKeySet publicKeySet, bool skipShareValidation)
        {
            var myPublicKey = privateKey.GetPublicKeyShare();
            if (!publicKeySet.Keys.Contains(myPublicKey))
                throw new ArgumentException(
                    "Invalid private key share for threshold encryption: " +
                    "corresponding public key is not present"
                );
            for (int i = 0 ; i < publicKeySet.Count; i++)
            {
                if (publicKeySet[i].Equals(myPublicKey))
                {
                    _myIdx = i;
                    break;
                }
            }
            _privateKeyShare = privateKey;
            _publicKeySet = publicKeySet;
            var N = _publicKeySet.Count;
            _receivedShares = new EncryptedShare[N];
            _decryptedShares = new List<PartiallyDecryptedShare>[N];
            for (var i = 0; i < N; ++i)
            {
                _decryptedShares[i] = new List<PartiallyDecryptedShare>();
            }

            _taken = new bool[N];
            _shares = new IRawShare[N];
            _skipShareValidation = skipShareValidation;
            _receivedShareFrom = new bool[N][];
            for (var iter = 0 ; iter < N ; iter++)
            {
                _receivedShareFrom[iter] = new bool[N];
            }
        }

        public EncryptedShare Encrypt(IRawShare rawShare)
        {
            return EncryptBenchmark.Benchmark(() =>
            {
                return _publicKeySet.SharedPublicKey.Encrypt(rawShare);
            });
        }

        private PartiallyDecryptedShare Decrypt(EncryptedShare share)
        {
            return DecryptBenchmark.Benchmark(() =>
            {
                return _privateKeyShare.Decrypt(share, _myIdx);
            });
        }

        public List<PartiallyDecryptedShare> AddEncryptedShares(List<EncryptedShare> encrypedShares)
        {
            var shares = new List<PartiallyDecryptedShare>();
            foreach (var share in encrypedShares)
            {
                _taken[share.Id] = true;
                _receivedShares[share.Id] = share;
                if (_decryptedShares[share.Id].Count > 0) // if we have any partially decrypted shares for this share - verify them
                {
                    _decryptedShares[share.Id] = _decryptedShares[share.Id]
                        .Where(ps => VerifyShare(ps))
                        .ToList();
                }
                var dec = Decrypt(share);
                shares.Add(dec);
            }

            _takenSet = true;
            return shares;
        }

        public bool AddDecryptedShare(TPKEPartiallyDecryptedShareMessage msg, int senderId)
        {
            PartiallyDecryptedShare? share = null;
            // DecryptorId is basically the validator id, it tells us who decrypted the message, so it should be same
            if (msg.DecryptorId != senderId)
                throw new Exception($"Validator {senderId} sends message with decryptor id {msg.DecryptorId}");
            // same decrypted id more than once prevents full decrypt
            if (_receivedShareFrom[msg.ShareId][msg.DecryptorId])
                throw new Exception($"validator {senderId} sent decrypted messsage for share {msg.ShareId} twice");

            _receivedShareFrom[msg.ShareId][msg.DecryptorId] = true;
            // Converting any random bytes to G1 is not possible
            share = PartiallyDecryptedShare.Decode(msg);
            if (!(_receivedShares[share.ShareId] is null))
            {
                if (!VerifyShare(share))
                {
                    throw new Exception("Invalid share");
                }
            }

            _decryptedShares[share.ShareId].Add(share);

            if (!(share is null))
                return true;
            else
                return false;
        }

        public bool CheckDecryptedShares(int id)
        {
            if (!_takenSet) return false;
            if (!_taken[id]) return false;
            if (_decryptedShares[id].Count < _publicKeySet.Threshold + 1) return false;
            if (_shares[id] != null) return false;
            if (_receivedShares[id] is null) return false;
            Logger.LogTrace($"Collected {_decryptedShares[id].Count} shares for {id}, can decrypt now");
            _shares[id] = FullDecrypt(_receivedShares[id]!, _decryptedShares[id]);
            return true;
        }

        private RawShare FullDecrypt(EncryptedShare share, List<PartiallyDecryptedShare> us)
        {
            return FullDecryptBenchmark.Benchmark(() =>
            {
                if (us.Count < _publicKeySet.Threshold + 1)
                {
                    throw new Exception("Insufficient number of shares!");
                }

                var ids = new HashSet<int>();
                foreach (var part in us)
                {
                    if (ids.Contains(part.DecryptorId))
                        throw new Exception($"Id {part.DecryptorId} was provided more than once!");
                    if (part.ShareId != share.Id)
                        throw new Exception($"Share id mismatch for decryptor {part.DecryptorId}");
                    ids.Add(part.DecryptorId);
                }

                var ys = new List<G1>();
                var xs = new List<Fr>();

                foreach (var part in us)
                {
                    xs.Add(Fr.FromInt(part.DecryptorId + 1));
                    ys.Add(part.Ui);
                }

                var u = MclBls12381.LagrangeInterpolate(xs.ToArray(), ys.ToArray());
                if (!VerifyFullDecryptedShare(u, share))
                    throw new Exception("Impossible! all shares are verified but fulldecrypted share cannot be verified");
                return new RawShare(Utils.XorWithHash(u, share.V), share.Id);
            });
        }

        public bool GetResult(out ISet<IRawShare>? result)
        {
            result = null;
            if (!_takenSet) return false;

            if (_taken.Zip(_shares, (b, share) => b && share is null).Any(x => x)) return false;

            result = _taken.Zip(_shares, (b, share) => (b, share))
                .Where(x => x.b)
                .Select(x => x.share ?? throw new Exception("impossible"))
                .ToHashSet();

            return true;
        }

        private bool VerifyFullDecryptedShare(G1 interpolation, EncryptedShare share)
        {
            return VerifyBenchmark.Benchmark(() =>
            {
                if (_skipShareValidation)
                    return true;
                return _publicKeySet.SharedPublicKey.VerifyFullDecryptedShare(share, interpolation);
            });
        }

        private bool VerifyShare(PartiallyDecryptedShare share)
        {
            return VerifyBenchmark.Benchmark(() =>
            {
                if (_skipShareValidation)
                    return true;
                try
                {
                    return _publicKeySet[share.DecryptorId].VerifyShare(_receivedShares[share.ShareId]!, share);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(
                        $"Could not verify decrypted share with decryptor id {share.DecryptorId} and share id {share.ShareId}: {ex}"
                    );
                    return false;
                }
            });
        }
    }
}