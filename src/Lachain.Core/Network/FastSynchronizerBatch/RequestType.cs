namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public enum RequestType : byte
    {
        BlocksRequest = 0,
        NodesRequest = 1,
        SingleBlockRequest = 2,
        RootHashRequest = 3,
    }
}