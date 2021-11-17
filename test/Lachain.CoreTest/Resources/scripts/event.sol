// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

contract Event {
    event Test(uint256 i, address a, bool b);

    function test() public {
        emit Test(0, address(0xa76f0e65D6923484bAaA8c48b81Be15952128CfA), false);
    }
}
