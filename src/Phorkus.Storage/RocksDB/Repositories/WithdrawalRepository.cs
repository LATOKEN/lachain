using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Storage.RocksDB.Repositories
{
    public class WithdrawalRepository : IWithdrawalRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public WithdrawalRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public Withdrawal GetWithdrawalByHash(UInt256 withdrawalHash)
        {
            var prefix = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawalHash);
            var raw = _rocksDbContext.Get(prefix);
            return raw == null ? null : Withdrawal.Parser.ParseFrom(raw);
        }

        public void RemoveWithdrawal(Withdrawal withdrawal)
        {
            var prefixTx = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawal.TransactionHash);
            _rocksDbContext.Delete(prefixTx);
        }

        public void AddWithdrawal(Withdrawal withdrawal)
        {
            /* write Withdrawal to storage */
            var prefixTx = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawal.TransactionHash);
            _rocksDbContext.Save(prefixTx, withdrawal.ToByteArray());
            var prefixNonce = EntryPrefix.WithdrawalByNonce.BuildPrefix(withdrawal.Nonce);
            _rocksDbContext.Save(prefixNonce, withdrawal.TransactionHash.ToByteArray());
        }
        
        public void AddWithdrawalState(Withdrawal withdrawal)
        {
            var prefixState =
                EntryPrefix.WithdrawalByStateNonce.BuildPrefix(withdrawal.State.ToString() + withdrawal.Nonce);
            _rocksDbContext.Save(prefixState, withdrawal.TransactionHash.ToByteArray());
        }

        public WithdrawalState ChangeWithdrawalState(UInt256 withdrawalHash, WithdrawalState withdrawalState)
        {
            if (withdrawalHash is null)
                throw new ArgumentNullException(nameof(withdrawalHash));
            var prefix = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawalHash);
            var withdrawal = GetWithdrawalByHash(withdrawalHash);
            if (withdrawal == null)
            {
                return WithdrawalState.Unknown;
            }

            var oldState = withdrawal.State;
            withdrawal.State = withdrawalState;
            _rocksDbContext.Save(prefix, withdrawal.ToByteArray());
            return oldState;
        }

        public Withdrawal ChangeWithdrawal(UInt256 withdrawalHash, Withdrawal withdrawal)
        {
            if (withdrawalHash is null)
                throw new ArgumentNullException(nameof(withdrawalHash));
            var prefix = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawalHash);
            var oldWithdrawal = GetWithdrawalByHash(withdrawalHash);
            _rocksDbContext.Save(prefix, withdrawal.ToByteArray());
            return oldWithdrawal;
        }

        public WithdrawalState GetWithdrawalState(UInt256 withdrawalHash)
        {
            if (withdrawalHash is null)
                throw new ArgumentNullException(nameof(withdrawalHash));
            var withdrawal = GetWithdrawalByHash(withdrawalHash);
            return withdrawal != null ? withdrawal.State : WithdrawalState.Unknown;
        }

        public IEnumerable<Withdrawal> GetWithdrawalsByNonces(IEnumerable<ulong> nonces)
        {
            return nonces.Select(GetWithdrawalByNonce).Where(withdrawal => withdrawal != null);
        }


        public IEnumerable<Withdrawal> GetWithdrawalsByHashes(IEnumerable<UInt256> withdrawalHashes)
        {
            return withdrawalHashes.Select(GetWithdrawalByHash).Where(withdrawal => withdrawal != null);
        }

        public bool ContainsWithdrawalByHash(UInt256 withdrawalHash)
        {
            var prefix = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawalHash);
            var raw = _rocksDbContext.Get(prefix);
            return raw != null;
        }

        public Withdrawal GetWithdrawalByNonce(ulong nonce)
        {
            var prefix = EntryPrefix.WithdrawalByNonce.BuildPrefix(nonce);
            var raw = _rocksDbContext.Get(prefix);
            return raw == null ? null : Withdrawal.Parser.ParseFrom(raw);
        }

        public Withdrawal GetWithdrawalByStateNonce(WithdrawalState withdrawalState, ulong nonce)
        {
            var prefix = EntryPrefix.WithdrawalByStateNonce.BuildPrefix(withdrawalState.ToString() + nonce);
            var raw = _rocksDbContext.Get(prefix);
            return raw == null ? null : Withdrawal.Parser.ParseFrom(raw);
        }

        public Withdrawal GetWithdrawalByNonceAndDelete(ulong nonce)
        {
            var prefix = EntryPrefix.WithdrawalByNonce.BuildPrefix(nonce);
            var raw = _rocksDbContext.Get(prefix);
            _rocksDbContext.Delete(prefix);
            return raw == null ? null : Withdrawal.Parser.ParseFrom(raw);
        }
    }
}