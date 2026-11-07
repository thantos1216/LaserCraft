# Architecture - decision 001

2026-11-07. Chosen: .NET 10, Avalonia 12.1.5, Fluent controls, original custom vector viewport. Avalonia uses Skia via its desktop renderer. No performance claim is made yet.

LaserCraft.Domain owns immutable shape/layer records, document validation, undo/redo snapshots, project serialization and SVG generation. LaserCraft.Desktop owns document sessions, file pickers, theme persistence, interactive viewport and help presentation. xUnit exercises domain behavior. Add an application orchestration project when device or background-processing workflows require it; avoid empty projects.

All geometry is millimetres with a top-left design origin. This is not a machine coordinate or homing contract. Two layers are created by default; the document supports multiple validated layers. Shape kinds currently cover rectangles and axis-aligned ellipses.

Native project v1 is UTF-8 JSON, constrained to 32 MB on input. It stores dimensions, name, shape IDs/geometry/layers and layer speed/power/output settings. Atomic save flushes a same-directory temporary file then replaces the destination. Crash recovery, migrations, embedded assets and preview integrity are future work.

Rust is reserved for profiled raster/geometry workloads. No Rust runtime is required for milestone 0.1; FFI is not yet implemented. Future FFI must specify version, owned buffers, cancellation, errors and thread safety before use. Python is development-only.

Device integration will be isolated from the editor, first using a simulated controller. No transport or laser execution path exists in this milestone. The default bed is an engineering assumption, not an Atezr profile.

The BDK WPF ThemeManager and ThemeDefinition were inspected read-only. Their WPF ResourceDictionary/SystemEvents coupling prevents direct use in Avalonia. The original LaserCraft implementation adopts runtime light/dark selection and account-local persistence. Portable palettes and workspace layouts remain planned; no BDK source was copied.

Known design debt: snapshot history is unbounded and copies complete lists; viewport has no spatial index; document tabs cannot close individually; only axis-aligned geometry is supported. Benchmark representative files before introducing native acceleration or a command-based history.
