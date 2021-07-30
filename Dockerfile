FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim as build-env
WORKDIR /lachain
COPY src/Lachain.Consensus/Lachain.Consensus.csproj ./Lachain.Consensus/
COPY src/Lachain.Console/Lachain.Console.csproj ./Lachain.Console/
COPY src/Lachain.Core/Lachain.Core.csproj ./Lachain.Core/
COPY src/Lachain.Crypto/Lachain.Crypto.csproj ./Lachain.Crypto/
COPY src/Lachain.Logger/Lachain.Logger.csproj ./Lachain.Logger/
COPY src/Lachain.Networking/Lachain.Networking.csproj ./Lachain.Networking/
COPY src/Lachain.Proto/Lachain.Proto.csproj ./Lachain.Proto/
COPY src/Lachain.Storage/Lachain.Storage.csproj ./Lachain.Storage/
COPY src/Lachain.Utility/Lachain.Utility.csproj ./Lachain.Utility/
COPY wasm/WebAssembly/WebAssembly.csproj /wasm/WebAssembly/
WORKDIR /lachain/Lachain.Console
RUN dotnet restore
WORKDIR /lachain
COPY src/ /lachain/
COPY wasm/ /wasm/
WORKDIR /lachain/Lachain.Console
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:5.0-buster-slim
RUN apt update && apt install -y libc6-dev
WORKDIR /lachain
COPY --from=build-env /lachain/Lachain.Console/out .
RUN rm -rf /lachain/wallet.json /lachain/config.json
ARG CONFIG=src/Lachain.Console/config0.json
ARG WALLET=src/Lachain.Console/wallet0.json
COPY $CONFIG /lachain/config.json
COPY $WALLET /lachain/wallet.json
ENTRYPOINT ["dotnet", "/lachain/Lachain.Console.dll"]
