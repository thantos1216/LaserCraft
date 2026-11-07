# LaserCraft

Original Windows laser design and engraving studio, under active development.

The current milestone has a compact laser-design workspace, rectangle/ellipse multi-object editing, 30 color layers, line/fill preview and offline playback, native save/load, restricted SVG import/export, themes and integrated help. Full functional parity and hardware execution are not implemented.

## Build and run

Requires .NET SDK 10.0.403.

```powershell
dotnet build LaserCraft.slnx
dotnet test LaserCraft.slnx
dotnet run --project src/LaserCraft.Desktop
```

See [Help](docs/help/Help.md), [progress](docs/roadmap/progress.md), [architecture](docs/architecture/overview.md) and [feature matrix](docs/research/feature-parity-matrix.md).

REA integration is project-scoped in `.codex/config.toml` and `scripts/rea.ps1`. It is not distributed with the application. See [REA findings](docs/research/rea-findings.md).
