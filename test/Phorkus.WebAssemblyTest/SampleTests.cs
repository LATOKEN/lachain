﻿using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;

namespace Phorkus.WebAssemblyTest
{
    using Module = WebAssembly.Module;

    /// <summary>
    /// Verifies proper functionality when handling small externally-generate WASM sources.
    /// </summary>
    [TestClass]
    public class SampleTests
    {
        /// <summary>
        /// The data acquired from calls to <see cref="Issue7Receive(int)"/>
        /// </summary>
        private static readonly StringBuilder issue7Received = new StringBuilder();

        /// <summary>
        /// Used with <see cref="Execute_Sample_Issue7"/> to verify a call out from a WebAssembly file.
        /// </summary>
        /// <param name="value">The input.</param>
        public static void Issue7Receive(int value)
        {
            issue7Received.Append((char) value);
        }

        /// <summary>
        /// Verifies proper parsing of the sample provided via https://github.com/RyanLamansky/dotnet-webassembly/issues/7 .
        /// This sample was produced via a very simple program built with https://webassembly.studio/ .
        /// </summary>
        [TestMethod]
        public void Parse_Sample_Issue7()
        {
            using (var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Phorkus.WebAssemblyTest.Samples.Issue7.wasm"))
            {
                var module = Module.ReadFromBinary(stream);

                Assert.AreEqual(2, module.Codes.Count);
                Assert.AreEqual(9, module.CustomSections.Count);
                Assert.AreEqual(0, module.Data.Count);
                Assert.AreEqual(0, module.Elements.Count);
                Assert.AreEqual(4, module.Exports.Count);
                Assert.AreEqual(2, module.Functions.Count);
                Assert.AreEqual(1, module.Imports.Count);
                Assert.AreEqual(1, module.Memories.Count);
                Assert.IsNull(module.Start);
                Assert.AreEqual(1, module.Tables.Count);
                Assert.AreEqual(3, module.Types.Count);
            }
        }

        /// <summary>
        /// Verifies proper functionality of the sample provided via https://github.com/RyanLamansky/dotnet-webassembly/issues/7 .
        /// This sample was produced via a very simple program built with https://webassembly.studio/ .
        /// </summary>
        [TestMethod]
        public void Execute_Sample_Issue7()
        {
            using (var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Phorkus.WebAssemblyTest.Samples.Issue7.wasm"))
            {
                var compiled = Compile.FromBinary<dynamic>(stream,
                    new RuntimeImport[]
                    {
                        new FunctionImport("env", "sayc",
                            typeof(SampleTests).GetTypeInfo().GetMethod(nameof(Issue7Receive)))
                    })();
                Assert.AreEqual<int>(0, compiled.Exports.main());
            }

            Assert.AreEqual("Hello World (from WASM)\n", issue7Received.ToString());
        }
    }
}