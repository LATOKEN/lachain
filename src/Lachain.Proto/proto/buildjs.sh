#!/usr/bin/env bash

cd ../src
mkdir proto
cd ../proto

PROTO_OUT="../src/proto"
GRPC_OUT="../src/proto/grpc"

protoc --js_out import_style=commonjs:${PROTO_OUT} block.proto
protoc --js_out import_style=commonjs:${PROTO_OUT} consensus.proto
protoc --js_out import_style=commonjs:${PROTO_OUT} contract.proto
protoc --js_out import_style=commonjs:${PROTO_OUT} default.proto
protoc --js_out import_style=commonjs:${PROTO_OUT} event.proto
protoc --js_out import_style=commonjs:${PROTO_OUT} multisig.proto
protoc --js_out import_style=commonjs:${PROTO_OUT} node.proto
protoc --js_out import_style=commonjs:${PROTO_OUT} transaction.proto
protoc --js_out import_style=commonjs:${PROTO_OUT} networking.proto
