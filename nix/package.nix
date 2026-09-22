{
  lib,
  buildDotnetModule,
  dotnetCorePackages,
}:

buildDotnetModule (finalAttrs: {
  pname = "forlabs-mcp";
  version = "1.0.0";

  src = lib.cleanSourceWith {
    src = ../.;
    filter =
      path: type:
      let
        base = baseNameOf path;
      in
      base != "bin" && base != "obj" && base != ".git" && !(lib.hasSuffix ".har" base);
  };

  projectFile = "src/ForlabsMcp/ForlabsMcp.csproj";
  nugetDeps = ./deps.json;

  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  dotnet-runtime = dotnetCorePackages.runtime_10_0;

  executables = [ "ForlabsMcp" ];

  meta = {
    description = "MCP server exposing the Forlabs LMS (schedule, homework, chat, grades) to AI agents";
    homepage = "https://github.com/forlabs-mcp/forlabs-mcp";
    license = lib.licenses.mit;
    mainProgram = "ForlabsMcp";
    platforms = lib.platforms.unix;
  };
})
