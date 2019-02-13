using System;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public class WithdrawalSnapshot : IWithdrawalSnapshot
    {
        private readonly IStorageState _state;

        public WithdrawalSnapshot(IStorageState state)
        {
            _state = state;
        }

        public ulong Version => _state.CurrentVersion;
        
        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 Hash => _state.Hash;

        public Withdrawal GetWithdrawalByHash(UInt256 withdrawalHash)
        {
            var raw = _state.Get(EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawalHash));
            return raw != null ? Withdrawal.Parser.ParseFrom(raw) : null;
        }

        public bool ContainsWithdrawalByHash(UInt256 withdrawalHash)
        {
            var raw = _state.Get(EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawalHash));
            return raw != null;
        }

        public Withdrawal GetWithdrawalByNonce(ulong nonce)
        {
            var raw = _state.Get(EntryPrefix.WithdrawalByNonce.BuildPrefix(nonce));
            return raw != null ? Withdrawal.Parser.ParseFrom(raw) : null;
        }

        public bool ContainsWithdrawalByNonce(ulong nonce)
        {
            var raw = _state.Get(EntryPrefix.WithdrawalByNonce.BuildPrefix(nonce));
            return raw != null;
        }

        public bool AddWithdrawal(Withdrawal withdrawal)
        {
            /* validate withdrawal nonce */
            var currentNonce = GetCurrentWithdrawalNonce();
            if (withdrawal.Nonce == 0)
                withdrawal.Nonce = currentNonce;
            else if (currentNonce + 1 != withdrawal.Nonce)
                return false;
            if (withdrawal.State != WithdrawalState.Registered)
                return false;
            /* write Withdrawal to storage */
            var prefixTx = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawal.TransactionHash);
            _state.Add(prefixTx, withdrawal.ToByteArray());
            var prefixNonce = EntryPrefix.WithdrawalByNonce.BuildPrefix(withdrawal.Nonce);
            _state.Add(prefixNonce, withdrawal.TransactionHash.ToByteArray());
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
            _state.AddOrUpdate(prefix, withdrawal.ToByteArray());
            return withdrawal;
        }

        public bool ConfirmWithdrawal(UInt256 withdrawalHash, byte[] rawTransaction, byte[] transactionHash)
        {
            /* try to find withdrawal */
            var withdrawal = GetWithdrawalByHash(withdrawalHash);
            if (withdrawal is null || withdrawal.State != WithdrawalState.Registered)
                return false;
            /* change approve fields */
            withdrawal.Timestamp = (ulong) new DateTimeOffset().ToUnixTimeMilliseconds();
            withdrawal.State = WithdrawalState.Sent;
            withdrawal.OriginalTransaction = ByteString.CopyFrom(rawTransaction);
            withdrawal.OriginalHash = ByteString.CopyFrom(transactionHash);
            /* write new withdrawal */
            var prefix = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawalHash);
            _state.AddOrUpdate(prefix, withdrawal.ToByteArray());
            return true;   
        }

        public bool ApproveWithdrawal(UInt256 withdrawalHash)
        {
            /* try to find withdrawal */
            var withdrawal = GetWithdrawalByHash(withdrawalHash);
            if (withdrawal is null || withdrawal.State != WithdrawalState.Sent)
                return false;
            /* validate withdrawal nonce */
            var approvedNonce = GetApprovedWithdrawalNonce();
            if (withdrawal.Nonce != approvedNonce + 1)
                return false;
            /* change approve fields */
            withdrawal.State = WithdrawalState.Approved;
            /* write new withdrawal */
            var prefix = EntryPrefix.WithdrawalByHash.BuildPrefix(withdrawalHash);
            _state.AddOrUpdate(prefix, withdrawal.ToByteArray());
            /* update approved withdrawal nonce */
            SetApprovedWithdrawalNonce(withdrawal.Nonce);
            return true;
        }

        public Withdrawal GetWithdrawalByNonceAndDelete(ulong nonce)
        {
            var prefix = EntryPrefix.WithdrawalByNonce.BuildPrefix(nonce);
            _state.Delete(prefix, out var raw);
            return raw != null ? Withdrawal.Parser.ParseFrom(raw) : null;
        }

        public ulong GetCurrentWithdrawalNonce()
        {
            var prefix = EntryPrefix.WithdrawalNonce.BuildPrefix();
            var raw = _state.Get(prefix);
            var global = WithdrawalQueueConfig.Parser.ParseFrom(raw);
            return global?.CurrentNonce ?? 0UL;
        }

        public ulong GetApprovedWithdrawalNonce()
        {
            var prefix = EntryPrefix.WithdrawalNonce.BuildPrefix();
            var raw = _state.Get(prefix);
            var global = WithdrawalQueueConfig.Parser.ParseFrom(raw);
            return global?.ApprovedNonce ?? 0UL;
        }
        
        private void SetCurrentWithdrawalNonce(ulong currentNonce)
        {
            var prefix = EntryPrefix.WithdrawalNonce.BuildPrefix();
            var raw = _state.Get(prefix);
            var global = WithdrawalQueueConfig.Parser.ParseFrom(raw) ?? new WithdrawalQueueConfig();
            global.CurrentNonce = currentNonce;
            _state.AddOrUpdate(prefix, global.ToByteArray());
        }
        
        private void SetApprovedWithdrawalNonce(ulong approvedNonce)
        {
            var prefix = EntryPrefix.WithdrawalNonce.BuildPrefix();
            var raw = _state.Get(prefix);
            var global = WithdrawalQueueConfig.Parser.ParseFrom(raw) ?? new WithdrawalQueueConfig();
            global.ApprovedNonce = approvedNonce;
            _state.AddOrUpdate(prefix, global.ToByteArray());
        }
    }
}