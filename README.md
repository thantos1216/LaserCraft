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

Version 0.8 adds photograph-oriented trace sliders, Gaussian noise reduction, polyline smoothing and node reduction, local-contrast sketch mode, live updates, mask preview and detail zoom. Try samples/photo-tracing.lcraft; its NASA photograph has separate [credits](samples/PHOTO-CREDITS.md). Bézier fitting, centerlines and region cropping remain planned. The REA development launcher now uses the configured Docker container; see [connection details](docs/research/rea-findings.md#docker-integration-08).

Version 0.9 adds manual path cleanup: visible node selection, midpoint/edge insertion, node and contour deletion, and breaking closed/open contours with undo. Try Nodes mode in samples/paths-and-nodes.lcraft or on a photo trace. Curve handles and endpoint joining remain planned.

Version 0.10 improves GRBL connection testing: read-only firmware settings, richer reported telemetry, serial-port refresh/startup delay, profile locking while connected, and clearer connection status. Simulator and TCP loopback tests pass; physical USB hardware is unverified. [Connection steps](docs/help/Help.md#test-a-grbl-connection) describe what can be tested without machine execution.

Version 0.11 adds separation of compound trace contours into independently selectable path objects and same-layer combination without joining endpoints. It also fixes a Windows CI-discovered disconnect race in queued monitor queries. [Validation](docs/testing/milestone-11.md) records tests and remaining limits.

Version 0.12 adds previewed joining of two open contours with a maximum gap and explicit midpoint-merge or laser-on bridge methods. Try samples/endpoint-joining.lcraft. Joining remains an editing operation; physical machine execution is not implemented.

Version 0.13 imports SVG cubic/quadratic Bézier paths (C/S/Q/T, including relative forms) as editable polylines with a physical tolerance option and cancellable background processing. Try samples/bezier-curves.svg. Original control handles, arcs and transforms remain unsupported; this is a geometry-import extension, not native Bézier editing.

Version 0.16 adds Save connection log for USB/TCP test diagnosis and a transferable Windows x64 package. Build a portable ZIP with PowerShell 7: `./scripts/package-windows.ps1`. It includes the .NET runtime, source/build metadata, per-file hashes and the [USB connection test guide](docs/help/USB-CONNECTION-TEST.md). Extract all files and run on the computer with the USB laser attached. Physical machine execution remains unavailable. Version 0.14 added always-live trace preview/manual simulator controls; 0.15 added per-connection COM selection and read-only firmware/modal comparisons.

Version 0.17 adds Arrange commands for rectangular arrays, horizontal/vertical center/gap distribution, collective vector rotation and path-direction reversal. Try samples/arrangement-demo.lcraft. Image copies get independent layers/settings; vector rotation converts primitives to paths and does not yet rotate rasters. Operations support Undo and existing native/SVG/offline output. This remains an alpha without physical execution.
