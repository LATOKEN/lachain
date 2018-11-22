#!/usr/bin/env bash

mkdir ../src/Phorkus.Proto/

protoc --csharp_out ../src/Phorkus.Proto account.proto
protoc --csharp_out ../src/Phorkus.Proto asset.proto
protoc --csharp_out ../src/Phorkus.Proto balance.proto
protoc --csharp_out ../src/Phorkus.Proto block.proto
protoc --csharp_out ../src/Phorkus.Proto consensus.proto
protoc --csharp_out ../src/Phorkus.Proto contract.proto
protoc --csharp_out ../src/Phorkus.Proto default.proto
protoc --csharp_out ../src/Phorkus.Proto global.proto
protoc --csharp_out ../src/Phorkus.Proto message.proto
protoc --csharp_out ../src/Phorkus.Proto multisig.proto
protoc --csharp_out ../src/Phorkus.Proto node.proto
protoc --csharp_out ../src/Phorkus.Proto storage.proto
protoc --csharp_out ../src/Phorkus.Proto transaction.proto
protoc --csharp_out ../src/Phorkus.Proto validator.proto