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
    public class ReserveFundContract : ISystemContract
    {
        
        private readonly ContractContext _contractContext;
        
        private static readonly ILogger<ReserveFundContract> Logger =
            LoggerFactory.GetLoggerForClass<ReserveFundContract>();

        public ReserveFundContract(ContractContext contractContext)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
        }

        public ContractStandard ContractStandard => ContractStandard.ReserveFundContract;

        [ContractMethod(ReserveFundInterface.MethodPack)]
        public UInt256 Pack(UInt256 laAmount)
        {
            EnsurePositive(laAmount);

            var laToken = new NativeTokenContract(_contractContext);
            
            var balance = laToken.BalanceOf(MsgSender()) ?? UInt256Utils.Zero;
            if (balance.ToMoney().CompareTo(laAmount.ToMoney(true)) == -1)
            {
                throw new Exception("Insufficient balance");
            }
            
            var laInFund = laToken.BalanceOf(ContractRegisterer.ReserveFundContract);
            
            var ok = laToken.Transfer(ContractRegisterer.ReserveFundContract, laAmount);
            if (!ok)
            {
                throw new Exception("Transfer failure");
            }

            var user = MsgSender();
            
            // change the sender of the transaction to perform token minting 
            _contractContext.Sender = ContractRegisterer.ReserveFundContract;
            var lrfToken = new ReserveFundTokenContract(_contractContext);
            var lrfSupply = lrfToken.TotalSupply();
            BigInteger lrfAmount;
            if (lrfSupply.Equals(UInt256Utils.Zero))
            {
                lrfAmount = laAmount.ToBigInteger(true);
            }
            else
            {
                lrfAmount = laAmount.ToBigInteger(true) * lrfSupply.ToBigInteger(true) / laInFund.ToBigInteger(true);
            }

            if (!lrfToken.Mint(user, lrfAmount.ToUInt256())) 
                throw new Exception("Mint failure");
            
            return lrfAmount.ToUInt256();
        }

        [ContractMethod(ReserveFundInterface.MethodRedeem)]
        public UInt256 Redeem(UInt256 lrfAmount)
        {
            EnsurePositive(lrfAmount);

            // change the sender of the transaction to burn tokens and transfer from this contract
            _contractContext.Sender = ContractRegisterer.ReserveFundContract;
            
            var lrfToken = new ReserveFundTokenContract(_contractContext);
            var laToken = new NativeTokenContract(_contractContext);
            
            var balance = lrfToken.BalanceOf(MsgSender());
            if (balance.ToMoney().CompareTo(lrfAmount.ToMoney(true)) == -1)
            {
                throw new Exception("Insufficient balance");
            }

            var user = MsgSender();
            var lrfSupply = lrfToken.TotalSupply();
            var laInFund = laToken.BalanceOf(ContractRegisterer.ReserveFundContract);
           
            var laAmount = lrfAmount.ToBigInteger(true) * laInFund.ToBigInteger(true) / lrfSupply.ToBigInteger(true);
            
            if (!lrfToken.Burn(user, lrfAmount))
            {
                throw new Exception("Burn failure");
            }
            
            if (!laToken.Transfer(user, laAmount.ToUInt256()))
            {
                throw new Exception("Transfer failure");
            }
            
            return laAmount.ToUInt256();
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