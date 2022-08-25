using AustinHarris.JsonRpc;
using System;

namespace Lachain.Core.RPC
{
    public static class ExceptionHandler
    {
        public static void WarningException(string message, object? data)
        {
            throw new JsonRpcException((byte) RpcErrorCode.Warning, message, data);
        }

        public static void ErrorException(string message, object? data)
        {
            throw new JsonRpcException((byte) RpcErrorCode.Error, message, data);
        }
    }
}