FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build-env
WORKDIR /phorkus
COPY src/Phorkus.Consensus/Phorkus.Consensus.csproj ./Phorkus.Consensus/
COPY src/Phorkus.Console/Phorkus.Console.csproj ./Phorkus.Console/
COPY src/Phorkus.Core/Phorkus.Core.csproj ./Phorkus.Core/
COPY src/Phorkus.Crypto/Phorkus.Crypto.csproj ./Phorkus.Crypto/
COPY src/Phorkus.Logger/Phorkus.Logger.csproj ./Phorkus.Logger/
COPY src/Phorkus.Networking/Phorkus.Networking.csproj ./Phorkus.Networking/
COPY src/Phorkus.Proto/Phorkus.Proto.csproj ./Phorkus.Proto/
COPY src/Phorkus.Storage/Phorkus.Storage.csproj ./Phorkus.Storage/
COPY src/Phorkus.Utility/Phorkus.Utility.csproj ./Phorkus.Utility/
COPY src/Phorkus.WebAssembly/Phorkus.WebAssembly.csproj ./Phorkus.WebAssembly/
WORKDIR /phorkus/Phorkus.Console
RUN dotnet restore
WORKDIR /phorkus
COPY src/ ./
WORKDIR /phorkus/Phorkus.Console
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.1
RUN apt update && apt install -y libc6-dev libsnappy-dev
WORKDIR /phorkus
COPY --from=build-env /phorkus/Phorkus.Console/out .
ARG CONFIG
COPY $CONFIG ./config.json
ENTRYPOINT ["dotnet", "Phorkus.Console.dll"]
