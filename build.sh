#!/bin/sh

dotnet publish cli/Elk.Cli.csproj -r linux-x64 -c Release
mkdir -p build
cp cli/bin/Release/*/linux-x64/publish/Elk.Cli build/elk
cp cli/bin/Release/*/linux-x64/publish/*.so build/
