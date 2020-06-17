#!/bin/bash
set -e
if [ ! -f "appimagetool-x86_64.AppImage" ]; then
  wget "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage"
  chmod +x ./appimagetool-x86_64.AppImage
fi
dotnet publish -c Release -r linux-x64 ../../src/Lachain.Console/Lachain.Console.csproj
mkdir -p ./AppDir/usr/bin
cp -r ../../src/Lachain.Console/bin/Release/netcoreapp3.1/linux-x64/publish/* ./AppDir/usr/bin
ARCH=x86_64 ./appimagetool-x86_64.AppImage AppDir
