#!/bin/sh

ARCHITECTURE=x64
if [ "$(uname -m)" = "aarch64" ] || [ "$(uname -m)" = "arm64" ]; then
    ARCHITECTURE=arm64
elif [ -n "$1" ]; then
    ARCHITECTURE=$1
fi

TARGET=linux-$ARCHITECTURE
if [ "$(uname)" = "Darwin" ]; then
    TARGET=osx-$ARCHITECTURE
elif [ -n "$2" ]; then
    TARGET=$2-$ARCHITECTURE
fi

dotnet publish cli/Elk.Cli.csproj -r $TARGET -c Release
rm -rf build/$TARGET

if [ "$(uname)" = "Darwin" ]; then
    mkdir -p build/$TARGET
    cp cli/bin/Release/*/$TARGET/publish/Elk.Cli build/$TARGET/elk
    cp -r cli/bin/Release/*/$TARGET/publish/Resources/* build/$TARGET
    cp cli/bin/Release/*/$TARGET/publish/*.dylib build/$TARGET

    cd build/$TARGET
    tar -czf macOS-$ARCHITECTURE.tar.xz *
else
    mkdir -p build/$TARGET/usr/bin
    mkdir -p build/$TARGET/usr/lib/elk
    mkdir -p build/$TARGET/usr/share/elk
    cp cli/bin/Release/*/$TARGET/publish/Elk.Cli build/$TARGET/usr/bin/elk
    cp -r cli/bin/Release/*/$TARGET/publish/Resources/* build/$TARGET/usr/share/elk
    cp cli/bin/Release/*/$TARGET/publish/*.so build/$TARGET/usr/lib/elk

    cd build/$TARGET
    tar -czf $TARGET.tar.xz usr
    rm -rf usr
fi
