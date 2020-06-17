#!/bin/bash

BUILD_ARGS="--runtime $RUNTIME -p:Configuration=Release -p:GithubDistrib=true"
FRAMEWORK="netcoreapp3.1"
DIST="/source/dist"
RESOURCES="/source/Build/${RUNTIME}"
RESOURCES_COMMON="/source/Build/common"
PROJECT_FILE="/source/Lachain.Console/Lachain.Console.csproj"
VERSION_FILE="/source/Lachain.Console/Version.csproj"
LICENSE="$(cat $PROJECT_FILE | sed -n 's/.*<PackageLicenseExpression>\(.*\)<\/PackageLicenseExpression>.*/\1/p')"
DESCRIPTION="$(cat $PROJECT_FILE | sed -n 's/.*<Description>\(.*\)<\/Description>.*/\1/p')"
COMPANY="$(cat $PROJECT_FILE | sed -n 's/.*<Company>\(.*\)<\/Company>.*/\1/p')"
TITLE="$(cat $PROJECT_FILE | sed -n 's/.*<Title>\(.*\)<\/Title>.*/\1/p')"
if [ -f "$VERSION_FILE" ]; then
    VERSION="$(cat $VERSION_FILE | sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p')"
else
    VERSION="0.0.0"
fi
PUBLISH_FOLDER="/source/Lachain.Console/bin/Release/$FRAMEWORK/$RUNTIME/publish"
EXECUTABLE="$(cat $PROJECT_FILE | sed -n 's/.*<TargetName>\(.*\)<\/TargetName>.*/\1/p')"

mkdir -p "$DIST"

replaceProjectVariables () {
  sed -i "s/{VersionPrefix}/$VERSION/g" "$1"
  sed -i "s/{VERSION}/$VERSION/g" "$1"
  sed -i "s/{ApplicationName}/$TITLE/g" "$1"
  sed -i "s/{TITLE}/$TITLE/g" "$1"
  sed -i "s/{DESCRIPTION}/$DESCRIPTION/g" "$1"
  sed -i "s/{EXECUTABLE}/$EXECUTABLE/g" "$1"
  sed -i "s/{COMPANY}/$COMPANY/g" "$1"
}

dotnet_publish () {
    dotnet publish --no-restore --framework $FRAMEWORK $BUILD_ARGS $ADDITIONAL_PUBLISH_ARGS Lachain.Console/Lachain.Console.csproj
}

dotnet_restore () {
    dotnet restore $BUILD_ARGS Lachain.Console/Lachain.Console.csproj
}
