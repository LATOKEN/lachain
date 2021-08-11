using System.Runtime.CompilerServices;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Storage.Trie;
using System.Collections.Generic;


namespace Lachain.Storage.State
{
    public class ContractSnapshot : IContractSnapshot
    {
        private readonly IStorageState _state;

        public ContractSnapshot(IStorageState state)
        {
            _state = state;
        }

        public IDictionary<ulong,IHashTrieNode> GetState()
        {
            return _state.GetAllNodes();
        }
        
        public bool IsTrieNodeHashesOk()
        {
            return _state.IsNodeHashesOk();
        }

        public ulong Version => _state.CurrentVersion;
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 Hash => _state.Hash;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Contract? GetContractByHash(UInt160 contractHash)
        {
            var value = _state.Get(EntryPrefix.ContractByHash.BuildPrefix(contractHash));
            return value != null ? Contract.FromBytes(value) : null;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddContract(UInt160 sender, Contract contract)
        {
            _state.AddOrUpdate(EntryPrefix.ContractByHash.BuildPrefix(contract.ContractAddress), contract.ToBytes());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void DeleteContractByHash(UInt160 contractHash)
        {
            _state.Delete(EntryPrefix.ContractByHash.BuildPrefix(contractHash), out _);
        }
    }
}