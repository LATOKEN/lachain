#!/usr/bin/env bash

if echo "$OSTYPE" | grep 'darwin'; then
	PLUGIN=~/.nuget/packages/grpc.tools/1.17.0/tools/macosx_x64/grpc_csharp_plugin
else
	PLUGIN=~/.nuget/packages/grpc.tools/1.17.0/tools/linux_x64/grpc_csharp_plugin
fi

protoc --csharp_out .. -I . -I ../../Phorkus.Proto/proto account_service.proto --grpc_out .. --plugin=protoc-gen-grpc="${PLUGIN}"
protoc --csharp_out .. -I . -I ../../Phorkus.Proto/proto blockchain_service.proto --grpc_out .. --plugin=protoc-gen-grpc="${PLUGIN}"
