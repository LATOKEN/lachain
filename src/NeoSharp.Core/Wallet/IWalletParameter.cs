namespace NeoSharp.Core.Wallet
{
    public interface IWalletParameter
    {
        string Name { get; }
        string Type { set; }
    }
}