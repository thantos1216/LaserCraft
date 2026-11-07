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

Phase 0 is not complete: full semantic feature inventory, exact host LightBurn version and detailed current-vs-legacy observations remain pending. Get-VM fails with a host permission denial, but the existing VM console is accessible and LightBurn 2.2.01 (Trial) was observed there. REA works locally; it is not hot-loaded into the existing chat's tool catalog.

Phase 1 is partial: real docking, saved layouts, configurable toolbars and shortcuts, polished menus, per-tab closing and crash recovery remain outstanding. This milestone is usable but has no raster, text/path editor, G-code, transport, camera, rotary, AI or hardware support. It is not LightBurn parity or a production release.

## Next milestone

Complete the vector workflow: line/Bezier document primitives, path editing, SVG import with unit/transform validation, selection transforms, object alignment and bounded history. Update Help and acceptance tests with each capability. Start simulated GRBL job planning only after geometry and coordinate contracts are established. Continue manual/reference research alongside implementation.
