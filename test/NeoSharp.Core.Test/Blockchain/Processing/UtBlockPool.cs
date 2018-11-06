﻿using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoSharp.Core.Blockchain.Genesis;
using NeoSharp.Core.Blockchain.Processing;
using NeoSharp.Core.Exceptions;
using NeoSharp.Core.Models;
using NeoSharp.TestHelpers;
using NeoSharp.Types;

namespace NeoSharp.Core.Test.Blockchain.Processing
{
    [TestClass]
    public class UtBlockPool : TestBase
    {
        [TestMethod]
        [Ignore]
        public void Add_AddValidBlock_PoolHasOneElementAndOnAddedEventFired()
        {
            // Arrange
            var addedBlock = new Block
            {
                Hash = UInt256.Parse("d4dab99ed65c3655a9619b215ab1988561b706b6e5196b6e0ada916aa6601622")
            };

            var testee = this.AutoMockContainer.Create<BlockPool>();

            var onAddedFired = false;

            // Act
            testee.TryAdd(addedBlock);

            // Assert
            testee.Size
                .Should()
                .Be(1);
            onAddedFired
                .Should()
                .BeTrue();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Add_NullBlock_ArgumetNullExceptionThrown()
        {
            // Arrange
            var testee = this.AutoMockContainer.Create<BlockPool>();

            // Act
            testee.TryAdd(null);
        }

        [TestMethod]
        [ExpectedException(typeof(BlockAlreadyQueuedException))]
        [Ignore]
        public void Add_TwiceSameBlock_InvalidOperationExceptionThrown()
        {
            // Arrange
            var addedBlock = new Block
            {
                Hash = UInt256.Parse("d4dab99ed65c3655a9619b215ab1988561b706b6e5196b6e0ada916aa6601622")
            };

            var testee = this.AutoMockContainer.Create<BlockPool>();

            // Act
            testee.TryAdd(addedBlock);
            testee.TryAdd(addedBlock);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        [Ignore("There is no code in BlockPool that satisfy this logic. Maybe was moved to another place.")]
        public void Add_GenesisBlockWithIndexDifferentFromZero_InvalidOperationExceptionThrown()
        {
            // Arrange
            var genesisBuilder = this.AutoMockContainer.Create<GenesisBuilder>();
            var genesisBlock = genesisBuilder.Build();

            var indexField = typeof(Block).GetField("Index", BindingFlags.Instance | BindingFlags.Public);
            indexField.SetValue(genesisBlock, (uint)1);

            var testee = this.AutoMockContainer.Create<BlockPool>();

            // Act
            testee.TryAdd(genesisBlock);
        }
    }
}
