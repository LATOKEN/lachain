// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

contract test {
   function testMsgSender() public view returns (address) {
       return msg.sender;
   }
}

