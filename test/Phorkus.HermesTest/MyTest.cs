using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Phorkus.HermesTest
{
    [TestClass]
    public class MyTest1
    {

        class A
        {
            private int _a;
            public A f(int a)
            {
                _a = a;
                ++_a;
                Console.WriteLine($"a :{_a}");
                return new A();
            }
        }
        
        [TestMethod]
        public void Test()
        {
            var a = new A();
            a.f(2).f(3).f(4);
        }
        
    }
}