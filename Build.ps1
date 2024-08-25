param($arch)

$systemArch = [System.Environment]::GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
if ([string]::IsNullOrWhiteSpace($arch)) {
  if ($systemArch -eq "ARM64") {
    $arch = "arm64"
  } else {
    $arch = "x64"
  }
}

$target = "win-$arch"
dotnet publish cli/Elk.Cli.csproj -c Release -r $target


if (-not (Test-Path "build")) {
  mkdir build
}

if (Test-Path "build/$target") {
  rd -r build/$target
}

mkdir build/$target
cp cli/bin/Release/*/$target/publish/Elk.Cli.exe build/$target/elk.exe
cp -r cli/bin/Release/*/$target/publish/Resources/* build/$target
