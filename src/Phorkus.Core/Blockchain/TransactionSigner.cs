using System;
using System.Collections.Concurrent;
using System.Linq;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain
{
    public class TransactionSigner : ITransactionSigner
    {
                
        private readonly ICrypto _crypto;
        private readonly ConcurrentDictionary<UInt256, UInt256> _verifiedTransactions
            = new ConcurrentDictionary<UInt256, UInt256>();
        private readonly ITransactionVerifier _transactionVerifier;

        public TransactionSigner(
            IValidatorManager validatorManager,
            ITransactionVerifier transactionVerifier,
            IMultisigVerifier multisigVerifier,
            ICrypto crypto)
        {
            
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
            _transactionVerifier = transactionVerifier ?? throw new ArgumentNullException(nameof(transactionVerifier));

            transactionVerifier.OnTransactionVerified += (sender, transaction) =>
                _verifiedTransactions.TryAdd(transaction.Hash, transaction.Hash);
        }
        
        public event EventHandler<SignedTransaction> OnTransactionSigned;
        
        public SignedTransaction Sign(Transaction transaction, KeyPair keyPair)
        {
            /* use raw byte arrays to sign transaction hash */
            var message = transaction.ToHash256().Buffer.ToByteArray();
            var signature = _crypto.Sign(message, keyPair.PrivateKey.Buffer.ToByteArray());
            /* we're afraid */
            var pubKey = _crypto.RecoverSignature(message, signature);
            if (!pubKey.SequenceEqual(keyPair.PublicKey.Buffer.ToByteArray()))
                throw new InvalidKeyPairException();
            var signed = new SignedTransaction
            {
                Transaction = transaction,
                Hash = transaction.ToHash256(),
                Signature = signature.ToSignature()
            };
            OnTransactionSigned?.Invoke(this, signed);
            return signed;
        }

        public OperatingError VerifySignature(SignedTransaction transaction, PublicKey publicKey)
        {
            if (!_verifiedTransactions.ContainsKey(transaction.Hash))
                return _transactionVerifier.VerifyTransactionImmediately(transaction, publicKey)
                    ? OperatingError.Ok
                    : OperatingError.InvalidSignature;
            _verifiedTransactions.TryRemove(transaction.Hash, out _);
            return OperatingError.Ok;
        }

        public OperatingError VerifySignature(SignedTransaction transaction)
        {
            if (!_verifiedTransactions.ContainsKey(transaction.Hash))
                return _transactionVerifier.VerifyTransactionImmediately(transaction)
                    ? OperatingError.Ok
                    : OperatingError.InvalidSignature;
            _verifiedTransactions.TryRemove(transaction.Hash, out _);
            return OperatingError.Ok;
        }
    }
}