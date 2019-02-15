FROM microsoft/dotnet:sdk as build-env
WORKDIR /phorkus
COPY src/Phorkus.Console/Phorkus.Console.csproj ./Phorkus.Console/
COPY src/Phorkus.Core/Phorkus.Core.csproj ./Phorkus.Core/
COPY src/Phorkus.Crypto/Phorkus.Crypto.csproj ./Phorkus.Crypto/
COPY src/Phorkus.Storage/Phorkus.Storage.csproj ./Phorkus.Storage/
COPY src/Phorkus.Logger/Phorkus.Logger.csproj ./Phorkus.Logger/
COPY src/Phorkus.Networking/Phorkus.Networking.csproj ./Phorkus.Networking/
COPY src/Phorkus.Proto/Phorkus.Proto.csproj ./Phorkus.Proto/
COPY src/Phorkus.Utility/Phorkus.Utility.csproj ./Phorkus.Utility/
COPY src/Phorkus.WebAssembly/Phorkus.WebAssembly.csproj ./Phorkus.WebAssembly/
WORKDIR /phorkus/Phorkus.Console
RUN dotnet restore
WORKDIR /phorkus
COPY src/ ./
WORKDIR /phorkus/Phorkus.Console
RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:aspnetcore-runtime
RUN apt update && apt install -y libc6-dev libsnappy-dev
WORKDIR /phorkus
COPY --from=build-env /phorkus/Phorkus.Console/out .
ARG CONFIG=src/Phorkus.Console/config.json
COPY ${CONFIG} ./config.json
ENTRYPOINT ["dotnet", "Phorkus.Console.dll"]
