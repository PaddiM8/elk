#!/bin/sh

dotnet build ./src/Elk.csproj --output ./src/bin/Debug/gen
dotnet run --project ./doc-gen/Elk.DocGen.csproj -- ./src/bin/Debug/gen/Elk.xml ./docs/std/
