setlocal
cd /d %~dp0
set PLUGIN="%UserProfile%\.nuget\packages\Grpc.Tools\1.17.1\tools\windows_x64\grpc_csharp_plugin.exe"

protoc --csharp_out .. -I . -I ../../Phorkus.Proto/proto account_service.proto --grpc_out .. --plugin=protoc-gen-grpc=%PLUGIN%
protoc --csharp_out .. -I . -I ../../Phorkus.Proto/proto blockchain_service.proto --grpc_out .. --plugin=protoc-gen-grpc=%PLUGIN%

endlocalrequest signature is invalid