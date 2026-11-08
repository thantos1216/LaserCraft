# Feature parity matrix - working baseline 0.7

2026-11-07. This is a categorized implementation backlog, not a claim of complete parity. D = documented in the supplied manual; R = requested in the specification but exact baseline behavior still needs research. The VM console shows LightBurn 2.2.01 (Trial); workspace panel presence was observed, detailed behavioral testing remains pending. Page references are physical PDF pages; see the full page index for further discovery.

| Capability | Evidence | LaserCraft status | Acceptance / next validation |
| --- | --- | --- | --- |
| Desktop workspace / accessible help | D p55 | Partial, UI checked | Contextual F1, menus, tooltips, accessibility coverage |
| Document tabs | R | Implemented, manual check | Save-before-close implemented; rename UI still planned |
| Dockable panels / floating windows | R | Planned; resizable sidebar only | Persist layout across monitors |
| Theme switching | R, BDK read-only review | Implemented | Light/dark and persistence UI validation |
| Workspace and keyboard presets | R | Planned | Round-trip user settings |
| Rectangles and ellipses | D p63-67 | Implemented; rectangle UI checked | Shape dimensions round-trip |
| Lines / Bezier curves | D p63 | Partial: open/closed straight polylines and compound contours, tested | Bezier control points and arcs |
| Selection / translation / sizing | D p63-67 | Partial: Ctrl-click multi-select, collective movement and numeric scaling | Drag handles, rotation and richer hit testing |
| Rotate / mirror / skew | R | Partial: horizontal/vertical mirror, tested | Rotation, skew and path transforms |
| Node editing and curve conversion | R | Partial: drag path vertices, convert rectangle/ellipse to polylines, close contours | Insert/delete/join/split, curve handles |
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
| Fill / offset fill | D p81 | Partial: horizontal even-odd fill for rectangles/ellipses and closed compound paths, tested | Offset fill, angles, overscan and controller planning |
| Native editable project | R | Partial v1, tested | Assets, recovery, migrations, previews |
| SVG | D p63-67 | Export + rect/ellipse/circle/line/polygon/polyline and M/L/H/V/Z path import tested | Curves/arcs, transforms, styling, broad unit coverage |
| DXF / PDF / AI / EPS | R | Planned | Codec/license review and malformed input tests |
| PNG / JPEG / BMP / TIFF / WebP / GIF | R | Partial: PNG/JPEG/BMP embedding, rendering, transforms and dedicated layers | High-resolution optimization, TIFF/WebP/GIF and orientation coverage |
| Vector-to-bitmap | D p929 | Planned | Outlines/fills rendering correctness |
| Nondestructive raster adjustments | R | Partial: brightness/contrast/gamma/invert with original/processed view | Curves, sharpening, masks, calibrated materials, higher precision |
| Threshold raster engraving | D p1001 | Implemented for simulator paths, tested | Physical output qualification |
| Error diffusion / ordered / grayscale modes | D p1001ff; R expanded algorithms | Partial: Floyd-Steinberg, Stucki, Jarvis, Atkinson, 4x4 Bayer and grayscale planning, tested | More algorithms, photographic/material quality benchmarks |
| DPI / scan interval / overscan | R | Partial: interval/effective DPI, bidirectional scan and bounded off-power overscan | Scan angles, device acceleration and calibration |
| Outline / centerline tracing | D p1705; R expanded tracing | Partial: threshold pixel-edge outlines, holes, component filtering, preview and editable paths | Curve fitting, smoothing, centerlines, sketch and photographic qualification |
| Cut preview / time estimate | R | Partial: path/travel overlay and ideal estimate; simulation states tested | Acceleration, controller timing and physical validation |
| Path optimization / cut order | R | Partial: user-defined layer order | Inner-before-outer and geometry optimization |
| Material libraries / test grids | R | Planned | Serializable presets and calibration evidence |
| GRBL controller | R, Atezr priority | Partial: read-only serial/TCP monitor, simulated and loopback-tested | Hardware qualification and job queue |
| grblHAL / FluidNC / Marlin / Smoothieware | R | Partial: experimental GRBL queries for grblHAL/FluidNC; other families profile-only | Firmware-specific protocol qualification |
| Ruida / Trocen / Galvo controllers | R | Planned | Documented interoperability + licensing review |
| G-code generation | R | Partial: offline GRBL 1.1 export, bounds/units/power-off/origin/air tests | Physical qualification, other dialects |
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
