namespace Phorkus.Utility.Utils
{
    public static class BitsUtils
    {
        public static uint Popcount(uint x)
        {
            x -= x >> 1 & 0x55555555;
            x = (x & 0x33333333) + (x >> 2 & 0x33333333);
            x = x + (x >> 4) & 0x0f0f0f0f;
            x += x >> 8;
            x += x >> 16;
            return x & 0x7f;
        }

        public static uint PositionOf(uint mask, byte h)
        {
            return Popcount(mask & ((1u << h) - 1));
        }
    }
}