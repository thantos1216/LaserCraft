# Feature parity matrix - working baseline 0.30

0.30: captured-head return after successful output with acknowledgement/Idle/MPos checks; immediate layer edits; whole-layer/per-object pass scheduling for vectors, fills and images; pass column and project/preset persistence; centered vector icons. No return after abort/error. Pass order does not guarantee cooling. User's successful 0.29 cuts are separate from new-feature verification; see [validation](../testing/milestone-30.md).

0.29: orderly pre-close port cleanup, remembered device/control defaults, automatic device bed, direct Run and compact icons, separate Help window, File-menu G-code export, rectangle/circle framing, and main design/Move/layer mm/in units. Advanced dialogs remain explicitly metric. Physical USB recovery is not established by the new logs or automated tests. See [validation](../testing/milestone-29.md).

0.28: capability-checked delayed automatic homing, persistent connected Home controls, live status during job waits, bounded silence/missing-ack failures, and explicit-Abort connection recovery without automatic motion. Automated coverage; physical Attzr confirmation pending. See [validation](../testing/milestone-28.md).

0.27 adds Absolute Coordinates, Current Position and session-only User Origin for reviewed GRBL output, nine drawing anchors and selected-output bounds. Exact bed positioning and origin capture require Home/Idle; framing shares placement with cutting and can run automatically first. New behavior has domain/fake/TCP coverage. User Origin persistence, configurable finish position, live placement overlay and physical qualification remain pending. See [reference scope](placement-reference-27.md).

2026-11-07. This is a categorized implementation backlog, not a claim of complete parity. D = documented in the supplied manual; R = requested in the specification but exact baseline behavior still needs research. The VM console shows LightBurn 2.2.01 (Trial); workspace panel presence was observed, detailed behavioral testing remains pending. Page references are physical PDF pages; see the full page index for further discovery.

| Capability | Evidence | LaserCraft status | Acceptance / next validation |
| --- | --- | --- | --- |
| Desktop workspace / accessible help | D p55 | Partial, UI checked | Contextual F1, menus, tooltips, accessibility coverage |
| Document tabs | R | Implemented, manual check | Save-before-close implemented; rename UI still planned |
| Dockable panels / floating windows | R | Planned; resizable sidebar only | Persist layout across monitors |
| Theme switching | R, BDK read-only review | Implemented | Light/dark and persistence UI validation |
| Workspace and keyboard presets | R | Planned | Round-trip user settings |
| Rectangles and ellipses | D p63-67 | Implemented; rectangle UI checked | Shape dimensions round-trip |
| Lines / Bezier curves | D p63; SVG 2 path semantics | Partial: polylines/compound contours; cubic/quadratic SVG curves imported as tolerance-controlled polylines, tested | Native Bézier control points and arcs |
| Selection / translation / sizing | D p63-67 | Partial: Ctrl-click and directional bounds-box selection, group/individual override, collective movement and numeric scaling | Drag handles, rotation and richer hit testing |
| Rotate / mirror / skew | R | Partial: mirror and collective vector rotation, tested; rectangles/ellipses become polylines | Raster rotation, skew and native curve transforms |
| Node editing and curve conversion | R | Partial: node editing, open/split at node, shape conversion, separate/combine, nearest-endpoint joining with midpoint/bridge preview and closing; tested | Manual endpoint pairing, multi-path joining, curve handles and Bézier conversion |
| Grid / snap / guides / rulers | R | Partial: grid, 1 mm snap, rulers | Configurable units and guides |
| Undo / redo | R | Implemented, automated + UI tested | Bound memory, transaction edits |
| Group / ungroup | R | Partial: flat groups, selection/individual override, group-aware arrangement, independent copies and native persistence tested | Nested hierarchy, names and locks |
| Alignment / distribution | R | Partial: alignment, center-on-bed and horizontal/vertical center/gap distribution, tested | Additional anchor modes |
| Union / subtract / intersect / weld | R | Planned | Geometry library/license evaluation |
| Offsets / kerf / smoothing | R | Planned | Degenerate geometry fixtures |
| Text / text-to-path / text-on-path | R | Partial: editable basic Latin source with cached closed paths, font/style/size/line spacing/tolerance, conversion and persistence tested | Complex-script shaping, kerning/ligatures, text-on-path, welding and variable text |
| Arrays / nesting | R | Rectangular arrays with dedicated image-copy layers and expansion checks, tested | Circular arrays, nesting and overlap constraints |
| Multiple pages | R | Planned | Per-page data and ordering |
| Layer assignment and settings | D p81 | Partial: create/reassign/reorder typed layers; independent settings and stored air | Sublayers, cut ordering, advanced settings |
| Fill / offset fill | D p81 | Partial: horizontal even-odd fill for rectangles/ellipses and closed compound paths, tested | Offset fill, angles, overscan and controller planning |
| Native editable project | R | Partial v1, tested | Assets, recovery, migrations, previews |
| SVG | D p63-67; W3C SVG 2 paths | Export + basic vector elements; M/L/H/V/Z/C/S/Q/T paths, physical-tolerance flattening and cancellable import options tested | Arcs, transforms, styling, native curve retention and broad conformance |
| DXF / PDF / AI / EPS | R | Planned | Codec/license review and malformed input tests |
| PNG / JPEG / BMP / TIFF / WebP / GIF | R | Partial: PNG/JPEG/BMP embedding, rendering, transforms and dedicated layers | High-resolution optimization, TIFF/WebP/GIF and orientation coverage |
| Vector-to-bitmap | D p929 | Planned | Outlines/fills rendering correctness |
| Nondestructive raster adjustments | R | Partial: brightness/contrast/gamma/invert with original/processed view | Curves, sharpening, masks, calibrated materials, higher precision |
| Threshold raster engraving | D p1001 | Implemented for simulator paths, tested | Physical output qualification |
| Error diffusion / ordered / grayscale modes | D p1001ff; R expanded algorithms | Partial: Floyd-Steinberg, Stucki, Jarvis, Atkinson, 4x4 Bayer and grayscale planning, tested | More algorithms, photographic/material quality benchmarks |
| DPI / scan interval / overscan | R | Partial: interval/effective DPI, bidirectional scan and bounded off-power overscan | Scan angles, device acceleration and calibration |
| Outline / centerline tracing | D p1705; official 2.2 TraceImage reference; host 1.3.01 observation | Partial: brightness range, noise blur, polyline smoothing/reduction, local-contrast sketch, speckle filtering, live zoomable preview; one NASA photograph integration fixture | Bézier fitting, centerlines, region selection, broader photographic quality/scale qualification; algorithm equivalence unverified |
| Cut preview / time estimate | R | Partial: path/travel overlay and ideal estimate; simulation states tested | Acceleration, controller timing and physical validation |
| Path optimization / cut order | R | Partial: user-defined layer order | Inner-before-outer and geometry optimization |
| Material libraries / test grids | R | Partial: typed local recipes, capture/replace/delete/apply, raster settings, backup, validation and Undo tested | Import/export, categories/search, per-device binding, material-test grids and physical calibration |
| GRBL controller | R, Atezr priority; official GRBL 1.1 protocol | Partial: query diagnostics, opt-in guided X/Y/pulse/cancel/off and reviewed one-line test and single-line console/settings writes via serial/TCP; telemetry, startup delay, simulator/UI and TCP loopback tested | User observed query-only USB connection in 0.14; user-reported 0.24 line cut succeeded; new homing/document qualification and startup/reset variants pending |
| grblHAL / FluidNC / Marlin / Smoothieware | R | Partial: experimental GRBL queries for grblHAL/FluidNC; other families profile-only | Firmware-specific protocol qualification |
| Ruida / Trocen / Galvo controllers | R | Planned | Documented interoperability + licensing review |
| G-code generation | R | Partial: offline GRBL 1.1 export, bounds/units/power-off/origin/air tests | Physical qualification, other dialects |
| Streaming / pause / resume / stop | R | Initial reviewed vector/raster snapshot streamer, machine-anchor placement with signed range/override, laser-off framing/return, hold/resume and reset abort; scripted/TCP tested | Physical qualification, dense-raster throughput, richer placement/recovery |
| Device setup / machine profiles | R | Implemented profile configuration/persistence, query monitor and opt-in guided GRBL controls | Device-specific origin/power/air validation |
| Camera calibration / overlay | D p1705 | Planned | Measured calibration error |
| Print-and-cut / fiducials | R | Planned | Transform accuracy and recovery |
| Rotary calibration / compensation | R | Planned | Device-specific simulated fixtures |
| Art / asset library | R | Planned | Original assets and reference portability |
| Local AI / optional providers | R | Planned extension | No machine execution from AI output |
| Plugins / scripting | R | Planned | Versioned API and explicit permissions |
| Installer / updates / recovery | R | Planned | Signing, package notices and crash tests |

