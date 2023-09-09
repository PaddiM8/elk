#!/bin/sh

dotnet publish cli/Elk.Cli.csproj -r linux-x64 -c Release --no-self-contained
mkdir -p build
cp -r cli/bin/Release/*/linux-x64/publish/Elk build/elk
