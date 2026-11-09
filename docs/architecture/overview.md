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

VectorPaths is the single geometry boundary for immutable normalized polyline contours. World mapping applies scaling and mirroring before hit tests, planner edges and SVG export. Node moves rebuild normalized geometry from world positions, retaining identity and layer. UI node drags remain transient until release, so undo is one transaction. Fill rejects open contours and budgets rows times total path edges. SvgLinearPaths parses only M/L/H/V/Z; unsupported data fails the entire import.

ImageTracing performs bounded four-connected component filtering, emits oriented pixel boundary edges and walks closed contours with a deterministic right-turn rule at diagonal contacts. Collinear grid points are removed before mapping through the source image frame and mirror flags into VectorPaths. TraceResult carries its source record; application rejects changed sources and creates one new vector layer plus source-output suppression as an atomic history edit. The UI samples adjusted eight-bit grayscale independently of the engraving dither mode, applies its own threshold, and accepts only a current preview/document fingerprint. It cancels on dialog close and disposes replaced bitmaps.

## Addendum: 0.8 photograph tracing

PhotoTracing owns brightness-range selection, separable Gaussian noise blur and summed-area local-contrast selection. TraceRefinement owns sample-coordinate Ramer–Douglas–Peucker simplification and bounded corner rounding into polylines. It retains delicate contours when area/winding, edge intersection or nesting checks fail; inconclusive/budget-limited refinement retains the complete original trace with an explicit note. These checks limit damage from refinement, not establish universal photographic quality or curve equivalence.

The trace dialog serializes workers behind a semaphore, debounces live edits for 300 ms and caches only toned grayscale for its immutable source/settings and resolution/inversion key. Mask/refinement controls reuse that grayscale. New edits invalidate Apply immediately and cancel obsolete work; installation requires matching options and application requires the original document fingerprint. Closing cancels and drains jobs before disposing worker synchronization; preview bitmaps have explicit replacement/close ownership. These caches and temporary tuning never enter the native format. Original embedded source bytes remain unchanged.

## Addendum: 0.9 node cleanup

PathNodeEditing validates node addresses, edits world-coordinate immutable contours and delegates normalization/identity retention to VectorPaths.Create. Closing-edge insertion, minimum-node rules, contour removal and break/split semantics are owned here. No native format extension is needed. The canvas owns transient node selection bound to the exact source record, hit testing, segment projection and grab-offset drag state. A node edit installs one validated history snapshot; source changes or different selection make the stale node inapplicable. GUI commands and keyboard node edits share this same domain boundary.

## Addendum: 0.10 read-only diagnostics

GrblMonitor serializes status and settings requests through the same gate and links both deadlines to connection lifetime. Settings are installed only after an explicit terminal ok, with nonempty/unique numbered records and a bounded line count. GrblSetting retains raw/value text, including unknown numbers; descriptions are display metadata. GrblStatus parses known telemetry but keeps the full raw frame and explicit absent values; it does not cache or infer coordinate offsets/units. Query failures cause the desktop to disconnect before another exchange.

The UI binds its monitor to the connected profile, disables profile edits/switches and duplicate queries, and compares firmware S maximum only for display. No reported setting silently changes the profile. SerialStartupDelayMs is a backward-compatible optional profile property; the serial opener owns and disposes the newly opened transport if cancellation occurs during the delay. Device profiles remain separate from design documents. No job execution API was introduced.

## Addendum: 0.11 composition and disconnect boundary

PathComposition owns document-level separation/combination and returns the validated next document plus resulting selected IDs. World contours are normalized through VectorPaths.Create; separation retains the first contour's object identity and creates others, while combination retains the earliest selected object/position and requires a shared layer. No connector edges are introduced. The desktop only installs the result as one history edit. Degenerate standalone contours and aggregate format limits fail before mutation.

Monitor disposal uses an atomic initiation marker. New queries reject disposal at entry; queries already admitted and waiting for the semaphore are cancelled if they acquire it during closure, independently of the timing of linked-token callbacks. Both status/settings paths follow this rule. This distinction is covered in both queue orders after a Windows CI failure exposed the previous mismatch.

## Addendum: 0.12 joining

PathComposition.SelectedPaths owns validated document selection for separation, combination and joining. PathJoining prepares two world-space open contours, chooses the nearest endpoint orientation in stable order, applies an explicit midpoint/bridge rule, and validates the complete replacement document without mutating sources. It returns endpoint/gap facts for display rather than having the UI reinterpret normalized geometry. The desktop owns temporary options, preview and source/scope fingerprint checks; Apply installs one history entry. Shared VectorPaths normalization and existing planners/serializers handle the resulting single open contour. No native-format or transport extension was needed.

## Addendum: 0.13 SVG curves

SvgPaths now owns path tokenization, current-point/control-history semantics and mapped-millimetre Bézier flattening; SvgLinearPaths was removed. Smooth control history is updated per segment packet and reset by incompatible commands. Quadratics become cubic controls, then bounded adaptive subdivision produces ordinary VectorPaths contours. SvgImport owns whole-file XML/unit/viewBox interpretation and checks aggregate expanded vertices after each object. Unsupported input rejects the entire operation.

SvgImportPanel owns temporary tolerance/options and a cancellation lifetime. Parsing runs on a worker; the dialog drains it after cancellation/close. Installation requires the captured document fingerprint, selected destination layer and session, and creates any necessary vector layer only after successful parsing. Imported objects are installed/selected in one history edit. No native format or planner extension was needed, and the original curve handles are deliberately not represented as editable native curves.
