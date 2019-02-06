﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
    /// <summary>
    /// Tests the <see cref="GetGlobal"/> instruction.
    /// </summary>
    [TestClass]
    public class GetGlobalTests
    {
        /// <summary>
        /// Used to test a single return with no parameters.
        /// </summary>
        public abstract class TestBase
        {
            /// <summary>
            /// Returns a value.
            /// </summary>
            public abstract int TestInt32();

            /// <summary>
            /// Returns a value.
            /// </summary>
            public abstract long TestInt64();

            /// <summary>
            /// Returns a value.
            /// </summary>
            public abstract float TestFloat32();

            /// <summary>
            /// Returns a value.
            /// </summary>
            public abstract double TestFloat64();
        }

        /// <summary>
        /// Tests compilation and execution of the <see cref="GetGlobal"/> instruction for immutable values.
        /// </summary>
        [TestMethod]
        public void GetGlobal_Immutable_Compiled()
        {
            var module = new Module();
            module.Types.Add(new Type
            {
                Parameters = new ValueType[]
                {
                },
                Returns = new[]
                {
                    ValueType.Int32,
                }
            });
            module.Types.Add(new Type
            {
                Parameters = new ValueType[]
                {
                },
                Returns = new[]
                {
                    ValueType.Int64,
                }
            });
            module.Types.Add(new Type
            {
                Parameters = new ValueType[]
                {
                },
                Returns = new[]
                {
                    ValueType.Float32,
                }
            });
            module.Types.Add(new Type
            {
                Parameters = new ValueType[]
                {
                },
                Returns = new[]
                {
                    ValueType.Float64,
                }
            });
            module.Functions.Add(new Function
            {
                Type = 0,
            });
            module.Functions.Add(new Function
            {
                Type = 1,
            });
            module.Functions.Add(new Function
            {
                Type = 2,
            });
            module.Functions.Add(new Function
            {
                Type = 3,
            });
            module.Globals.Add(new Global
            {
                ContentType = ValueType.Int32,
                InitializerExpression = new Instruction[]
                {
                    new Int32Constant(4),
                    new End(),
                },
            });
            module.Globals.Add(new Global
            {
                ContentType = ValueType.Int64,
                InitializerExpression = new Instruction[]
                {
                    new Int64Constant(5),
                    new End(),
                },
            });
            module.Globals.Add(new Global
            {
                ContentType = ValueType.Float32,
                InitializerExpression = new Instruction[]
                {
                    new Float32Constant(6),
                    new End(),
                },
            });
            module.Globals.Add(new Global
            {
                ContentType = ValueType.Float64,
                InitializerExpression = new Instruction[]
                {
                    new Float64Constant(7),
                    new End(),
                },
            });
            module.Exports.Add(new Export
            {
                Index = 0,
                Name = nameof(TestBase.TestInt32)
            });
            module.Exports.Add(new Export
            {
                Index = 1,
                Name = nameof(TestBase.TestInt64)
            });
            module.Exports.Add(new Export
            {
                Index = 2,
                Name = nameof(TestBase.TestFloat32)
            });
            module.Exports.Add(new Export
            {
                Index = 3,
                Name = nameof(TestBase.TestFloat64)
            });
            module.Codes.Add(new FunctionBody
            {
                Code = new Instruction[]
                {
                    new GetGlobal(0),
                    new End(),
                },
            });
            module.Codes.Add(new FunctionBody
            {
                Code = new Instruction[]
                {
                    new GetGlobal(1),
                    new End(),
                },
            });
            module.Codes.Add(new FunctionBody
            {
                Code = new Instruction[]
                {
                    new GetGlobal(2),
                    new End(),
                },
            });
            module.Codes.Add(new FunctionBody
            {
                Code = new Instruction[]
                {
                    new GetGlobal(3),
                    new End(),
                },
            });

            var compiled = module.ToInstance<TestBase>();

            var exports = compiled.Exports;
            Assert.AreEqual(4, exports.TestInt32());
            Assert.AreEqual(5, exports.TestInt64());
            Assert.AreEqual(6, exports.TestFloat32());
            Assert.AreEqual(7, exports.TestFloat64());
        }

        /// <summary>
        /// Tests compilation and execution of the <see cref="GetGlobal"/> instruction for mutable values.
        /// </summary>
        [TestMethod]
        public void GetGlobal_Mutable_Compiled()
        {
            var module = new Module();
            module.Types.Add(new Type
            {
                Parameters = new ValueType[]
                {
                },
                Returns = new[]
                {
                    ValueType.Int32,
                }
            });
            module.Types.Add(new Type
            {
                Parameters = new ValueType[]
                {
                },
                Returns = new[]
                {
                    ValueType.Int64,
                }
            });
            module.Types.Add(new Type
            {
                Parameters = new ValueType[]
                {
                },
                Returns = new[]
                {
                    ValueType.Float32,
                }
            });
            module.Types.Add(new Type
            {
                Parameters = new ValueType[]
                {
                },
                Returns = new[]
                {
                    ValueType.Float64,
                }
            });
            module.Functions.Add(new Function
            {
                Type = 0,
            });
            module.Functions.Add(new Function
            {
                Type = 1,
            });
            module.Functions.Add(new Function
            {
                Type = 2,
            });
            module.Functions.Add(new Function
            {
                Type = 3,
            });
            module.Globals.Add(new Global
            {
                IsMutable = true,
                ContentType = ValueType.Int32,
                InitializerExpression = new Instruction[]
                {
                    new Int32Constant(4),
                    new End(),
                },
            });
            module.Globals.Add(new Global
            {
                IsMutable = true,
                ContentType = ValueType.Int64,
                InitializerExpression = new Instruction[]
                {
                    new Int64Constant(5),
                    new End(),
                },
            });
            module.Globals.Add(new Global
            {
                IsMutable = true,
                ContentType = ValueType.Float32,
                InitializerExpression = new Instruction[]
                {
                    new Float32Constant(6),
                    new End(),
                },
            });
            module.Globals.Add(new Global
            {
                IsMutable = true,
                ContentType = ValueType.Float64,
                InitializerExpression = new Instruction[]
                {
                    new Float64Constant(7),
                    new End(),
                },
            });
            module.Exports.Add(new Export
            {
                Index = 0,
                Name = nameof(TestBase.TestInt32)
            });
            module.Exports.Add(new Export
            {
                Index = 1,
                Name = nameof(TestBase.TestInt64)
            });
            module.Exports.Add(new Export
            {
                Index = 2,
                Name = nameof(TestBase.TestFloat32)
            });
            module.Exports.Add(new Export
            {
                Index = 3,
                Name = nameof(TestBase.TestFloat64)
            });
            module.Codes.Add(new FunctionBody
            {
                Code = new Instruction[]
                {
                    new GetGlobal(0),
                    new End(),
                },
            });
            module.Codes.Add(new FunctionBody
            {
                Code = new Instruction[]
                {
                    new GetGlobal(1),
                    new End(),
                },
            });
            module.Codes.Add(new FunctionBody
            {
                Code = new Instruction[]
                {
                    new GetGlobal(2),
                    new End(),
                },
            });
            module.Codes.Add(new FunctionBody
            {
                Code = new Instruction[]
                {
                    new GetGlobal(3),
                    new End(),
                },
            });

            var compiled = module.ToInstance<TestBase>();

            var exports = compiled.Exports;
            Assert.AreEqual(4, exports.TestInt32());
            Assert.AreEqual(5, exports.TestInt64());
            Assert.AreEqual(6, exports.TestFloat32());
            Assert.AreEqual(7, exports.TestFloat64());
        }
    }
}