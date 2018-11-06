
namespace Phorkus.Core.Design
{    
    public interface IToken
    {
        UInt160 Asset { get; }
        TokenStandard Standard { get; }
        UInt160 Address { get; }
        string Name { get; }
        uint Decimals { get; }
        UInt160 Owner { get; }
        UInt160 External { get; }
    }
}