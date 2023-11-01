#!/bin/sh

TARGET=linux-x64
dotnet publish cli/Elk.Cli.csproj -r $TARGET -c Release
rm -rf build
mkdir -p build/$TARGET/usr/bin
mkdir -p build/$TARGET/usr/lib/elk
mkdir -p build/$TARGET/usr/share/elk
cp cli/bin/Release/*/linux-x64/publish/Elk.Cli build/$TARGET/usr/bin/elk
cp cli/bin/Release/*/linux-x64/publish/*.so build/$TARGET/usr/lib/elk
cp -r cli/bin/Release/*/linux-x64/publish/Resources/* build/$TARGET/usr/share/elk
