using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoSharp.Types;

namespace NeoSharp.Core.Test.Types
{
    [TestClass]
    public class UtUInt160
    {
        [TestMethod]
        public void Is_zero_by_default()
        {
            var actual = new UInt160();
            (actual == UInt160.Zero).Should().BeTrue();
        }

        [TestMethod]
        public void Throw_when_intialized_with_buffer_non_equal_to_size()
        {
            // ReSharper disable once ObjectCreationAsStatement
            Action a = () => new UInt160(new byte[]
                { 212, 41, 126, 177, 14, 255, 59, 82, 218, 113, 248, 145, 98, 5, 128, 140, 42, 70, 32, 69, 0 });

            a.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        public void Can_be_equal_to_another_number_of_same_type()
        {
            var a = new UInt160(new byte[]
                { 212, 41, 126, 177, 14, 255, 59, 82, 218, 113, 248, 145, 98, 5, 128, 140, 42, 70, 32, 69 });
            var b = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");
            (a == b).Should().BeTrue();
        }

        [TestMethod]
        public void Can_be_not_equal_to_another_number_of_same_type()
        {
            var a = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");
            var b = UInt160.Zero;
            (a == b).Should().BeFalse();
        }

        [TestMethod]
        public void Can_be_not_equal_to_null_of_same_type()
        {
            var a = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");
            a.Equals(null).Should().BeFalse();
        }

        [TestMethod]
        public void Can_be_not_equal_to_null_of_object_type()
        {
            var a = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");          
            a.Equals((object)null).Should().BeFalse();
        }

        [TestMethod]
        public void Can_be_equal_to_another_number_of_object_type()
        {
            var a = new UInt160(new byte[]
                { 212, 41, 126, 177, 14, 255, 59, 82, 218, 113, 248, 145, 98, 5, 128, 140, 42, 70, 32, 69 });
            var b = (object)UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");
            a.Equals(b).Should().BeTrue();
        }

        [TestMethod]
        public void Can_be_not_equal_to_another_number_of_object_type()
        {
            var a = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");
            var b = (object)1;
            a.Equals(b).Should().BeFalse();
        }

        [TestMethod]
        public void Can_be_greater_than_another_number()
        {
            var a = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d5");
            var b = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");
            (a > b).Should().BeTrue();
        }

        [TestMethod]
        public void Can_be_greater_than_another_number_or_equal()
        {
            var a = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d5");
            var b = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");
            (a >= b).Should().BeTrue();
        }

        [TestMethod]
        public void Can_be_less_than_another_number()
        {
            var a = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d3");
            var b = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");
            (a < b).Should().BeTrue();
        }

        [TestMethod]
        public void Can_be_less_than_another_number_or_equal()
        {
            var a = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d3");
            var b = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");
            (a <= b).Should().BeTrue();
        }

        [TestMethod]
        public void Can_parse_and_stringify()
        {
            var actual = UInt160.Parse("0x4520462a8c80056291f871da523bff0eb17e29d4");
            actual.ToString().Should().Be("0x4520462a8c80056291f871da523bff0eb17e29d4");
        }

        [TestMethod]
        public void Can_parse_valid_string_safely()
        {
            var actual = UInt160.TryParse("0x4520462a8c80056291f871da523bff0eb17e29d4", out var a);
            actual.Should().BeTrue();
            a.Should().NotBe(UInt160.Zero);
        }

        [TestMethod]
        public void Can_parse_invalid_string_safely()
        {
            var actual =  UInt160.TryParse("0x4520462a8c80056291f871da523bff0eb17e29d4x0", out var a);
            actual.Should().BeFalse();
            a.Should().Be(UInt160.Zero);
        }

        [TestMethod]
        public void ParseAddressToUInt160()
        {
            var actual = UInt160.TryParse("LhYzeoL7r7CLggtnyFpgF9kSxKuekWMGg", out var a);
            a.Equals(null).Should().BeFalse();
        }

        [TestMethod]
        public void ParseAddressToUInt160InvalidAddress()
        {
            var actual = UInt160.TryParse("LhYzeoL7r7CLggtnyFpgF9kSxKuekWMGgx0", out var a);
            actual.Should().BeFalse();
            a.Should().Be(UInt160.Zero);
        }

        [TestMethod]
        public void ParseAddressToUInt160NotZero()
        {
            var actual = UInt160.TryParse("LhYzeoL7r7CLggtnyFpgF9kSxKuekWMGgx0", out var a);
            var b = UInt160.Zero;
            (a == b).Should().BeTrue();
        }
    }
}