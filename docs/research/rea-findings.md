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

Docker fallback was not required. Hopper is unsupported on Windows; IDA and Kuna are not configured. Global doctor health includes optional unavailable providers and does not imply that every listed analysis family works.
