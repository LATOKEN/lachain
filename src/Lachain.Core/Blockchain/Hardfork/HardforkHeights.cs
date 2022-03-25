using System; 

namespace Lachain.Core.Blockchain.Hardfork
{
    public static class HardforkHeights
    {
        private static bool alreadySet;
        private static ulong Hardfork_1;
        private static ulong Hardfork_2;
        private static ulong Hardfork_3;
        private static ulong Hardfork_4;
        private static ulong Hardfork_5;
        private static ulong Hardfork_6;

        public static bool IsHardfork_1Active(ulong height)
        {
            return height >= Hardfork_1;
        }

        public static bool IsHardfork_2Active(ulong height)
        {
            return height >= Hardfork_2;
        }

        public static bool IsHardfork_3Active(ulong height)
        {
            return height >= Hardfork_3;
        }

        public static bool IsHardfork_4Active(ulong height)
        {
            return height >= Hardfork_4;
        }

        public static bool IsHardfork_5Active(ulong height)
        {
            return height >= Hardfork_5;
        }

        public static bool IsHardfork_6Active(ulong height)
        {
            return height >= Hardfork_6;
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
            
            if(hardforkConfig.Hardfork_3 is null)
                throw new Exception("hardfork_3 is null");
            Hardfork_3 = (ulong) hardforkConfig.Hardfork_3;

            if(hardforkConfig.Hardfork_4 is null)
                throw new Exception("hardfork_4 is null");
            Hardfork_4 = (ulong) hardforkConfig.Hardfork_4;

            if(hardforkConfig.Hardfork_5 is null)
                throw new Exception("hardfork_5 is null");
            Hardfork_5 = (ulong) hardforkConfig.Hardfork_5;
            
            if(hardforkConfig.Hardfork_6 is null)
                throw new Exception("hardfork_6 is null");
            Hardfork_6 = (ulong) hardforkConfig.Hardfork_6;

        }
    }
}