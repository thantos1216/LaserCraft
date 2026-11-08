# Progress

Updated 2026-11-07.

## Completed and checked

- Created D:\Development\LaserCraft and initialized Git.
- .NET 10 solution with isolated Domain, Avalonia Desktop and xUnit test projects.
- Millimetre viewport, grid, snapping, zoom/pan, rectangles/ellipses, selection/movement and numeric geometry editing.
- Document tabs; speed/power layer settings; undo/redo; validated .lcraft JSON save/load; SVG geometry export.
- Runtime light/dark preference; resizable inspector; integrated help generated from the repository help source.
- Build: 0 warnings / 0 errors. Tests: 9 passed. Desktop drawing/history/help checked interactively.
- Windows x64 framework-dependent Release publish created at artifacts/windows-x64 (requires the installed .NET 10 runtime).
- REA 6.8.0 checkout built and linked through project MCP configuration and CLI script. Successful MCP and Ghidra/managed inspection smoke tests.
- .NET, Rust/MSVC linker, Python venv, Windows SDK, Git and GPU inventory recorded.
- 1784-page manual text extracted locally and page index created; initial sections reviewed and feature backlog written.

## Incomplete / limitations

Phase 0 is not complete: full semantic feature inventory, detailed current-vs-legacy behavior observations remain pending (host now verified as 1.3.01). Get-VM fails with a host permission denial, but the existing VM console is accessible and LightBurn 2.2.01 (Trial) was observed there. REA works locally; it is not hot-loaded into the existing chat's tool catalog.

Phase 1 is partial: real docking, saved layouts, configurable toolbars and shortcuts, saved keyboard/workspace presets and crash recovery remain outstanding; compact menus and save-before-close tabs were added in 0.2. This milestone is usable but has no raster, text/path editor, G-code, transport, camera, rotary, AI or hardware support. It is not LightBurn parity or a production release.

## Next milestone

Complete the vector workflow: line/Bezier document primitives, path editing, SVG import with unit/transform validation, selection transforms, object alignment and bounded history. Update Help and acceptance tests with each capability. Start simulated GRBL job planning only after geometry and coordinate contracts are established. Continue manual/reference research alongside implementation.


## Milestone 0.2 - compact matching workspace

Reworked the layout to match the reference arrangement: menu categories, compact original vector icons, top numeric geometry controls, left drawing tools, right Cuts/Layers above Laser, and bottom palette. The host is now verified as LightBurn 1.3.01 from its live title/UI Automation and the user screenshot; VM reference is LightBurn Pro 2.2.01 Trial.

Added 30 default color layers; editable Line/Fill mode, speed, power, pass count, scan interval, output/show and stored air-assist state. Added multi-selection, combined movement/numeric scaling, copy/paste/duplicate, alignment, mirroring, center-on-bed and save-before-close project tabs. Added a restricted SVG import for rectangles/circles/ellipses with uniform viewBox/unit conversion; unsupported constructs reject the whole import.

Added independent rectangle/ellipse line and even-odd fill planning with bounded work/segment limits, off-bed output rejection, selected-only planning, travel overlay and ideal duration estimates. Start/Pause/Stop operate the offline simulator only. Edits and document switching invalidate a plan. No transport, machine commands or G-code export exist yet.

Build passes with zero warnings/errors; 24 tests pass. UI checked with samples/studio-demo.lcraft: 4 shapes across 3 layers, 590 segments, 17524.3 mm cut length and 758.0 s ideal duration. Preview visibly leaves the inner ring uncut; travel crosses it with no cutting. Running and Paused simulation states were observed. Full detailed parity remains incomplete. Disabled commands show future capability locations and are not represented as working.

Next: line/Bezier/text primitives and node editor, complete SVG path/transform support, better selection handles/hit tests, configurable machine/display coordinates and units, cutting-order optimization, and a GRBL protocol simulator before physical integration. Full feature and parameter inventory remains active research.

Release 0.2 publish: artifacts/windows-x64-v0.2 (framework-dependent, .NET 10). Versioned output preserves the previously running 0.1 application. Final build: zero warnings/errors; 24 passing tests.


## Milestone 0.3 - ordered typed layers, raster assets and device profiles

User-directed layer workflow replaces the 30 precreated layers for new projects: begin with one vector layer, create named Vector/Image layers, assign selected artwork, and reorder layers with up/down controls. List order is job order. Reordering remaps shape references and retains cut settings. Delete-empty protects populated layers. Every image requires a dedicated image layer, separate from all vector artwork and other images. Image duplication creates another layer. Layer settings include name/color, speed, power, mode, passes, interval, output/show and air assist.

PNG/JPEG/BMP original data is embedded, validated and displayed with a layer-colored border. Images share movement, resizing, mirroring, clipboard, selection and native persistence with vector objects. SVG vector imports can be reassigned like drawn geometry. Raster engraving paths remain unimplemented and reject enabled image output rather than generating incorrect vector paths. Size/pixel/data limits bound the current decoder/JSON format. samples/layers-and-image.lcraft demonstrates the workflow with an original generated PNG fixture.

