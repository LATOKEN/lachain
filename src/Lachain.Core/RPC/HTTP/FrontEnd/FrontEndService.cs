using System.Linq;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Logger;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.RPC.HTTP.FrontEnd
{
    public class FrontEndService : JsonRpcService
    {
        private readonly IStateManager _stateManager;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IContractRegisterer _contractRegisterer;
        private readonly ISystemContractReader _systemContractReader;
        private readonly ILogger<FrontEndService> _logger = LoggerFactory.GetLoggerForClass<FrontEndService>();

        public FrontEndService(
            IStateManager stateManager,
            ITransactionManager transactionManager,
            ITransactionPool transactionPool,
            IContractRegisterer contractRegisterer,
            ISystemContractReader systemContractReader
            )
        {
            _stateManager = stateManager;
            _transactionManager = transactionManager;
            _transactionPool = transactionPool;
            _contractRegisterer = contractRegisterer;
            _systemContractReader = systemContractReader;
        }

        [JsonRpcMethod("fe_getBalance")]
        private JObject GetBalance(string address)
        {
            var addressUint160 = address.HexToBytes().ToUInt160();
            var balance =
                _stateManager.LastApprovedSnapshot.Balances.GetBalance(addressUint160);

            var stake = _systemContractReader.GetStake(addressUint160).ToMoney();
            var penalty = _systemContractReader.GetPenalty(addressUint160).ToMoney();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(
                addressUint160);
            return new JObject
            {
                ["balance"] = balance.ToString(),
                ["stake"] = stake.ToString(),
                ["penalty"] = penalty.ToString(),
                ["nonce"] = nonce,
            };
        }

        [JsonRpcMethod("fe_account")]
        private JObject GetAccount(string address = null)
        {
            address ??= _systemContractReader.NodeAddress().ToHex();
            var addressUint160 = address.HexToBytes().ToUInt160();
            var balance =
                _stateManager.LastApprovedSnapshot.Balances.GetBalance(addressUint160);

            var stake = _systemContractReader.GetStake(addressUint160).ToMoney();
            var penalty = _systemContractReader.GetPenalty(addressUint160).ToMoney();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(
                addressUint160);
            return new JObject
            {
                ["address"] = address,
                ["balance"] = balance.ToString(),
                ["stake"] = stake.ToString(),
                ["penalty"] = penalty.ToString(),
                ["isValidator"] = _stateManager.CurrentSnapshot.Validators.GetValidatorsPublicKeys().Select(pk => pk.Buffer.ToByteArray()).Contains(_systemContractReader.NodePublicKey()),
                ["isNextValidator"] = _systemContractReader.IsNextValidator(),
                ["isPreviousValidator"] = _systemContractReader.IsPreviousValidator(),
            };
        }

        [JsonRpcMethod("fe_phase")]
        private JObject GetCurrentPhase()
        {
            var attendanceDetectionPhase = _systemContractReader.IsAttendanceDetectionPhase();
            var vrfSubmissionPhase = _systemContractReader.IsVrfSubmissionPhase();
            var keyGenPhase = _systemContractReader.IsKeyGenPhase();
           
            return new JObject
            {
                ["AttendanceSubmissionPhase"] = attendanceDetectionPhase,
                ["VrfSubmissionPhase"] = vrfSubmissionPhase,
                ["KeyGenPhase"] = keyGenPhase,
            };
        }
    }
}