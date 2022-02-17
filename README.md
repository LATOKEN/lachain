[![Build Status](https://travis-ci.com/LAToken/lachain.svg?branch=dev)](https://travis-ci.com/LAToken/lachain)


### Build the project
```
> git clone https://github.com/LATOKEN/lachain
> cd lachain
> git submodule update --init --recursive
> dotnet build -c Release
> dotnet publish -p:Configuration=Release --force -p:PublishReadyToRun=true -p:IncludeAllContentForSelfExtract=true --self-contained true -p:PublishSingleFile=true --runtime linux-x64 src/Lachain.Console/Lachain.Console.csproj
```
As result `Lachain.Console` file will be placed in `/src/Lachain.Console/bin/Release/net5.0/linux-x64/publish/`
After that place `Lachain.Console` file in the same folder with appropriate config.json and start it
