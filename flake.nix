{
  description = "vrc_sabatools の検証とスクリプトを動かす開発環境";

  inputs = {
    # 入力はリビジョンで固定する。ブランチ名による参照は flake.lock が無い環境で
    # 取得結果が変動するため使用しない。dotfiles と同一のリビジョンに揃えてある。
    nixpkgs.url = "github:NixOS/nixpkgs/597283ad8aa0b331c788e97c4c262d58877074ef"; # nixos-26.05
  };

  outputs =
    { self, nixpkgs }:
    let
      systems = [
        "x86_64-linux"
        "aarch64-linux"
        "x86_64-darwin"
        "aarch64-darwin"
      ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f nixpkgs.legacyPackages.${system});
    in
    {
      devShells = forAllSystems (pkgs: {
        default = pkgs.mkShell {
          packages = [
            # .github/verify/verify.sh が使う。Roslyn は SDK に含まれる。
            pkgs.dotnet-sdk_8

            # 参照アセンブリの取得と zip の検査。
            pkgs.curl
            pkgs.unzip
            pkgs.zip
            pkgs.jq

            # シェルスクリプトの静的解析と整形。
            pkgs.shellcheck
            pkgs.shfmt

            pkgs.git
          ];

          # NuGet のテレメトリと初回メッセージを止める。CI のログと手元の
          # 出力を一致させるため。
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          DOTNET_NOLOGO = "1";

          shellHook = ''
            echo "vrc_sabatools dev shell"
            echo "  ./.github/verify/verify.sh          全検証 (podman または docker が別途必要)"
            echo "  .github/scripts/run.sh <script.py>  Python を固定コンテナで実行"
          '';
        };
      });

      # Python は固定した digest のコンテナで動かす方針のため、この flake には
      # インタプリタを入れていない。.github/scripts/run.sh を参照すること。
    };
}
