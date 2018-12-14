#!/usr/bin/env bash

protoc --csharp_out ../ account.proto
protoc --csharp_out ../ asset.proto
protoc --csharp_out ../ balance.proto
protoc --csharp_out ../ block.proto
protoc --csharp_out ../ consensus.proto
protoc --csharp_out ../ contract.proto
protoc --csharp_out ../ default.proto
protoc --csharp_out ../ global.proto
protoc --csharp_out ../ multisig.proto
protoc --csharp_out ../ node.proto
protoc --csharp_out ../ transaction.proto
protoc --csharp_out ../ networking.proto

PLUGIN=~/.nuget/packages/grpc.tools/1.17.0/tools/linux_x64/grpc_csharp_plugin
#protoc --csharp_out ../Grpc -I . -I ./grpc blockchain_service.proto --grpc_out ../Grpc --plugin=protoc-gen-grpc="${PLUGIN}"
#protoc --csharp_out ../Grpc -I . -I ./grpc consensus_service.proto --grpc_out ../Grpc --plugin=protoc-gen-grpc="${PLUGIN}"
#protoc --csharp_out ../Grpc -I . -I ./grpc threshold_service.proto --grpc_out ../Grpc --plugin=protoc-gen-grpc="${PLUGIN}"
