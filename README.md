# LaserCraft

Original Windows laser design and engraving studio, under active development.

The current milestone has a compact laser-design workspace, rectangle/ellipse multi-object editing, editable ordered vector/image layers, line/fill preview and offline playback, native save/load, restricted SVG import/export, themes and integrated help. Full functional parity and hardware execution are not implemented.

## Build and run

Requires .NET SDK 10.0.403.

```powershell
dotnet build LaserCraft.slnx
dotnet test LaserCraft.slnx
dotnet run --project src/LaserCraft.Desktop
```

See [Help](docs/help/Help.md), [progress](docs/roadmap/progress.md), [architecture](docs/architecture/overview.md) and [feature matrix](docs/research/feature-parity-matrix.md).

REA integration is project-scoped in `.codex/config.toml` and `scripts/rea.ps1`. It is not distributed with the application. See [REA findings](docs/research/rea-findings.md).

Version 0.3 adds embedded PNG/JPEG/BMP artwork and independently configured ordered layers, plus device profiles and GRBL read-only monitoring via simulator/serial/TCP. See Help for support limits. Original source is [MIT-licensed](LICENSE). Source and history are hosted at [thantos1216/LaserCraft](https://github.com/thantos1216/LaserCraft). GitHub Actions builds and tests Windows changes on main and pull requests.

Version 0.4 adds image preparation (threshold, Floyd-Steinberg, Bayer, grayscale and tone controls), ordered raster/vector scanline planning and cancellable background previews. Use samples/raster-engraving.lcraft to try it; hardware execution remains unavailable.

Version 0.5 adds monochrome canvas artwork, Stucki/Jarvis/Atkinson dithering, a visual layer color picker, white-bed beam playback and offline GRBL 1.1 export. No physical job execution is implemented.

Version 0.6 adds straight path drawing, vertex dragging, shape-to-path conversion, open/closed compound paths and SVG M/L/H/V/Z, polygon, polyline and line import. See samples/paths-and-nodes.lcraft. Bezier curves and image tracing remain planned.

Version 0.7 adds image outline tracing with threshold/inversion, speckle filtering, faded contour preview and undoable creation of a separate vector layer. Try samples/trace-outlines.lcraft; curves, smoothing and centerline tracing remain planned.
