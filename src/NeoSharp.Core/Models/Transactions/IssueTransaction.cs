using NeoSharp.BinarySerialization;
using NeoSharp.Core.Converters;
using Newtonsoft.Json;

namespace NeoSharp.Core.Models.Transactions
{
    [BinaryTypeSerializer(typeof(TransactionSerializer))]
    public class IssueTransaction : Transaction
    {
        [JsonProperty("asset")]
        public UInt160 Asset { get; set; }
        
        [JsonProperty("amount")]
        public UInt256 Amount { get; set; }

        /// <inheritdoc />
        public IssueTransaction() : base(TransactionType.IssueTransaction)
        {
        }
    }
}