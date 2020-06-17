FROM mcr.microsoft.com/dotnet/core/sdk:3.1.101 AS builder

# Optimize docker cache, do not make it one layer
RUN apt-get update
RUN apt-get install -y --no-install-recommends imagemagick
RUN apt-get install -y --no-install-recommends git icnsutils

WORKDIR /source
ENV RUNTIME "osx-x64"
COPY "dist/common" "Build/common"
ENV EXPORT_VARIABLES "source Build/common/export-variables.sh"
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

SHELL ["/bin/bash", "-c"]
RUN $EXPORT_VARIABLES && dotnet_restore
COPY src/ /source/
COPY wasm/ /wasm/
RUN $EXPORT_VARIABLES && dotnet_publish

COPY "dist/${RUNTIME}" "Build/${RUNTIME}"
COPY dist/common/lachain.png lachain.png
RUN $EXPORT_VARIABLES && \
    replaceProjectVariables "$RESOURCES/Info.plist" && \
    dest=/tmp/lachain.iconset && mkdir -p $dest && \
    for size in 16x16 32x32 128x128 256x256 512x512 1024x1024; do \
        convert -background none -resize "!$size" "lachain.png" "/tmp/lachain.iconset/icon_${size}.png"; \
    done && \
    png2icns /tmp/lachain.icns /tmp/lachain.iconset/*png

RUN $EXPORT_VARIABLES && \
    dmgroot="/tmp/dmgroot/$TITLE" && \
    appfolder="$dmgroot/$TITLE.app" && \
    mkdir -p "$appfolder/Contents" && \
    cp "$RESOURCES/Info.plist" "$appfolder/Contents/" && \
    cp "$RESOURCES/entitlements.plist" "$appfolder/Contents/" && \
    mv "$PUBLISH_FOLDER" "$appfolder/Contents/MacOS" && \
    mkdir -p "$appfolder/Contents/Resources" && \
    cp /tmp/lachain.icns "$appfolder/Contents/Resources/" && \
    cp /tmp/lachain.icns "$dmgroot/.VolumeIcon.icns" && \
    ln -s /Applications "$dmgroot" && \
    cp -r "$RESOURCES/Metadata/." "$dmgroot" && \
    # We need to cd in "$dmgroot", because tar's -C option always add a root folder to the tar otherwise
    cd "$dmgroot" && shopt -s dotglob && tar -czf "$DIST/lachain-${RUNTIME}-$VERSION.tar.gz" *

ENTRYPOINT [ "/bin/bash", "-c", "$EXPORT_VARIABLES && cp $DIST/* /opt/dist/" ]
