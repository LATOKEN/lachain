using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]