Before implementing each planned family, add a requirement with inputs, outputs, public standards, dependencies, performance targets, safety effects and acceptance fixtures. The 1784-page index supports discovery but does not substitute for detailed review of each family. Full manual-wide command inventory and legacy/current black-box comparisons remain Phase 0 work.

## Device-control audit 0.14

See [observed LightBurn 2.2 device/Move controls](lightburn-22-device-controls.md). LaserCraft adds always-automatic trace updates, simulator X/Y/pulse rehearsal and offline M7/M8 selection. Guided GRBL X/Y/pulse/cancel/off is implemented with scripted/TCP tests; physical verification remains pending. Streaming, Z/U, saved/work/finish positions, advanced transfer/buffer/G-code options and full parity remain planned. Simulator verification does not establish physical controller behavior.

## USB diagnostics 0.15

Per-connection detected COM selection and a 430 × 430 Attzr USB draft are implemented. Source-preserving $G modal state and read-only settings/status comparisons are tested. Matches do not establish execution readiness, homing or hardware compatibility. USB-driver and physical controller testing remain unavailable; guided GRBL jog/pulse scheduling is tested against a local peer; physical machine execution remains unverified.

## Connection support 0.16

Connection journal export and Windows x64 self-contained packaging are implemented for remote testing. Transport logs retain attempted writes, replies/errors and closure with bounded storage; they do not demonstrate controller execution or physical compatibility. Live command scheduling and machine execution remain planned.

## Console 0.19

Console now has a single-line command box and Submit/Enter. Default query mode and explicit per-connection setting/G-code permission, raw replies, history, timeout and reconnect-after-uncertainty are implemented and tested with simulator/TCP loopback. Raw commands can move/fire or persist settings; physical USB behavior remains unverified. 0.22 adds separate guided GRBL motion/pulse controls. Firmware-image flashing, hardware emergency controls, macros and job streaming remain unavailable.
