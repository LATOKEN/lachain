// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

contract B {
    uint256 public value;
    uint256 public a;

    function get() public view returns (uint256) {
        return value;
    }

    function getA() public view returns (uint256) {
        return a;
    }

    function receiveValue() payable public {
        value = msg.value;
    }

    function receiveValue(uint256 _a) payable public {
        value = msg.value;
        a = _a;
    }
}

contract A {
    B b1;
    B b2;

    uint256 public value;

    function init(address _b1, address _b2) public {
        b1 = B(_b1);
        b2 = B(_b2);
    }

    function get() public view returns (uint256) {
        return value;
    }

    function getA() public view returns (uint256) {
        return b1.getA();
    }

    function receiveValue(uint256 _a) payable public {
        value = msg.value;

        uint256 value1 = value / 3;
        uint256 value2 = value - value1;

        b1.receiveValue{value:value1}(_a);
        b2.receiveValue{value:value2}();
    }
}
