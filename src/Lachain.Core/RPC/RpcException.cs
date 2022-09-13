using AustinHarris.JsonRpc;

namespace Lachain.Core.RPC
{
    public class RpcException : JsonRpcException
    {
        public RpcException(RpcErrorCode errorCode, string message, object? data = null)
            : base((byte) errorCode, message, data)
        {
            
        }
    }
}