[![Build Status](https://travis-ci.com/LAToken/lachain.svg?branch=dev)](https://travis-ci.com/LAToken/lachain)

## LACHAIN
LACHAIN is a blockchain with Proof-of-Stake with HoneyBadgerBFT consensus and smart contracts in WASM eVM . 

Proof-of-Stake in this case is based on the [VRF](https://en.wikipedia.org/wiki/Verifiable_random_function) lottery where chances to win are proportional to the validator's stake. 

Current validator set creates blocks with [HoneyBadgerBFT consensus](https://eprint.iacr.org/2016/199.pdf) protocol.

### Security Audit
An [official security audit](audit/Lachain-consensus-audit-report.pdf) has been completed on LACHAIN HoneyBadgerBFT consensus implementation by [Hashex](https://hashex.org). 



### Build the project

#### Linux
```
> git clone https://github.com/LATOKEN/lachain
> cd lachain
> git submodule update --init --recursive
> dotnet build -c Release
> dotnet publish -p:Configuration=Release --force -p:PublishReadyToRun=true -p:IncludeAllContentForSelfExtract=true --self-contained true -p:PublishSingleFile=true -p:TrimUnusedDependencies=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true --runtime linux-x64 src/Lachain.Console/Lachain.Console.csproj
```
As result `Lachain.Console` file will be placed in `/src/Lachain.Console/bin/Release/net5.0/linux-x64/publish/`
After that place `Lachain.Console` file in the same folder with appropriate config.json and start it

#### Windows 
```
> git clone https://github.com/LATOKEN/lachain
> cd lachain
> git submodule update --init --recursive
> dotnet build -c Release
> dotnet publish -p:Configuration=Release --force -p:PublishReadyToRun=true -p:IncludeAllContentForSelfExtract=true --self-contained true -p:PublishSingleFile=true -p:TrimUnusedDependencies=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true --runtime win-x64 src/Lachain.Console/Lachain.Console.csproj
```
As result `Lachain.Console` file will be placed in `/src/Lachain.Console/bin/Release/net5.0/win-x64/publish/`
After that place `Lachain.Console` file in the same folder with appropriate config.json and start it

#### MacOS 
```
> git clone https://github.com/LATOKEN/lachain
> cd lachain
> git submodule update --init --recursive
> dotnet build -c Release
> dotnet publish -p:Configuration=Release --force -p:PublishReadyToRun=true -p:IncludeAllContentForSelfExtract=true --self-contained true -p:PublishSingleFile=true -p:TrimUnusedDependencies=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true --runtime osx-x64 src/Lachain.Console/Lachain.Console.csproj
```
As result `Lachain.Console` file will be placed in `/src/Lachain.Console/bin/Release/net5.0/osx-x64/publish/`
After that place `Lachain.Console` file in the same folder with appropriate config.json and start it

### Config files
Use `config_mainnet.json` template to connect to the LACHAIN mainnet or `config_testnet.json` to connect to the testnet.
Rename required template to the `config.json` and change `rpc/apiKey` to the real hex dump of the ECDSA public key and `vault/password` to the real password you want to use to encode your wallet,  after that place resulting `config.json` to the folder where `Lachain.Console` is placed.

### Docker 
1. Set config.json in the repo root (for example,  rename config_testnet.json or config_mainnet.json to the config.json)
2. `docker-compose build`
3. `docker-compose up`
