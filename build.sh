#!/bin/sh

ARCHITECTURE=x64
if [ "$(uname -m)" == "arm64" ]; then
    ARCHITECTURE=arm64
elif [ -z "$1" ]; then
    ARCHITECTURE=$1
fi

TARGET=linux-$ARCHITECTURE
if [ "$(uname)" == "Darwin" ]; then
    TARGET=osx-$ARCHITECTURE
else
    TARGET=$2-$ARCHITECTURE
fi

dotnet publish cli/Elk.Cli.csproj -r $TARGET -c Release
rm -rf build

if [ "$(uname)" == "Darwin" ]; then
    mkdir -p build/$TARGET
    cp cli/bin/Release/*/$TARGET/publish/Elk.Cli build/$TARGET/elk
    cp -r cli/bin/Release/*/$TARGET/publish/Resources/* build/$TARGET
    cp cli/bin/Release/*/$TARGET/publish/*.dylib build/$TARGET
else
    mkdir -p build/$TARGET/usr/bin
    mkdir -p build/$TARGET/usr/lib/elk
    mkdir -p build/$TARGET/usr/share/elk
    cp cli/bin/Release/*/$TARGET/publish/Elk.Cli build/$TARGET/usr/bin/elk
    cp -r cli/bin/Release/*/$TARGET/publish/Resources/* build/$TARGET/usr/share/elk
    cp cli/bin/Release/*/$TARGET/publish/*.so build/$TARGET/usr/lib/elk
fi
