{
  buildDotnetModule,
  fetchFromGitHub,
  callPackage,
  ...
}:
buildDotnetModule {
  name = "osu-sentakki";
  src = ./.;
  projectFile = "osu.Game.Rulesets.Sentakki/osu.Game.Rulesets.Sentakki.csproj";
  nugetDeps = ./deps.nix;
  projectReferences = [
    (buildDotnetModule
      rec {
        pname = "simai-sharp";
        version = "0.1.17";
        src = fetchFromGitHub {
          owner = "reflektone-games";
          repo = "SimaiSharp";
          tag = "v${version}";
          hash = "sha256-/EjlXtgm5skMbDZ2mN2fQacFz2R9Q6HLR6u4SZ2kXCY=";
        };
        projectFile = "SimaiSharp/SimaiSharp.csproj";
        packNupkg = true;
        preInstall = ''
          pushd ./SimaiSharp/bin/Release/netstandard2.1/
          ln -s ./*/SimaiSharp.dll
          ln -s ./*/SimaiSharp.pdb
          popd
        '';
      })
  ];
  installPhase = ''
    mkdir -p $out
    cp ./osu.Game.Rulesets.Sentakki/bin/Release/net8.0/*/osu.Game.Rulesets.Sentakki.dll $out/
  '';
}
