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
        private static ulong Hardfork_7;
        private static ulong Hardfork_8;
        private static ulong Hardfork_9;
        private static ulong Hardfork_10;
        private static ulong Hardfork_11;
        private static ulong Hardfork_12;

        //we need this value as default deploy height
        public static ulong GetHardfork_3() 
        {
            return Hardfork_3;
        }

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

        public static bool IsHardfork_7Active(ulong height)
        {
            return height >= Hardfork_7;
        }

        public static bool IsHardfork_8Active(ulong height)
        {
            return height >= Hardfork_8;
        }

        
        public static bool IsHardfork_9Active(ulong height)
        {
            return height >= Hardfork_9;
        }
        
        public static bool IsHardfork_10Active(ulong height)
        {
            return height >= Hardfork_10;
        }

        public static bool IsHardfork_11Active(ulong height)
        {
            return height >= Hardfork_11;
        }

        public static bool IsHardfork_12Active(ulong height)
        {
            return height >= Hardfork_12;
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

            if(hardforkConfig.Hardfork_7 is null)
                throw new Exception("hardfork_7 is null");
            Hardfork_7 = (ulong) hardforkConfig.Hardfork_7;

            if(hardforkConfig.Hardfork_8 is null)
                throw new Exception("hardfork_8 is null");
            Hardfork_8 = (ulong) hardforkConfig.Hardfork_8;
            
            if(hardforkConfig.Hardfork_9 is null)
                throw new Exception("hardfork_9 is null");
            Hardfork_9 = (ulong) hardforkConfig.Hardfork_9;
            
            if(hardforkConfig.Hardfork_10 is null)
                throw new Exception("hardfork_10 is null");
            Hardfork_10 = (ulong) hardforkConfig.Hardfork_10;

            if(hardforkConfig.Hardfork_11 is null)
                throw new Exception("hardfork_11 is null");
            Hardfork_11 = (ulong) hardforkConfig.Hardfork_11;

            if(hardforkConfig.Hardfork_12 is null)
                throw new Exception("hardfork_12 is null");
            Hardfork_12 = (ulong) hardforkConfig.Hardfork_12;
        }
    }
}
