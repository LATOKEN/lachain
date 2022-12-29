using System.Linq;
using System.Numerics;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Utility
{
    public static class SystemContractAddresses
    {
        public static readonly UInt160 DeployContract = new BigInteger(0).ToUInt160();
        public static readonly UInt160 LatokenContract = new BigInteger(1).ToUInt160();
        public static readonly UInt160 GovernanceContract = new BigInteger(2).ToUInt160();
        public static readonly UInt160 StakingContract = new BigInteger(3).ToUInt160();
        public static readonly UInt160 NativeTokenContract = new BigInteger(4).ToUInt160();

        public static bool IsSystemContract(UInt160? address)
        {
            if (address is null)
                return false;
            var contracts = typeof(SystemContractAddresses).GetFields()
                .Where(x => x.FieldType.Equals(typeof(UInt160)) && address.Equals(x.GetValue(x)))
                .ToArray();
            return contracts.Length > 0;
        }
    }
}