// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

contract D {
    uint256 public a;

    function setA(uint256 _a) public {
        a = _a;
    }

    function getA() public view returns (uint256) {
        return a;
    }
}

contract C {
    D b;

    uint256 public value;

    function init(address _b) public {
        b = D(_b);
    }

    function getADirect(address _b) public view returns (uint256) {
        D temp;
        temp = D(_b);
        return temp.getA();
    }

    function getA() public view returns (uint256) {
        return b.getA();
    }
}
