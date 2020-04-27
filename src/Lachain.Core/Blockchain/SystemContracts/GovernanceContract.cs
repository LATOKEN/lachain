using System;
using System.Linq;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class GovernanceContract : ISystemContract
    {
        private readonly ContractContext _contractContext;

        private static readonly ILogger<GovernanceContract> Logger =
            LoggerFactory.GetLoggerForClass<GovernanceContract>();

        public GovernanceContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
        }

        public ContractStandard ContractStandard => ContractStandard.GovernanceContract;

        [ContractMethod(GovernanceInterface.MethodChangeValidators)]
        public void ChangeValidators(byte[][] newValidators)
        {
            // TODO: validate everything
            _contractContext.Snapshot.Validators.NewValidators(
                newValidators.Select(x => x.ToPublicKey())
            );
        }

        [ContractMethod(GovernanceInterface.MethodKeygenCommit)]
        public void KeyGenCommit(byte[] commitment, byte[][] encryptedRows)
        {
            // TODO: validate everything
        }

        [ContractMethod(GovernanceInterface.MethodKeygenSendValue)]
        public void KeyGenSendValue(UInt256 proposer, byte[][] encryptedValues)
        {
            // TODO: validate everything
        }

        [ContractMethod(GovernanceInterface.MethodKeygenConfirm)]
        public void KeyGenConfirm(byte[] tpkePublicKey, byte[][] thresholdSignaturePublicKeys)
        {
            // TODO: validate everything
            var faulty = (thresholdSignaturePublicKeys.Length - 1) / 3;
            var tsKeys = new PublicKeySet(thresholdSignaturePublicKeys.Select(PublicKey.FromBytes), faulty);
            var tpkeKey = Crypto.TPKE.PublicKey.FromBytes(tpkePublicKey);
            var confirmations = _contractContext.Snapshot.Validators.ConfirmCredentials(tsKeys, tpkeKey);
            if (confirmations == 2 * faulty + 1)
            {
                _contractContext.Snapshot.Validators.UpdateValidators(tsKeys, tpkeKey);
                Logger.LogError("Enough confirmations collected, validators will be changed in the next block");
            }
        }
    }
}