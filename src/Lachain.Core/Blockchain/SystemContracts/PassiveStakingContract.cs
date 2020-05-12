using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Lachain.Crypto.VRF;
using Lachain.Utility;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class PassiveStakingContract : ISystemContract
    {
        
        private readonly ContractContext _contractContext;
        
        private static readonly ILogger<PassiveStakingContract> Logger =
            LoggerFactory.GetLoggerForClass<PassiveStakingContract>();

        public PassiveStakingContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
        }

        public ContractStandard ContractStandard => ContractStandard.ReserveFundContract;

        [ContractMethod(PassiveStakingInterface.MethodPack)]
        public UInt256 Pack(UInt256 laAmount)
        {
            EnsurePositive(laAmount);

            var laToken = new NativeTokenContract(_contractContext);
            
            var balance = laToken.BalanceOf(MsgSender()) ?? UInt256Utils.Zero;
            if (balance.ToMoney(true).CompareTo(laAmount.ToMoney(true)) == -1)
            {
                throw new Exception("Insufficient balance");
            }
            
            var laInFund = laToken.BalanceOf(ContractRegisterer.PassiveStakingContract);
            
            var ok = laToken.Transfer(ContractRegisterer.PassiveStakingContract, laAmount);
            if (!ok)
            {
                throw new Exception("Transfer failure");
            }

            var user = MsgSender();
            
            // change the sender of the transaction to perform token minting 
            _contractContext.Sender = ContractRegisterer.PassiveStakingContract;
            var lpsToken = new PassiveStakingTokenContract(_contractContext);
            var lpsSupply = lpsToken.TotalSupply();
            BigInteger lpsAmount;
            if (lpsSupply.IsZero())
            {
                lpsAmount = laAmount.ToBigInteger(true);
            }
            else
            {
                lpsAmount = laAmount.ToBigInteger(true) * lpsSupply.ToBigInteger(true) / laInFund.ToBigInteger(true);
            }

            if (!lpsToken.Mint(user, lpsAmount.ToUInt256(true))) 
                throw new Exception("Mint failure");
            
            return lpsAmount.ToUInt256(true);
        }

        [ContractMethod(PassiveStakingInterface.MethodRedeem)]
        public UInt256 Redeem(UInt256 lpsAmount)
        {
            EnsurePositive(lpsAmount);

            var user = MsgSender();
            
            // change the sender of the transaction to burn tokens and transfer from this contract
            _contractContext.Sender = ContractRegisterer.PassiveStakingContract;
            
            var lpsToken = new PassiveStakingTokenContract(_contractContext);
            var laToken = new NativeTokenContract(_contractContext);
            
            var balance = lpsToken.BalanceOf(user);
            if (balance.ToMoney(true).CompareTo(lpsAmount.ToMoney(true)) == -1)
            {
                throw new Exception("Insufficient balance");
            }

            var lpsSupply = lpsToken.TotalSupply();
            var laInFund = laToken.BalanceOf(ContractRegisterer.PassiveStakingContract);
           
            var laAmount = lpsAmount.ToBigInteger(true) * laInFund.ToBigInteger(true) / lpsSupply.ToBigInteger(true);
            
            if (!lpsToken.Burn(user, lpsAmount))
            {
                throw new Exception("Burn failure");
            }
            
            if (!laToken.Transfer(user, laAmount.ToUInt256(true)))
            {
                throw new Exception("Transfer failure");
            }
            
            return laAmount.ToUInt256(true);
        }

        private static void EnsurePositive(UInt256 amount)
        {
            if (amount.ToMoney(true).CompareTo(UInt256Utils.ToUInt256(0).ToMoney()) != 1)
            {
                throw new Exception("Should be positive");
            }
        }

        private UInt160 MsgSender()
        {
            return _contractContext.Sender ?? throw new InvalidOperationException();
        }
    }
}