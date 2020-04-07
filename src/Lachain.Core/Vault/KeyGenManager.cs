using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.Blockchain.ContractManager.Standards;
using Lachain.Core.Blockchain.OperationManager;
using Lachain.Core.VM;
using Lachain.Crypto;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Vault
{
    public class KeyGenManager
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private static readonly ILogger<KeyGenManager> Logger =
            LoggerFactory.GetLoggerForClass<KeyGenManager>();

        private readonly IPrivateWallet _privateWallet;

        public KeyGenManager(ITransactionManager transactionManager, IPrivateWallet privateWallet)
        {
            _privateWallet = privateWallet;
            transactionManager.OnSystemContractInvoked += TransactionManagerOnOnSystemContractInvoked;
        }

        private void TransactionManagerOnOnSystemContractInvoked(object sender, ContractContext context)
        {
            var tx = context.Receipt.Transaction;
            if (!tx.To.Equals(ContractRegisterer.GovernanceContract)) return;
            if (tx.Invocation.Length < 4) return;

            var signature = BitConverter.ToUInt32(tx.Invocation.Take(4).ToArray(), 0);
            var decoder = new ContractDecoder(tx.Invocation.ToArray());
            if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodChangeValidators))
            {
                var args = decoder.Decode(GovernanceInterface.MethodChangeValidators);
                var publicKeys =
                    (args[0] as byte[][] ?? throw new ArgumentException("Cannot parse method args"))
                    .Select(x => x.ToPublicKey())
                    .ToArray();
                if (!publicKeys.Contains(_privateWallet.EcdsaKeyPair.PublicKey)) return;
                StartKeygen(publicKeys);
            }
            else if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodKeygenCommit))
            {
                var args = decoder.Decode(GovernanceInterface.MethodKeygenCommit);
            }
            else if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodKeygenSendValue))
            {
                var args = decoder.Decode(GovernanceInterface.MethodKeygenSendValue);
            }
            else if (signature == ContractEncoder.MethodSignatureBytes(GovernanceInterface.MethodKeygenConfirm))
            {
                var args = decoder.Decode(GovernanceInterface.MethodKeygenConfirm);
            }
        }

        private void StartKeygen(ECDSAPublicKey[] publicKeys)
        {
            var n = publicKeys.Length;
            var f = (n - 1) / 3;
            var biVarPoly = new Fr[][f + 1];
            var commitment = new G1[][f + 1];
            for (var i = 0; i <= f; ++i)
            {
                biVarPoly[i] = new Fr[f + 1];
                commitment[i] = new G1[f + 1];
                for (var j = 0; j <= f; ++j)
                {
                    biVarPoly[i][j] = Fr.GetRandom();
                    commitment[i][j] = G1.Generator * biVarPoly[i][j];
                }
            }

            var rows = new Fr[][n];
            for (var x = 1; x <= n; ++x)
            {
                rows[x - 1] = Enumerable.Range(0, f + 1).Select(_ => Fr.Zero).ToArray();
                for (var i = 0; i <= f; ++i)
                {
                    rows[x - 1][i] = Fr.Zero;
                    var xPowJ = Fr.FromInt(1);
                    var frX = Fr.FromInt(x);

                    for (var j = 0; j <= f; ++j)
                    {
                        rows[x - 1][i] += biVarPoly[i][j] * xPowJ;
                        xPowJ *= frX;
                    }
                }
            }

            return (commitment, rows.Select((row, i) => ))
        }

        private byte[] EncryptRow(IEnumerable<Fr> row, ECDSAPublicKey publicKey)
        {
            var serializedRow = row.Select(Fr.ToBytes)
                .Cast<IEnumerable<byte>>()
                .Aggregate((a, b) => a.Concat(b))
                .ToArray();
            return Crypto.Secp256K1Encrypt(publicKey.EncodeCompressed(), serializedRow);
        }

        private IEnumerable<Fr> DecryptRow(byte[] encryptedRow, ECDSAPrivateKey privateKey)
        {
            return Crypto.Secp256K1Decrypt(privateKey.Encode(), encryptedRow)
                .Batch(Fr.ByteSize)
                .Select(Fr.FromBytes);
        }
    }
}