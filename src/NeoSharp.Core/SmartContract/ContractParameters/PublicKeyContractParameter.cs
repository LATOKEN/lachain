using NeoSharp.Core.Cryptography;
using NeoSharp.VM;
using NeoSharp.VM.Types;

namespace NeoSharp.Core.SmartContract.ContractParameters
{
    public class PublicKeyContractParameter : ContractParameter
    {
        public PublicKeyContractParameter(PublicKey value) : base(ContractParameterType.PublicKey, value) { }

        public override void PushIntoScriptBuilder(ScriptBuilder scriptBuilder)
        {
            var valueAsECPoint = Value as PublicKey;
            scriptBuilder.EmitPush(valueAsECPoint.EncodedData);
        }
    }
}
