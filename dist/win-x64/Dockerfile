FROM mcr.microsoft.com/dotnet/core/sdk:3.1.101 AS builder

# Optimize docker cache, do not make it one layer
RUN apt-get update
RUN apt-get install -y --no-install-recommends imagemagick
RUN apt-get install -y --no-install-recommends nsis unzip wine

# Need to setup with rcedit because https://github.com/dotnet/sdk/issues/3943
RUN wget -qO "/tmp/rcedit.exe" https://github.com/electron/rcedit/releases/download/v1.1.1/rcedit-x64.exe

WORKDIR /source
ENV RUNTIME "win-x64"
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
COPY dist/common/lachain.png lachain.png

RUN $EXPORT_VARIABLES && \
    mkdir -p "/tmp/lachain.ico.tmp" && \
    for size in 256x256 48x48 32x32 16x16; do \
        convert -background none -resize "!$size" "lachain.png" "PNG32:/tmp/lachain.ico.tmp/lachain-$size.png"; \
    done && \
    convert /tmp/lachain.ico.tmp/*.png /tmp/lachain.ico && \
    ADDITIONAL_PUBLISH_ARGS="-p:ApplicationIcon=/tmp/lachain.ico" && \
    dotnet_publish

COPY "dist/${RUNTIME}" "Build/${RUNTIME}"
RUN $EXPORT_VARIABLES && \
    wine /tmp/rcedit.exe "$PUBLISH_FOLDER/$EXECUTABLE.exe" \
    --set-icon "/tmp/lachain.ico" \
    --set-version-string "LegalCopyright" "$LICENSE" \
    --set-version-string "CompanyName" "$COMPANY" \
    --set-version-string "FileDescription" "$DESCRIPTION" \
    --set-version-string "ProductName" "$TITLE" \
    --set-file-version "$VERSION" \
    --set-product-version "$VERSION"
RUN $EXPORT_VARIABLES && \
    makensis \
    "-DICON=/tmp/lachain.ico" \
    "-DICONNAME=lachain.ico" \
    "-DPRODUCT_VERSION=$VERSION" \
    "-DPRODUCT_NAME=$TITLE" \
    "-DPRODUCT_PUBLISHER=$COMPANY" \
    "-DPRODUCT_DESCRIPTION=$DESCRIPTION" \
    "-DDIST=$DIST" \
    "-DEXECUTABLE=$EXECUTABLE" \
    "-DPUBLISH_FOLDER=$PUBLISH_FOLDER" \
    "-DRESOURCES=${RESOURCES}" \
    "$RESOURCES/vault.nsis"

ENTRYPOINT [ "/bin/bash", "-c", "$EXPORT_VARIABLES && cp $DIST/* /opt/dist/" ]
