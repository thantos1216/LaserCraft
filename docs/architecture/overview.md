# Architecture - decision 001

2026-11-07. Chosen: .NET 10, Avalonia 12.1.5, Fluent controls, original custom vector viewport. Avalonia uses Skia via its desktop renderer. No performance claim is made yet.

LaserCraft.Domain owns immutable shape/layer records, document validation, undo/redo snapshots, project serialization and SVG generation. LaserCraft.Desktop owns document sessions, file pickers, theme persistence, interactive viewport and help presentation. xUnit exercises domain behavior. Add an application orchestration project when device or background-processing workflows require it; avoid empty projects.

All geometry is millimetres with a top-left design origin. This is not a machine coordinate or homing contract. Two layers are created by default; the document supports multiple validated layers. Shape kinds currently cover rectangles and axis-aligned ellipses.

Native project v1 is UTF-8 JSON, constrained to 32 MB on input. It stores dimensions, name, shape IDs/geometry/layers and layer speed/power/output settings. Atomic save flushes a same-directory temporary file then replaces the destination. Crash recovery, migrations, embedded assets and preview integrity are future work.

Rust is reserved for profiled raster/geometry workloads. No Rust runtime is required for milestone 0.1; FFI is not yet implemented. Future FFI must specify version, owned buffers, cancellation, errors and thread safety before use. Python is development-only.

Device integration will be isolated from the editor, first using a simulated controller. No transport or laser execution path exists in this milestone. The default bed is an engineering assumption, not an Atezr profile.

The BDK WPF ThemeManager and ThemeDefinition were inspected read-only. Their WPF ResourceDictionary/SystemEvents coupling prevents direct use in Avalonia. The original LaserCraft implementation adopts runtime light/dark selection and account-local persistence. Portable palettes and workspace layouts remain planned; no BDK source was copied.

Known design debt: snapshot history is unbounded and copies complete lists; viewport has no spatial index; document tabs cannot close individually; only axis-aligned geometry is supported. Benchmark representative files before introducing native acceleration or a command-based history.


## Addendum: 0.3

LaserCraft.Devices references Domain and owns profile persistence and read-only GRBL transports/monitoring. Desktop orchestrates it through explicit UI actions. The default transport is an in-process simulator. No machine job API is exposed yet. Identity/status queries are bounded; shutdown cancels pending reads before disposing transports. Loopback TCP tests exercise real transport framing.

LayerKind separates vector and image job steps. One image per image layer is enforced. Layer list order is execution order; move operations remap every object reference without changing identities or settings. New projects create one vector layer; older projects keep existing lists. Additional layer settings remain per-layer records and participate in history/native saves.

RasterAsset embeds immutable original PNG/JPEG/BMP encoded data and metadata on an Image object. Desktop validates codec metadata and complete decode, then caches bounded-resolution previews by asset ID. Shared asset IDs must have identical data. Mirror flags preserve source bytes. Native saves enforce output size before replacement. JSON duplicates shared asset data on disk, so total encoded-data limits include repeated objects; asset-table deduplication/tiled storage remain future work. Raster path generation explicitly fails for enabled image layers.


## Addendum: 0.4 raster boundary

Domain owns validated RasterSettings, RGBA-to-darkness processing, immutable raster grids and spatial scanline/power mapping. Desktop owns Skia decoding/resampling and provides an explicit raster-sampling delegate to the planner. The domain does not depend on Skia/Avalonia. Processing is an independent deterministic C# reference implementation; native optimization requires representative benchmarks before replacing it.

Samples use sRGB-weighted luminance, tone operations, white alpha compositing and optional binary/grayscale quantization. Image sample counts are ceil(physical size / interval), then uniformly cover the object. Mirroring belongs to geometric planning, not source mutation. Toolpath segments explicitly store power, air request, travel state, speed and endpoints. Whitespace/overscan moves retain scan speed; inter-path travel has a distinct speed. Controller execution is not connected to this path model yet.

Preview workers are serialized and cancelable, with immutable document records/list snapshots and selected-ID copies. Result installation compares owner/document/scope against the current UI. Closing cancels outstanding work. Native decode/resize/PNG preview encoding are bounded but not interruptible internally; row processing and planning check cancellation regularly.

Offline GrblExport is a pure Domain formatter over a validated continuous ToolpathPlan and DeviceProfile. It does not reference transport APIs. The desktop rebuilds the scoped preview before exporting and rejects changed document/scope snapshots around the save picker. Canvas artwork caches processed monochrome thumbnails by image identity, physical size, interval and raster settings; two bounded workers serialize native image work, and outdated entries are discarded/disposed. Simulation mode hides editor artwork and renders the planned power field on white with a current beam marker.
