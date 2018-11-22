#!/usr/bin/env bash

mkdir ../src/Phorkus.Core/Proto/

protoc --csharp_out ../src/Phorkus.Core/Proto account.proto
protoc --csharp_out ../src/Phorkus.Core/Proto asset.proto
protoc --csharp_out ../src/Phorkus.Core/Proto balance.proto
protoc --csharp_out ../src/Phorkus.Core/Proto block.proto
protoc --csharp_out ../src/Phorkus.Core/Proto consensus.proto
protoc --csharp_out ../src/Phorkus.Core/Proto contract.proto
protoc --csharp_out ../src/Phorkus.Core/Proto default.proto
protoc --csharp_out ../src/Phorkus.Core/Proto global.proto
protoc --csharp_out ../src/Phorkus.Core/Proto message.proto
protoc --csharp_out ../src/Phorkus.Core/Proto multisig.proto
protoc --csharp_out ../src/Phorkus.Core/Proto node.proto
protoc --csharp_out ../src/Phorkus.Core/Proto storage.proto
protoc --csharp_out ../src/Phorkus.Core/Proto transaction.proto
protoc --csharp_out ../src/Phorkus.Core/Proto validator.proto