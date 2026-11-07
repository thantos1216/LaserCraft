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
