#!/usr/bin/env bash

if echo "$OSTYPE" | grep 'darwin'; then
	PLUGIN=~/.nuget/packages/grpc.tools/1.17.0/tools/macosx_x64/grpc_csharp_plugin
else
	PLUGIN=~/.nuget/packages/grpc.tools/1.17.0/tools/linux_x64/grpc_csharp_plugin
fi

mkdir -p Grpc

protoc --csharp_out ../Grpc -I . -I ./grpc account_service.proto --grpc_out ../Grpc --plugin=protoc-gen-grpc="${PLUGIN}"
protoc --csharp_out ../Grpc -I . -I ./grpc blockchain_service.proto --grpc_out ../Grpc --plugin=protoc-gen-grpc="${PLUGIN}"
