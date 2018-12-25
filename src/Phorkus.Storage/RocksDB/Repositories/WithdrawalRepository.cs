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
        
        public bool ContainsWithdrawalByNonce(ulong nonce)
        {
            var prefix = EntryPrefix.WithdrawalByNonce.BuildPrefix(nonce);
            var raw = _rocksDbContext.Get(prefix);
            return raw != null;
        }
        
        public bool AddWithdrawal(Withdrawal withdrawal)
        {
            /* validate withdrawal nonce */
            var currentNonce = GetCurrentWithdrawalNonce();
            if (withdrawal.Nonce == 0)
                withdrawal.Nonce = currentNonce;
            else if (currentNonce != withdrawal.Nonce)
                return false;
            /* write Withdrawal to storage */
            var prefixTx = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawal.TransactionHash);
            _rocksDbContext.Save(prefixTx, withdrawal.ToByteArray());
            var prefixNonce = EntryPrefix.WithdrawalByNonce.BuildPrefix(withdrawal.Nonce);
            _rocksDbContext.Save(prefixNonce, withdrawal.TransactionHash.ToByteArray());
            /* update withdrawal nonce */
            SetCurrentWithdrawalNonce(withdrawal.Nonce);
            return true;
        }

        public Withdrawal ChangeWithdrawalState(UInt256 withdrawalHash, WithdrawalState withdrawalState)
        {
            if (withdrawalHash is null)
                throw new ArgumentNullException(nameof(withdrawalHash));
            var prefix = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawalHash);
            var withdrawal = GetWithdrawalByHash(withdrawalHash);
            if (withdrawal is null)
                return null;
            withdrawal.State = withdrawalState;
            _rocksDbContext.Save(prefix, withdrawal.ToByteArray());
            return withdrawal;
        }

        public bool ApproveWithdrawal(UInt256 withdrawalHash, byte[] rawTransaction, byte[] transactionHash)
        {
            /* try to find withdrawal */
            var withdrawal = GetWithdrawalByHash(withdrawalHash);
            if (withdrawal is null)
                return false;
            /* validate withdrawal nonce */
            var approvedNonce = GetApprovedWithdrawalNonce();
            if (withdrawal.Nonce != approvedNonce + 1)
                return false;
            /* change approve fields */
            withdrawal.Timestamp = (ulong) new DateTimeOffset().ToUnixTimeMilliseconds();
            withdrawal.State = WithdrawalState.Approved;
            withdrawal.OriginalTransaction = ByteString.CopyFrom(rawTransaction);
            withdrawal.OriginalHash = ByteString.CopyFrom(transactionHash);
            /* write new withdrawal */
            var prefix = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawalHash);
            _rocksDbContext.Save(prefix, withdrawal.ToByteArray());
            /* update approved withdrawal nonce */
            SetApprovedWithdrawalNonce(withdrawal.Nonce);
            return true;
        }

        public Withdrawal GetWithdrawalByNonceAndDelete(ulong nonce)
        {
            var prefix = EntryPrefix.WithdrawalByNonce.BuildPrefix(nonce);
            var raw = _rocksDbContext.Get(prefix);
            _rocksDbContext.Delete(prefix);
            return raw == null ? null : Withdrawal.Parser.ParseFrom(raw);
        }

        public ulong GetCurrentWithdrawalNonce()
        {
            var prefix = EntryPrefix.WithdrawalNonce.BuildPrefix();
            var raw = _rocksDbContext.Get(prefix);
            var global = GlobalWithdrawal.Parser.ParseFrom(raw);
            return global?.CurrentNonce ?? 0UL;
        }

        public ulong GetApprovedWithdrawalNonce()
        {
            var prefix = EntryPrefix.WithdrawalNonce.BuildPrefix();
            var raw = _rocksDbContext.Get(prefix);
            var global = GlobalWithdrawal.Parser.ParseFrom(raw);
            return global?.ApprovedNonce ?? 0UL;
        }

        private void SetCurrentWithdrawalNonce(ulong currentNonce)
        {
            var prefix = EntryPrefix.WithdrawalNonce.BuildPrefix();
            var raw = _rocksDbContext.Get(prefix);
            var global = GlobalWithdrawal.Parser.ParseFrom(raw) ?? new GlobalWithdrawal();
            global.CurrentNonce = currentNonce;
            _rocksDbContext.Save(prefix, global.ToByteArray());
        }
        
        private void SetApprovedWithdrawalNonce(ulong approvedNonce)
        {
            var prefix = EntryPrefix.WithdrawalNonce.BuildPrefix();
            var raw = _rocksDbContext.Get(prefix);
            var global = GlobalWithdrawal.Parser.ParseFrom(raw) ?? new GlobalWithdrawal();
            global.ApprovedNonce = approvedNonce;
            _rocksDbContext.Save(prefix, global.ToByteArray());
        }
    }
}