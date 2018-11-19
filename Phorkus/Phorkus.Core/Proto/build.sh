#!/usr/bin/env bash
protoc --csharp_out . account.proto
protoc --csharp_out . asset.proto
protoc --csharp_out . balance.proto
protoc --csharp_out . block.proto
protoc --csharp_out . consensus.proto
protoc --csharp_out . contract.proto
protoc --csharp_out . default.proto
protoc --csharp_out . message.proto
protoc --csharp_out . multisig.proto
protoc --csharp_out . network.proto
protoc --csharp_out . node.proto
protoc --csharp_out . storage.proto
protoc --csharp_out . transaction.proto
protoc --csharp_out . validator.proto