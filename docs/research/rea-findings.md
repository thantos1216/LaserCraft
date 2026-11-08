# REA integration and verification

Verified 2026-11-07. Checkout: D:\Development\REA\rea. Package version 6.8.0; Git revision cbd4bd4a6, 1236 commits after tag rea-agents-6.8.0. The GitHub latest release API and npm registry both report 6.8.0. This is a source checkout newer than the tagged release, not the exact release build.

Sources: https://github.com/morluto/rea, https://api.github.com/repos/morluto/rea/releases/latest, checkout docs/installation.md and native/windows/README.md.

## Completed verification

- Locked dependencies installed with npm ci, local to the REA checkout.
- Source build failed on host Node 26.13.0 because the verifier requests TypeScript transform mode. Existing bundled Node 24.22.1 exactly matches .nvmrc; using it resolves that error without machine-wide changes.
- Source checkout omitted Windows native build artifacts. Following upstream recovery instructions, restored only native/windows/build/manifest.json and rea-windows-x64.node from the exact rea-agents@6.8.0 npm tarball. Source was not patched.
- npm run build:cached succeeds with Node 24.22.1 and the restored addon.
- CLI --version returns 6.8.0.
- doctor detects Ghidra 12.1.4 at D:\AI\ghidra_12.1.4_PUBLIC and JDK 25.0.5.8, valid Windows native controls and local NTFS runtime.
- Native PE inspection of our own LaserCraft.Desktop.exe succeeds through Ghidra; inventory reports 8 segments, 588 procedures and 529 strings. This tests the toolchain, not application feature parity.
- Managed PE static inspection of our own LaserCraft.Domain.dll succeeds. A generic native inspection rejects managed PE targets as unsupported; use inspect-managed-artifact for these.
- A real MCP client initialized server rea 6.8.0, listed 246 tools, called inspect_managed_artifact successfully, then closed the connection. Reproduce using scripts/verify-rea.mjs and the Node path in project configuration after a Debug build.

## Project link

.codex/config.toml registers the local absolute launcher with Node 24 and explicit Ghidra/JDK paths. It applies when Codex opens this trusted LaserCraft project. This chat began in D:\Development\REA; project configuration alone does not hot-load new tools into an already-running chat. No global MCP configuration was changed.

scripts/rea.ps1 is the immediate CLI link from LaserCraft. Examples:

```powershell
.\scripts\rea.ps1 --version
.\scripts\rea.ps1 inspect-managed-artifact src/LaserCraft.Domain/bin/Debug/net10.0/LaserCraft.Domain.dll --format json
```

CLI and test-client evidence is retained locally in ignored tmp/rea-*.json. Use supported static inspection and documented behavior for LightBurn research. No proprietary source or protected assets were extracted or reused.

At the original verification above, Docker was not required. Hopper was unsupported on Windows; IDA and Kuna were not configured. Global doctor health includes optional unavailable providers and does not imply that every listed analysis family works.

## Docker integration 0.8

Verified 2026-11-08 after the user's reconfiguration. The running container is `rea-rea-1`, image `rea-rea:node-fixed`. Its `/workspace` bind is `D:\Development\REA` (not the older nested `rea` checkout). The container's existing launcher is `/workspace/scripts/rea.mjs`, version 6.8.0. LaserCraft's project MCP entry now uses `docker exec -i rea-rea-1 node /workspace/scripts/rea.mjs --mcp`; `scripts/rea.ps1` uses the same container for CLI commands. Previous project launcher/configuration copies are retained in ignored `tmp/rea-*-before-docker.*`. No global client configuration or container image/setup was changed.

Arguments are container paths. Windows paths are not automatically translated, and LaserCraft's project directory is not mounted. Stage only a selected target explicitly when needed:

```powershell
docker cp 'C:\Program Files\LightBurn\LightBurn.exe' rea-rea-1:/tmp/lasercraft-lightburn-reference.exe
.\scripts\rea.ps1 inspect-pe-resources /tmp/lasercraft-lightburn-reference.exe --format json > tmp/lightburn-docker-resources-08.json
```

This read-only inspection succeeded: PE32+, x86-64, complete resource-directory traversal, 13 entries examined and 5 resources. Evidence `ev_9604b2ce02fbf16acffe270c1842b645f2639b140d6170e3e103bb2af25657cb`; target SHA-256 `89324957cc459d0a3d0abd5543dc2544ba7f35d611de0c9f37b03e3ae459c49e`. Full output remains in the ignored JSON. The payloads were not extracted or reused. This is static inventory evidence, not execution, UI behavior, tracing math or functional parity.

`scripts/verify-rea.mjs` uses the existing SDK under `D:\Development\REA\node_modules` and connects to the exact Docker MCP command. After explicit staging above, it initialized REA 6.8.0, listed 246 tools and successfully called `inspect_pe_resources`. The handshake summary is `tmp/rea-mcp-handshake.json`. Run it with the existing Node 24 runtime. The current chat has no REA tools in its connected catalog; the CLI and independent MCP client both work. Reopening the trusted LaserCraft project is needed for its new MCP registration to be loaded into Codex.

Provider inventory is saved in `tmp/rea-docker-providers-08.json`. The Docker configuration does not advertise an available Ghidra/IDA/Kuna deep-analysis provider, and its macOS native UI tools report unsupported host. Portable PE inspection works without those providers. Windows UI behavior was checked separately through computer use. Earlier host Ghidra inspection remains valid for its matching target digest; it is not a claim that Docker now supports every analysis family. No installation or protection bypass was attempted.

After verification, remove only the explicitly staged copy with `docker exec rea-rea-1 rm -- /tmp/lasercraft-lightburn-reference.exe`. The installed Windows target remains untouched. Stage it again before rerunning the MCP smoke test.
