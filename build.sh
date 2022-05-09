#!/bin/sh

git submodule update --init --recursive
dotnet build src/Elk.csproj -c Release
cp -r src/bin/Release/* build/
