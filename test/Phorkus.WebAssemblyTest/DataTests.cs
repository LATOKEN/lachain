﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest
{
    /// <summary>
    /// Tests the <see cref="Data"/> class for proper behaviors.
    /// </summary>
    [TestClass]
    public class DataTests
    {
        /// <summary>
        /// Ensures that <see cref="Data"/> instances have full mutability when read from a file.
        /// </summary>
        [TestMethod]
        public void Data_MutabilityFromBinaryFile()
        {
            var module = new Module
            {
                Data = new[]
                {
                    new Data
                    {
                        InitializerExpression = new Instruction[]
                        {
                            new Int32Constant(0),
                            new End(),
                        },
                    },
                },
            }.BinaryRoundTrip();

            Assert.IsNotNull(module.Data);
            Assert.AreEqual(1, module.Data.Count);

            var data = module.Data[0];
            Assert.IsNotNull(data);

            var initializerExpression = data.InitializerExpression;
            Assert.IsNotNull(initializerExpression);
            Assert.AreEqual(2, initializerExpression.Count);

            //Testing mutability here.
            initializerExpression.Clear();
            initializerExpression.Add(new Int32Constant(0));
        }
    }
}