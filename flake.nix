{
  description = "A shell language with cleaner syntax, automatic redirection, and proper datatypes";

  outputs = { self, nixpkgs }:
    let
      systems =
        [ "x86_64-linux" "aarch64-linux" "x86_64-darwin" "aarch64-darwin" ];

      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f system);

      nixpkgsFor = forAllSystems (system:
        import nixpkgs {
          inherit system;
          overlays = [ self.overlay ];
        });
    in rec {
      overlay = final: prev: {
        elk = final.buildDotnetModule rec {
          pname = "elk";
          version = "0.0.6";
          description = "A shell language with cleaner syntax, automatic redirection, and proper datatypes";

          src = self;

          # AOT is completely broken under Nix, so we have to work around it
          # Ready to Run is broken without nix-ld, so we're going with a fully JITed build here
          dotnetFlags = "-p:PublishNativeAot=False -p:PublishAot=False -p:PublishTrimmed=False -p:PublishSingleFile=true;";

          dotnet-sdk = prev.dotnetCorePackages.sdk_9_0;
          dotnet-runtime = prev.dotnetCorePackages.runtime_9_0;

 	  projectFile = "cli/Elk.Cli.csproj";

          /**
            Mandatory reading: https://github.com/NixOS/nixpkgs/blob/master/doc/languages-frameworks/dotnet.section.md#generating-and-updating-nuget-dependencies-generating-and-updating-nuget-dependencies

            How to get a new lock file (a guide by nik):
            1. Load a shell environment with dotnet_9-sdk
            2. dotnet restore --packages out
            3. uget-to-json out > deps.json
            4. Remove any of the linux-x86 lines from deps.json
               Reason being: Nix cannot use binaries it has not built itself
               therefore breaking the build
            5. Pray it builds when u run `nix run`
          **/
          nugetDeps = ./nuget.json;

          # Do not emit any binaries apart from Elk itself
          executables = [ "Elk.Cli" ];

          # Rename elk to something more sane
          postFixup = ''
            mv $out/bin/Elk.Cli $out/bin/elk
          '';
        };
      };

      packages =
        forAllSystems (system: { inherit (nixpkgsFor.${system}) elk; });

      defaultPackage = forAllSystems (system: self.packages.${system}.elk);

      apps = forAllSystems (system: {
        elk = {
          type = "app";
          program = "${self.packages.${system}.elk}/bin/elk";
        };
      });

      defaultApp = forAllSystems (system: self.apps.${system}.elk);

      devShell = forAllSystems (system:
        nixpkgs.legacyPackages.${system}.mkShell {
          inputsFrom = builtins.attrValues (packages.${system});
        });
    };
}
