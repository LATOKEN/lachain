namespace Phorkus.VM
{
    public enum GasPriceTier
    {
        ZeroTier = 0,   // 0, Zero
        BaseTier,       // 2, Quick
        VeryLowTier,    // 3, Fastest
        LowTier,        // 5, Fast
        MidTier,        // 8, Mid
        HighTier,       // 10, Slow
        ExtTier,        // 20, Ext
        SpecialTier,    // multiparam or otherwise special
        InvalidTier     // Invalid.
    }
}