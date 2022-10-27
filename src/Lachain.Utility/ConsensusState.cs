using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Nethereum.RLP;

namespace Lachain.Utility
{
    public class ConsensusState
    {
        // if number of validators is greater than 255, we need to change ConsensusState with a hardfork
        public ConsensusState(byte[] tpkePublicKey, byte[][] tpkeVerificationKeys, ValidatorCredentials[] validators)
        {
            TpkePublicKey = tpkePublicKey;
            TpkeVerificationKeys = tpkeVerificationKeys;
            Validators = validators;
        }

        public byte[] TpkePublicKey { get; }
        
        public byte[][] TpkeVerificationKeys { get; }
        public ValidatorCredentials[] Validators { get; }

        private byte[] OldStateToBytes()
        {
            var rawInfo = new List<byte[]> {TpkePublicKey};
            rawInfo.AddRange(Validators.Select(c => c.ToBytes()));
            return RLP.EncodeList(rawInfo.Select(RLP.EncodeElement).ToArray());
        }

        private byte[] StateWithTpkeVerificationToBytes()
        {
            var rawInfo = new List<byte[]> {TpkePublicKey};
            rawInfo.Add(new byte[] {(byte) TpkeVerificationKeys.Length});
            rawInfo.AddRange(TpkeVerificationKeys);
            rawInfo.AddRange(Validators.Select(c => c.ToBytes()));
            return RLP.EncodeList(rawInfo.Select(RLP.EncodeElement).ToArray());
        }

        private byte[] StateWithOnlyValidatorInfoToBytes()
        {
            var rawInfo = new List<byte[]>();
            rawInfo.Add(new byte[] {(byte) Validators.Length});
            rawInfo.AddRange(Validators.Select(c => c.ToBytes()));
            return RLP.EncodeList(rawInfo.Select(RLP.EncodeElement).ToArray());
        }

        public byte[] ToBytes(ConsensusStateStatus status)
        {
            switch (status)
            {
                case ConsensusStateStatus.OldState:
                    return OldStateToBytes();
                case ConsensusStateStatus.StateWithTpkeVerification:
                    return StateWithTpkeVerificationToBytes();
                case ConsensusStateStatus.StateWithOnlyValidatorInfo:
                    return StateWithOnlyValidatorInfoToBytes();
                default:
                    throw new Exception($"Unhandled ConsensusStateStatus: {status}");
            }
        }

        // old state where we have tpke public key and validator credentials
        public static ConsensusState LoadOldState(RLPCollection decoded)
        {
            var tpkePubKey = decoded[0].RLPData;
            var old_credentials = decoded.Skip(1)
                .Select(x => x.RLPData)
                .Select(x => ValidatorCredentials.FromBytes(x))
                .ToArray();
            var fakeTpkeVerificationKeys = Enumerable.Range(0, old_credentials.Length)
                .Select(i => tpkePubKey).ToArray();
            return new ConsensusState(tpkePubKey, fakeTpkeVerificationKeys, old_credentials);
        }

        // state where we have tpke public and verification keys and validator credentials
        public static ConsensusState LoadStateWithTpkeVerification(RLPCollection decoded)
        {
            var tpkePubKey = decoded[0].RLPData;
            var keysNumber = decoded[1].RLPData[0];
            var tpkeVerificationKeys = decoded.Skip(2).Take(keysNumber)
                .Select(x => x.RLPData)
                .ToArray();
            var credentials = decoded.Skip(2 + keysNumber)
                .Select(x => x.RLPData)
                .Select(x => ValidatorCredentials.FromBytes(x))
                .ToArray();
            return new ConsensusState(tpkePubKey, tpkeVerificationKeys, credentials);
        }

        // updated state where we have only validator credentials
        public static ConsensusState LoadStateWithOnlyValidatorInfo(RLPCollection decoded)
        {
            var keysNumber = decoded[0].RLPData[0];
            var credentials = decoded.Skip(1).Take(keysNumber)
                .Select(x => x.RLPData)
                .Select(x => ValidatorCredentials.FromBytes(x))
                .ToArray();
            return new ConsensusState(Array.Empty<byte>(), Array.Empty<byte[]>(), credentials);
        }

        public static ConsensusState FromBytes(ReadOnlySpan<byte> bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            if (decoded[0].RLPData.Length == 1)
            {
                // updated data format with only validator info
                return LoadStateWithOnlyValidatorInfo(decoded);
            }
            else if (decoded[1].RLPData.Length == 1) // new data format with verification keys
            {
                return LoadStateWithTpkeVerification(decoded);
            }
            else return LoadOldState(decoded);
        }
    }

    public enum ConsensusStateStatus : byte
    {
        OldState = 0,
        StateWithTpkeVerification = 1,
        StateWithOnlyValidatorInfo = 2,
    }
}