using Lachain.Proto;

namespace Lachain.Consensus
{
    public static class ConsensusUtils
    {
        public static string PrettyTypeString(this ConsensusMessage m)
        {
            return m.PayloadCase switch
            {
                ConsensusMessage.PayloadOneofCase.ValMessage =>
                    $"VAL(Er={m.Validator.Era}, A={m.ValMessage.SenderId})",
                ConsensusMessage.PayloadOneofCase.EchoMessage =>
                    $"ECHO(Er={m.Validator.Era}, A={m.EchoMessage.SenderId})",
                ConsensusMessage.PayloadOneofCase.ReadyMessage =>
                    $"READY(Er={m.Validator.Era}, A={m.ReadyMessage.SenderId})",
                _ => $"{m.PayloadCase}(Er={m.Validator.Era})"
            };
        }
    }
}