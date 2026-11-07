# LaserCraft

Original Windows laser design and engraving studio, under active development.

The first working milestone supports vector rectangles/ellipses, selection and movement, numeric geometry editing, document tabs, layer settings, undo/redo, zoom/pan, native projects, SVG export, themes and integrated help. Hardware execution is not implemented.

## Build and run

Requires .NET SDK 10.0.403.

```powershell
dotnet build LaserCraft.slnx
dotnet test LaserCraft.slnx
dotnet run --project src/LaserCraft.Desktop
```

See [Help](docs/help/Help.md), [progress](docs/roadmap/progress.md), [architecture](docs/architecture/overview.md) and [feature matrix](docs/research/feature-parity-matrix.md).

REA integration is project-scoped in `.codex/config.toml` and `scripts/rea.ps1`. It is not distributed with the application. See [REA findings](docs/research/rea-findings.md).
