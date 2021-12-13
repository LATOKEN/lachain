using System; 

namespace Lachain.Core.Blockchain.Hardfork
{
    public static class HardforkHeights
    {
        private static bool alreadySet;
        public static ulong Hardfork_1
        {
            get; 
            private set;
        }

        public static ulong Hardfork_2
        {
            get; 
            private set;
        }

        public static void SetHardforkHeights(HardforkConfig hardforkConfig)
        {
            if(alreadySet == true) 
                throw new Exception("trying to set hardfork heights more than once");

            alreadySet = true;

            if(hardforkConfig.Hardfork_1 is null)
                throw new Exception("hardfork_1 is null");
            Hardfork_1 = (ulong) hardforkConfig.Hardfork_1;

            if(hardforkConfig.Hardfork_2 is null)
                throw new Exception("hardfork_2 is null");
            Hardfork_2 = (ulong) hardforkConfig.Hardfork_2;
        }
    }
}