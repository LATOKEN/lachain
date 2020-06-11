#!/bin/bash
set -e
dotnet publish -c Release -r linux-x64 ../../src/Lachain.Console/Lachain.Console.csproj
cp -r ../../src/Lachain.Console/bin/Release/netcoreapp3.1/linux-x64/publish/* ./AppDir/usr/bin
ARCH=x86_64 ./appimagetool-x86_64.AppImage AppDir
