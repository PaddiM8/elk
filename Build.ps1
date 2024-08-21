dotnet publish cli/Elk.Cli.csproj -c Release

if (Test-Path "build") {
    rd -r build
}

mkdir build
cp cli/bin/Release/*/*/publish/Elk.Cli.exe build/elk.exe
cp -r cli/bin/Release/*/*/publish/Resources/* build
