# Feature parity matrix - initial baseline

2026-11-07. This is a categorized implementation backlog, not a claim of complete parity. D = documented in the supplied manual; R = requested in the specification but exact baseline behavior still needs research. The VM console shows LightBurn 2.2.01 (Trial); workspace panel presence was observed, detailed behavioral testing remains pending. Page references are physical PDF pages; see the full page index for further discovery.

| Capability | Evidence | LaserCraft status | Acceptance / next validation |
| --- | --- | --- | --- |
| Desktop workspace / accessible help | D p55 | Partial, UI checked | Contextual F1, menus, tooltips, accessibility coverage |
| Document tabs | R | Implemented, manual check | Add individual close and rename |
| Dockable panels / floating windows | R | Planned; resizable sidebar only | Persist layout across monitors |
| Theme switching | R, BDK read-only review | Implemented | Light/dark and persistence UI validation |
| Workspace and keyboard presets | R | Planned | Round-trip user settings |
| Rectangles and ellipses | D p63-67 | Implemented; rectangle UI checked | Shape dimensions round-trip |
| Lines / Bezier curves | D p63 | Planned | Control points, closure and export |
| Selection / translation / sizing | D p63-67 | Partial | Multi-select, drag handles, transforms |
| Rotate / mirror / skew | R | Planned | Matrix and coordinate tests |
| Node editing and curve conversion | R | Planned | Preserve path topology |
| Grid / snap / guides / rulers | R | Partial: grid, 1 mm snap | Configurable units, guides, rulers |
| Undo / redo | R | Implemented, automated + UI tested | Bound memory, transaction edits |
| Group / ungroup | R | Planned | Hierarchy and selection tests |
| Alignment / distribution | R | Planned | Anchor and spacing tests |
| Union / subtract / intersect / weld | R | Planned | Geometry library/license evaluation |
| Offsets / kerf / smoothing | R | Planned | Degenerate geometry fixtures |
| Text / text-to-path / text-on-path | R | Planned | Font shaping and editable text |
| Arrays / nesting | R | Planned | Rotation and overlap constraints |
| Multiple pages | R | Planned | Per-page data and ordering |
| Layer assignment and settings | D p81 | Partial: stored speed/power + two defaults | Modes, passes, sublayers, visibility |
| Fill / offset fill | D p81 | Planned | Nested outlines and fill-rule fixtures |
| Native editable project | R | Partial v1, tested | Assets, recovery, migrations, previews |
| SVG | D p63-67 | Export rectangles/ellipses tested; import planned | Units, paths, transforms, styling |
| DXF / PDF / AI / EPS | R | Planned | Codec/license review and malformed input tests |
| PNG / JPEG / BMP / TIFF / WebP / GIF | R | Planned | Decode dimensions, color, orientation |
| Vector-to-bitmap | D p929 | Planned | Outlines/fills rendering correctness |
| Nondestructive raster adjustments | R | Planned | Preserve original, deterministic pipeline |
| Threshold raster engraving | D p1001 | Planned | Binary output and intensity fixtures |
| Error diffusion / ordered / grayscale modes | D p1001ff; R expanded algorithms | Planned | Algorithm fixtures + quality benchmarks |
| DPI / scan interval / overscan | R | Planned | Unit conversion and bounded toolpaths |
| Outline / centerline tracing | D p1705; R expanded tracing | Planned | Editable geometry with controlled error |
| Cut preview / time estimate | R | Planned | Simulation timestamps and travel display |
| Path optimization / cut order | R | Planned | Inner-before-outer constraints |
| Material libraries / test grids | R | Planned | Serializable presets and calibration evidence |
| GRBL controller | R, Atezr priority | Planned | Simulated serial protocol before hardware |
| grblHAL / FluidNC / Marlin / Smoothieware | R | Planned | Protocol adapter qualification |
| Ruida / Trocen / Galvo controllers | R | Planned | Documented interoperability + licensing review |
| G-code generation | R | Planned | Bounds, units, power-off travel tests |
| Streaming / pause / resume / stop | R | Planned | Queue state machine and disconnect tests |
| Device setup / machine profiles | R | Planned | Verify firmware, envelope and power range |
| Camera calibration / overlay | D p1705 | Planned | Measured calibration error |
| Print-and-cut / fiducials | R | Planned | Transform accuracy and recovery |
| Rotary calibration / compensation | R | Planned | Device-specific simulated fixtures |
| Art / asset library | R | Planned | Original assets and reference portability |
| Local AI / optional providers | R | Planned extension | No machine execution from AI output |
| Plugins / scripting | R | Planned | Versioned API and explicit permissions |
| Installer / updates / recovery | R | Planned | Signing, package notices and crash tests |

Before implementing each planned family, add a requirement with inputs, outputs, public standards, dependencies, performance targets, safety effects and acceptance fixtures. The 1784-page index supports discovery but does not substitute for detailed review of each family. Full manual-wide command inventory and legacy/current black-box comparisons remain Phase 0 work.
