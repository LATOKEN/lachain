[![Build Status](https://travis-ci.com/LAToken/lachain.svg?branch=dev)](https://travis-ci.com/LAToken/lachain)


### Build the project
```
> git clone https://github.com/LATOKEN/lachain
> cd lachain
> git submodule update --init --recursive
> dotnet build -c Debug
> dotnet publish -p:Configuration=Debug --force -p:PublishReadyToRun=true -p:IncludeAllContentForSelfExtract=true --self-contained true -p:PublishSingleFile=true --runtime linux-x64 src/Lachain.Console/Lachain.Console.csproj
```
After that place resulting `Lachain.Console` file in the same folder with appropriate config.json and start it
