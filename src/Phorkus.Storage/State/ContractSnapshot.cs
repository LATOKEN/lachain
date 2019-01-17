using System.Runtime.CompilerServices;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public class ContractSnapshot : IContractSnapshot
    {
        private readonly IStorageState _state;

        public ContractSnapshot(IStorageState state)
        {
            _state = state;
        }

        public ulong Version => _state.CurrentVersion;
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Commit()
        {
            _state.Commit();
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Contract GetContractByHash(UInt160 contractHash)
        {
            var value = _state.Get(EntryPrefix.ContractByHash.BuildPrefix(contractHash));
            return value != null ? Contract.Parser.ParseFrom(value) : null;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddContract(UInt160 from, Contract contract)
        {
            _state.AddOrUpdate(EntryPrefix.ContractByHash.BuildPrefix(contract.Hash), contract.ToByteArray());
            var raw = _state.Get(EntryPrefix.ContractCountByFrom.BuildPrefix(from));
            var global = new ContractGlobal();
            if (raw != null)
                global = ContractGlobal.Parser.ParseFrom(raw);
            global.TotalContracts++;
            _state.AddOrUpdate(EntryPrefix.ContractCountByFrom.BuildPrefix(from), global.ToByteArray());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void DeleteContractByHash(UInt160 contractHash)
        {
            _state.Delete(EntryPrefix.ContractByHash.BuildPrefix(contractHash), out _);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public uint GetTotalContractsByFrom(UInt160 @from)
        {
            var raw = _state.Get(EntryPrefix.ContractCountByFrom.BuildPrefix(from));
            if (raw is null)
                return 0;
            var global = ContractGlobal.Parser.ParseFrom(raw);
            return global.TotalContracts;
        }
    }
}