{
  description = "MCP server for the Forlabs LMS (schedule, homework, teacher chat, grades)";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs { inherit system; };
        forlabs-mcp = pkgs.callPackage ./nix/package.nix { };
      in
      {
        packages = {
          default = forlabs-mcp;
          forlabs-mcp = forlabs-mcp;
        };

        apps.default = {
          type = "app";
          program = "${forlabs-mcp}/bin/ForlabsMcp";
        };

        devShells.default = pkgs.mkShell {
          packages = [ pkgs.dotnetCorePackages.sdk_10_0 ];
        };
      }
    )
    // {
      # System-independent: lets other flakes (e.g. nix-openclaw) do
      #   overlays = [ inputs.forlabs-mcp.overlays.default ];
      # and then reference `pkgs.forlabs-mcp`.
      overlays.default = final: prev: {
        forlabs-mcp = final.callPackage ./nix/package.nix { };
      };
    };
}