Added LaserCraft.Devices: persistent validated profiles, GRBL simulator, serial/TCP transports and bounded read-only identity/status handshake. GRBL adapter is implemented; grblHAL/FluidNC use experimental compatible queries; Marlin/Smoothieware/Ruida/Trocen remain profile-only. No real hardware was contacted. Only $I and ? are sent by the monitor; no job, air, home or motion commands. Device origin/power/air capability are stored for future execution mapping. Apply device bed is explicit.

Initial build and 44 tests pass. UI verified: image renders with original pixels; simulator connects and reports Idle/MPos; image layer moves below the vector layer while retaining artwork/settings; New layer creates Layer 3; selecting the outline and Assign selected changes it to the new layer/color. Final validation is recorded in docs/testing/milestone-03.md.

Source is MIT-licensed. No proprietary code/assets were copied. Next: raster toolpaths and image adjustments, richer SVG paths, controller queue simulation and G-code generation, then separately authorized physical validation. Full LightBurn parity remains unfinished.

Final 0.3 validation: build 0 warnings / 0 errors; 47 tests passed, 0 failed. Release output is artifacts/windows-x64-v0.3. Prior versioned builds are preserved.


## Milestone 0.4 - raster preparation and mixed job planning

Added nondestructive per-image-layer brightness, contrast, gamma, inversion, threshold, Floyd-Steinberg, 4x4 Bayer and grayscale settings. Image preparation shows original and processed samples side by side and reports effective DPI. Settings survive native saves and history; source bytes remain unchanged. Fully transparent pixels remain off in every mode.

Raster scanlines now join vector layers in the existing ordered job plan. Equal-power cells form runs; zero-power spans and overscan are laser-off travel at scan speed; inter-row repositioning uses the ideal travel assumption. Supports grayscale power mapping, bidirectional/unidirectional rows, mirror placement, passes and cut-segment air requests. Overscan/geometry bounds and sample/work/segment budgets are checked explicitly.

Preview planning runs on a background worker with cancellation, serialized worker admission and snapshot/scope validation before results are installed. Stop, edits and document/scope changes cancel or discard stale work. Playback uses elapsed wall time to target 10x ideal speed. Dense path drawings sample at most about 25000 segments per frame while the complete plan controls timing. High-resolution dense-photo planning remains limited by the 200000-segment/4-million-sample/8-million-job-cell budgets; tiled/native planning is future work.

The original PNG sample decodes and generates paths in a headless native integration test. UI checked: original/processed Floyd-Steinberg view and a combined raster/vector plan of 13675 segments, 11509.0 mm cut length and 774.3 s ideal duration for samples/raster-engraving.lcraft. No physical output was performed. Controller connections remain read-only.

Final 0.4 checks: build zero warnings/errors; 65 tests passed (63 domain/device tests plus 2 native decoder integration tests). Publish output: artifacts/windows-x64-v0.4. No controller execution or physical air operation was performed.

## Milestone 0.5 - visual engraving workflow and offline export

Source/history uploaded to thantos1216/LaserCraft on GitHub. Main is the default branch. Windows CI builds and tests pushes/PRs; its initial run passed. Added visual RGB/palette layer picking, monochrome prepared canvas thumbnails, Stucki/Jarvis/Atkinson, power-weighted white-bed playback with beam marker and adjustable 1–100x speed. Added standalone GRBL 1.1 exporter with profile corner mapping, power scaling, explicit S0 off-feed spans, optional M8/M9 and final M5/M9. File generation only; no physical commands transmitted. Tracing and broader editor/controller parity remain unfinished. See milestone-05 validation and screenshot requirements for scope/limits.

Final 0.5 local validation: zero build warnings/errors; 79 tests pass. Visual checks verify monochrome artwork, color Apply/Undo, Stucki preview and beam playback. Release is artifacts/windows-x64-v0.5. REA runtime inspection recorded 112 entries and no recognized supported runtime candidates; it does not certify parity.

## Milestone 0.6 - editable straight paths

Added immutable normalized contour geometry, open/closed polylines, compound holes, line/fill planning and native/SVG persistence. Added line drafting (click/Enter/C/Backspace/Escape), square-node dragging with one undo transaction, conversion of rectangles/ellipses to paths, and closure of selected contours. Move/resize/copy/mirror/layers reuse the same path geometry. SVG import supports M/L/H/V/Z absolute/relative/implicit commands plus line/polygon/polyline; unsupported curves and transforms reject atomically. Full curves, advanced node editing and image tracing remain incomplete. Initial build has zero warnings/errors and 101 tests pass; see milestone-06 validation.

Final 0.6 validation: build zero warnings/errors; 101 tests pass. UI checks confirm line drafting/closure/one-step undo, node drag/undo and a compound-hole preview (145 segments, 4493.1 mm cut, 185.6 s ideal). Fixed a layer-selection crash by deferring list replacement until Avalonia completes its selection transaction; editing/preview selection rechecked. Release is artifacts/windows-x64-v0.6. Tracing remains a subsequent geometry consumer.
