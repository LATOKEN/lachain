using System;
using NeoSharp.Core.SmartContract.ContractParameters;
using NeoSharp.VM;
using NeoSharp.VM.Types;

namespace NeoSharp.Core.Extensions
{
    public static class ScriptBuilderExtensions
    {
        public static void PushContractParameter(this ScriptBuilder scriptBuilder, ContractParameter parameter)
        {
            parameter.PushIntoScriptBuilder(scriptBuilder);
        }
    }
}
