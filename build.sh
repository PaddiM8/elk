#!/bin/sh

git submodule update --init --recursive
dotnet publish src/Elk.csproj -r linux-x64 -c Release --no-self-contained
mkdir -p build
cp -r src/bin/Release/*/linux-x64/publish/Elk build/elk
