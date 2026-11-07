# Feature parity matrix - working baseline 0.3

2026-11-07. This is a categorized implementation backlog, not a claim of complete parity. D = documented in the supplied manual; R = requested in the specification but exact baseline behavior still needs research. The VM console shows LightBurn 2.2.01 (Trial); workspace panel presence was observed, detailed behavioral testing remains pending. Page references are physical PDF pages; see the full page index for further discovery.

| Capability | Evidence | LaserCraft status | Acceptance / next validation |
| --- | --- | --- | --- |
| Desktop workspace / accessible help | D p55 | Partial, UI checked | Contextual F1, menus, tooltips, accessibility coverage |
| Document tabs | R | Implemented, manual check | Save-before-close implemented; rename UI still planned |
| Dockable panels / floating windows | R | Planned; resizable sidebar only | Persist layout across monitors |
| Theme switching | R, BDK read-only review | Implemented | Light/dark and persistence UI validation |
| Workspace and keyboard presets | R | Planned | Round-trip user settings |
| Rectangles and ellipses | D p63-67 | Implemented; rectangle UI checked | Shape dimensions round-trip |
| Lines / Bezier curves | D p63 | Planned | Control points, closure and export |
| Selection / translation / sizing | D p63-67 | Partial: Ctrl-click multi-select, collective movement and numeric scaling | Drag handles, rotation and richer hit testing |
| Rotate / mirror / skew | R | Partial: horizontal/vertical mirror, tested | Rotation, skew and path transforms |
| Node editing and curve conversion | R | Planned | Preserve path topology |
| Grid / snap / guides / rulers | R | Partial: grid, 1 mm snap, rulers | Configurable units and guides |
| Undo / redo | R | Implemented, automated + UI tested | Bound memory, transaction edits |
| Group / ungroup | R | Planned | Hierarchy and selection tests |
| Alignment / distribution | R | Partial: edge/center alignment and center-on-bed, tested | Distribution and anchor modes |
| Union / subtract / intersect / weld | R | Planned | Geometry library/license evaluation |
| Offsets / kerf / smoothing | R | Planned | Degenerate geometry fixtures |
| Text / text-to-path / text-on-path | R | Planned | Font shaping and editable text |
| Arrays / nesting | R | Planned | Rotation and overlap constraints |
| Multiple pages | R | Planned | Per-page data and ordering |
| Layer assignment and settings | D p81 | Partial: create/reassign/reorder typed layers; independent settings and stored air | Sublayers, cut ordering, advanced settings |
| Fill / offset fill | D p81 | Partial: horizontal even-odd fill for rectangles/ellipses, tested | Offset fill, angles, overscan and controller planning |
| Native editable project | R | Partial v1, tested | Assets, recovery, migrations, previews |
| SVG | D p63-67 | Export + basic rect/ellipse/circle import tested | Paths, transforms, styling, broad unit coverage |
| DXF / PDF / AI / EPS | R | Planned | Codec/license review and malformed input tests |
| PNG / JPEG / BMP / TIFF / WebP / GIF | R | Partial: PNG/JPEG/BMP embedding, rendering, transforms and dedicated layers | Raster paths, TIFF/WebP/GIF and orientation coverage |
| Vector-to-bitmap | D p929 | Planned | Outlines/fills rendering correctness |
| Nondestructive raster adjustments | R | Planned | Preserve original, deterministic pipeline |
| Threshold raster engraving | D p1001 | Planned | Binary output and intensity fixtures |
| Error diffusion / ordered / grayscale modes | D p1001ff; R expanded algorithms | Planned | Algorithm fixtures + quality benchmarks |
| DPI / scan interval / overscan | R | Planned | Unit conversion and bounded toolpaths |
| Outline / centerline tracing | D p1705; R expanded tracing | Planned | Editable geometry with controlled error |
| Cut preview / time estimate | R | Partial: path/travel overlay and ideal estimate; simulation states tested | Acceleration, controller timing and physical validation |
| Path optimization / cut order | R | Partial: user-defined layer order | Inner-before-outer and geometry optimization |
| Material libraries / test grids | R | Planned | Serializable presets and calibration evidence |
| GRBL controller | R, Atezr priority | Partial: read-only serial/TCP monitor, simulated and loopback-tested | Hardware qualification and job queue |
| grblHAL / FluidNC / Marlin / Smoothieware | R | Partial: experimental GRBL queries for grblHAL/FluidNC; other families profile-only | Firmware-specific protocol qualification |
| Ruida / Trocen / Galvo controllers | R | Planned | Documented interoperability + licensing review |
| G-code generation | R | Planned | Bounds, units, power-off travel tests |
| Streaming / pause / resume / stop | R | Planned | Queue state machine and disconnect tests |
| Device setup / machine profiles | R | Implemented profile configuration and persistence, read-only monitor | Device-specific origin/power/air validation |
| Camera calibration / overlay | D p1705 | Planned | Measured calibration error |
| Print-and-cut / fiducials | R | Planned | Transform accuracy and recovery |
| Rotary calibration / compensation | R | Planned | Device-specific simulated fixtures |
| Art / asset library | R | Planned | Original assets and reference portability |
| Local AI / optional providers | R | Planned extension | No machine execution from AI output |
| Plugins / scripting | R | Planned | Versioned API and explicit permissions |
| Installer / updates / recovery | R | Planned | Signing, package notices and crash tests |

Before implementing each planned family, add a requirement with inputs, outputs, public standards, dependencies, performance targets, safety effects and acceptance fixtures. The 1784-page index supports discovery but does not substitute for detailed review of each family. Full manual-wide command inventory and legacy/current black-box comparisons remain Phase 0 work.
