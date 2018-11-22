FROM microsoft/dotnet:sdk
RUN apt update && apt install -y apt-transport-https libsnappy-dev
RUN ln -s /lib/x86_64-linux-gnu/libdl.so.2 /lib/x86_64-linux-gnu/libdl.so
WORKDIR /phorkus

COPY Phorkus.RocksDB/*.csproj ./Phorkus.RocksDB/
COPY Phorkus.Logger/*.csproj ./Phorkus.Logger/
COPY Phorkus.Core/*.csproj ./Phorkus.Core/
COPY Phorkus.Console/*.csproj ./Phorkus.Console/

WORKDIR /phorkus/Phorkus.Console
RUN dotnet restore

WORKDIR /phorkus
COPY . ./

WORKDIR /phorkus/Phorkus.Console
RUN dotnet build -c Release -o build
ENTRYPOINT ["dotnet", "build/Phorkus.Console.dll"]
