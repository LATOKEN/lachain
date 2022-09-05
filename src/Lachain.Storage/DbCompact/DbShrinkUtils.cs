using System.Collections.Generic;

namespace Lachain.Storage.DbCompact
{
    public static class DbShrinkUtils
    {
        public static int counter = DbUpdatePeriod();

        public static int DbUpdatePeriod()
        {
            return 100000; // must be a positive integer;
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

    public class ByteKeyComparer : IComparer<byte[]>
    {
        public int Compare(byte[]? x, byte[]? y)
        {
            if (x is null) return y is null ? 0 : -1; // nulls first, just in case
            if (y is null) return 1;
            
            for (int iter = 0 ; iter < x.Length; iter++)
            {
                if (iter >= y.Length) return 1;
                if (x[iter] != y[iter]) return x[iter].CompareTo(y[iter]);
            }
            return x.Length == y.Length ? 0 : -1;
        }
    }
}