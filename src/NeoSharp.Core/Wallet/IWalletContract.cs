namespace NeoSharp.Core.Wallet
{
    public interface IWalletContract
    {
        UInt160 ScriptHash { get; }

        string Script { get; }
        
        IWalletParameter[] Parameters { get; }
    }
}