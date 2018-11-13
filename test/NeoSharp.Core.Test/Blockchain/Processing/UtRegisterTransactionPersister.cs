using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NeoSharp.Core.Blockchain.Processing.TranscationProcessing;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.Transactions;
using NeoSharp.Core.Storage;
using NeoSharp.TestHelpers;
using NeoSharp.Types;

namespace NeoSharp.Core.Test.Blockchain.Processing
{
    [TestClass]
    public class UtRegisterTransactionPersister : TestBase
    {
        /*
        [TestMethod]
        public async Task Persist_AddsContract()
        {
            var pubKey = new byte[33];
            pubKey[0] = 0x02;
            var input = new RegisterTransaction
            {
                Hash = UInt256.FromHex(RandomInt().ToString("X64")),
                AssetType = (AssetType) RandomInt(16),
                Name = RandomString(10),
                Supply = UInt256.FromDec(RandomInt().ToString()),
                Precision = (byte) RandomInt(8),
                //Owner = new PublicKey(pubKey),
                Owner = UInt160.Parse(RandomInt().ToString("X40"))
            };
            var repositoryMock = AutoMockContainer.GetMock<IRepository>();
            var testee = AutoMockContainer.Create<RegisterTransactionPersister>();

            await testee.Persist(input);
            repositoryMock.Verify(m => m.AddAsset(It.Is<Asset>(a =>
                a.Hash.Equals(input.Hash) &&
                a.AssetType.Equals(input.AssetType) &&
                a.Name == input.Name &&
                a.Amount.Equals(input.Supply) &&
                a.Precision == input.Precision &&
                a.Owner.CompareTo(input.Owner) == 0 &&
                a.Owner.Equals(input.Owner))));
        }
        */
    }
}