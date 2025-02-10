{
  buildDotnetModule,
  fetchFromGitHub,
  callPackage,
  ...
}: let
  simai-sharp =
    buildDotnetModule
    rec {
      name = "simai-sharp";
      src = fetchFromGitHub {
        owner = "M1N0RM1N3R";
        repo = "SimaiSharp";
        rev = "e41f5f3f6cb3edadb2a19829c79990b17e40df7a";
        hash = "sha256-OoX5OXxpHzz6RXKM0SCVC/78/fsLwYiGa8eSjR3+txM=";
      };
      projectFile = "SimaiSharp/SimaiSharp.csproj";
      packNupkg = true;
      preInstall = ''
        pushd ./SimaiSharp/bin/Release/netstandard2.1/
        ln -s ./*/SimaiSharp.dll
        ln -s ./*/SimaiSharp.pdb
        popd
      '';
    };
in
  buildDotnetModule {
    name = "osu-sentakki";
    src = ./.;
    projectFile = "osu.Game.Rulesets.Sentakki/osu.Game.Rulesets.Sentakki.csproj";
    nugetDeps = ./deps.json;
    projectReferences = [
      simai-sharp
    ];
    installPhase = ''
      mkdir -p $out
      cp ./osu.Game.Rulesets.Sentakki/bin/Release/net8.0/*/osu.Game.Rulesets.Sentakki.dll $out/
      ln -s ${simai-sharp}/lib/SimaiSharp.dll $out/
    '';
  }
