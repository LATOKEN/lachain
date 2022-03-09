using System;
using Lachain.Logger;

namespace Lachain.Storage.DbCompact
{
    public static class DbShrinkUtils
    {
        public static int counter = DbUpdatePeriod();

        public static int DbUpdatePeriod()
        {
            return 10000; // must be a positive integer;
        }

        public static void ResetCounter()
        {
            counter = DbUpdatePeriod();
        }

        public static void UpdateCounter()
        {
            counter--;
        }

        public static bool CycleEnded()
        {
            return counter <= 0;
        }
        
    }
}