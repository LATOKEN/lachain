setlocal
cd /d %~dp0
set PLUGIN="%UserProfile%\.nuget\packages\Grpc.Tools\1.17.0-pre1\tools\windows_x64\grpc_csharp_plugin.exe"

cd ..
mkdir Grpc
cd proto

protoc --csharp_out ../Grpc -I . -I ./grpc account_service.proto --grpc_out ../Grpc --plugin=protoc-gen-grpc=%PLUGIN%
protoc --csharp_out ../Grpc -I . -I ./grpc blockchain_service.proto --grpc_out ../Grpc --plugin=protoc-gen-grpc=%PLUGIN%

endlocal