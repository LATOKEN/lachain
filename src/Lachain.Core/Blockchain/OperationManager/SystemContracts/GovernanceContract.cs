using System;
using System.Linq;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.Blockchain.ContractManager.Attributes;
using Lachain.Core.Blockchain.ContractManager.Standards;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.OperationManager.SystemContracts
{
    public class GovernanceContract : ISystemContract
    {
        private readonly ContractContext _contractContext;

        public GovernanceContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
        }

        public ContractStandard ContractStandard => ContractStandard.GovernanceContract;

        public void ChangeValidators(byte[][] newValidators)
        {
            _contractContext.Snapshot.Validators.NewValidators(
                newValidators.Select(x => x.ToPublicKey())
            );
            // TODO: validate everything
        }

        [ContractProperty(GovernanceInterface.MethodKeygenCommit)]
        public void KeyGenCommit(byte[] commitment, byte[][] encryptedRows)
        {
            // TODO: validate everything
        }

        [ContractProperty(GovernanceInterface.MethodKeygenSendValue)]
        public void KeyGenSendValue(UInt256 proposer, byte[][] encryptedValues)
        {
            // TODO: validate everything
        }

        [ContractProperty(GovernanceInterface.MethodKeygenConfirm)]
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
            } 
        }
    }
}