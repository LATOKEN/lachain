namespace Phorkus.Core.Design
{
    public interface IAsset
    {
        UInt160 Address { get; }
        string Name { get; }
        string Description { get; }
        AssetStatus Status { get; }
        uint Decimals { get; }
        AssetFlags Flags { get; }
    }
}