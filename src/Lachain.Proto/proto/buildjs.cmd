setlocal
cd /d %~dp0
cd ../src
mkdir proto
cd ../proto

set OUT_DIR=../src/proto
set JS_OUT=--js_out=import_style=commonjs:%OUT_DIR%

protoc %JS_OUT% block.proto
protoc %JS_OUT% consensus.proto
protoc %JS_OUT% contract.proto
protoc %JS_OUT% default.proto
protoc %JS_OUT% event.proto
protoc %JS_OUT% multisig.proto
protoc %JS_OUT% node.proto
protoc %JS_OUT% transaction.proto
protoc %JS_OUT% networking.proto
endlocal