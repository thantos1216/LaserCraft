# Progress

## Current milestone 0.24 - supervised one-line cut qualification

User supplied 0.14 screenshots and 48 annotated firmware settings: identity/status/settings queries succeeded; motion/cutting was not established. Added reviewed single-axis, single-pass physical line tests with separate control/fire/clear-path authority, bounded length/feed/power, fresh settings/modal checks, queued off and final Idle reporting. This is not arbitrary job streaming. Defaults are mark examples, not a 5 mm plywood through-cut recipe.

378 tests pass (366 unit, 12 integration), including exact fragmented TCP line sequence and fail-closed modes/permissions/cancellation/completion. Desktop checked the disconnected line-test control is disabled; the review/run workflow has not been physically exercised. See [validation](../testing/milestone-24.md) and [observations](../research/attzr-user-results-24.md). Actual motion, optical output and cuts remain user qualification work.

## Milestone 0.23 - material presets

Enabled Material Library with named/annotated typed recipes, optional thickness, capture from applied layer settings, replacement, confirmed deletion and undoable application. Target layer identity, output/visibility and artwork remain intact. Local versioned persistence validates before save and retains one .bak; failed load blocks writes. No factory recipes or automatic machine commands.

363 tests pass (351 unit, 12 integration), covering settings/type preservation, output/history, raster recipes, malformed/oversized data, duplicate IDs, limits, file round-trip and backup. See [validation](../testing/milestone-23.md). Import/export, categories, device binding and automated material-test grids remain planned. Hardware qualification remains pending the user USB test.

## Milestone 0.22 - guided GRBL control qualification

Added separate per-connection motion/firing permissions, bounded relative X/Y jogs with M5 first, raw-byte jog cancellation, laser-off and short controller-dwell pulse batches. Fresh Idle and reported power/laser-mode settings gate pulse tests; uncertainty requires reconnect without retries. Console/manual exchanges share one owning gate. Serial/TCP encoding preserves 0x85. Hardware emergency control, homing and job streaming remain incomplete.

353 tests pass (341 unit, 12 integration), including fragmented real TCP loopback. Desktop checked disconnected defaults, simulator 1 mm X motion, pulse automatic off and disconnect disable. No physical endpoint was opened or laser commands sent. Physical USB, actual motion and optical pulse behavior remain unverified. See [validation](../testing/milestone-22.md).

## Milestone 0.21 - drag-box selection

Added directional selection boxes from empty canvas: containment left-to-right, bounds overlap right-to-left, Ctrl additive and Alt individual-member selection. Groups include hidden companions with explicit selection counts. Selection uses stored bounds rather than exact contour intersection and leaves document/history unchanged.

318 tests pass (307 unit, 11 integration). Desktop verified selecting three sample rectangles, enclosing only two, and crossing part of the third; the sample remained clean. See [validation](../testing/milestone-21.md). Physical USB and guided hardware execution qualification remain pending.

## Milestone 0.20 - artwork groups

Added flat Group/Ungroup, whole-group selection/movement, individual Alt selection, hidden-member reporting, group-aware alignment/distribution and independent copy/array group identities. Native saves and geometry/text regeneration preserve membership. Groups retain independent layer settings and do not change burn order. Nested groups, names and locking remain planned.

314 tests pass (303 unit, 11 integration). Desktop checked Ctrl+G, selection through one member, collective 10 mm movement, Ctrl+Shift+G and Undo back to the unchanged sample. See [validation](../testing/milestone-20.md). Physical USB testing remains pending; guided hardware movement/fire and job streaming remain incomplete.

## Milestone 0.19 - console and GRBL setting changes

Added Console command input, Submit/Enter, bounded visible log/history, timeout, raw replies/results and a per-connection opt-in for setting writes/custom G-code. Queries are the default. Commands share the diagnostic gate and await a terminal response. Interrupted writes leave the session unable to send further queries/commands until reconnect; no retry is performed. Simulator settings support write/readback for selected numeric settings. User clarified firmware updates means setting changes, not flashing.

306 tests pass (295 unit, 11 integration). Coverage includes command framing/permission, push/error/alarm/ack handling, queue serialization, timeout/cancellation, limits/restarts, simulator settings and actual fragmented TCP loopback. Desktop verified query submission, default write rejection, opt-in $32=0/$$ readback, Up history and disconnect disable/reset. See [validation](../testing/milestone-19.md). Raw console can actuate physical hardware, but physical USB/controller behavior is unverified; guided Move/fire, job streaming, hardware emergency controls and full production readiness remain incomplete.


## Milestone 0.18 - editable basic text

Added create/edit text dialogs with installed font/style selection, em height, multiline spacing, outline tolerance and prepared preview. Text is represented as cached closed paths with optional immutable source settings, so saved output does not require font resolution. Editing retains identity/layer/anchor and affine resize/mirror factors. Geometry-changing operations detach the source; conversion and Undo are explicit. Basic Latin only; complex shaping, text-on-path, welding and variable text remain planned.

277 tests pass (266 unit, 11 integration). Tests cover source limits, invalid metadata, affine updates/history, cached native output without fonts, detach/preserve behavior, installed font styles, multiline outlines/holes, line/fill/SVG/G-code and cancellation. Desktop verified initial preview after fixing duplicate selection-event invalidation, stale Apply blocking, bold multiline creation, edit/Undo, conversion guard and Undo to clean empty. See [validation](../testing/milestone-18.md). Physical hardware and complete parity/production readiness remain unverified/unfinished.


## Milestone 0.17 - editor arrangement operations

Added rectangular arrays with total rows/columns, cell gaps, live resource/bounds summary, original/copy selection and dedicated copied image layers. Added center/gap distribution on both axes, collective vector rotation and contour-direction reversal. Operations preserve cut associations, validate before replacement and apply as one undo transaction. Vector rotation converts rectangles/ellipses to sampled paths; raster rotation, native curves, circular arrays and nesting remain planned.

260 tests pass (252 unit, 8 integration). Coverage includes geometry, mirrors, compound holes, expansion budgets, independent raster-layer settings/real PNG planning, history and native/SVG/G-code round trips. Desktop checked over-budget rejection, 18-object array/90-segment preview (1080 mm), equal gaps, 90-degree rotation, direction reversal and Undo to the clean original fixture. See [validation](../testing/milestone-17.md) and [requirements](../research/arrangement-requirements-17.md). Physical execution and production readiness remain unfinished.


## Milestone 0.16 - transferable connection testing

Added a bounded in-memory connection journal and user-chosen text export, including failed attempts, raw escaped query/reply lines, errors, selected endpoint/profile facts and close events. Snapshot export remains available after disconnect and never uploads automatically. Instrumentation preserves simulator ownership and monitor cancellation semantics. Status reads now surface ALARM immediately. Added a self-contained Windows x64 packaging script, source/build metadata, file hashes and USB testing guide.

232 tests pass (226 unit, 6 integration), and desktop simulator connection/disconnect/log export/cancellation were checked. Exported text retained exact escaped $I newline, identity/status replies and close event. See [validation](../testing/milestone-16.md). Packaging verification is recorded there separately. Physical USB, command scheduling, motion/fire/cuts and complete feature parity remain unfinished.


## Milestone 0.15 - USB connection diagnostics

Added an explicit per-connection detected COM selector, refresh without port opening, and an unsaved Attzr 35 W USB draft using the requested 430 × 430 mm bed. Unknown manufacturer defaults remain labeled. Added bounded, serialized $G querying and source-preserving modal parsing. The Connection check compares settings/modes/status against the connected profile, distinguishes status units from program units, records the collection interval and never grants motion/fire authority.

225 tests pass (219 unit, 6 integration), including fragmented real loopback TCP, missing/duplicate/conflicting modal data, acknowledgement completion, cancellation, mismatches and explicit unknown homing. Desktop verified simulator report/source evidence, disconnect and draft cancellation. See [validation](../testing/milestone-15.md). Physical USB, live command scheduling, jogging/fire/cuts and full feature parity remain unfinished.


## Milestone 0.14 - dynamic tracing and manual-control rehearsal

Trace sliders and numeric values always update the photograph overlay automatically after a 150 ms debounce; the manual Update/live-toggle path is removed. Stale work is cancelled and cannot be applied. Added simulator-only X/Y distance/feed controls, bounded timed power pulses, explicit Stop, shared simulated position/S status and bounded rehearsal history. Physical serial/TCP profiles cannot acquire these controls. Added per-profile M7/M8 air-output selection for offline G-code with M9 off and legacy M8 default.

Release build and 204 tests pass (198 unit, 6 integration). Desktop verified X/Y jogs, automatic pulse off, shared status, disconnect disabling and live numeric/slider photograph updates with cancellation leaving one unchanged source object. See [validation](../testing/milestone-14.md). The [VM 2.2 inventory](../research/lightburn-22-device-controls.md) records additional missing options. Physical execution, first cuts and complete parity remain unfinished.


## Milestone 0.13 - SVG curve interoperability

Added cubic/smooth-cubic and quadratic/smooth-quadratic SVG path import, relative/repeated coordinate handling, physical-tolerance adaptive flattening and a cancellable import-options dialog. The parser is now SvgPaths; the superseded linear-only parser was removed and existing consumers migrated. Curves are stored as editable polyline vertices, not native control handles. Expansion budgets are enforced per path and incrementally across the whole import.

Local Release build has zero warnings/errors; 193 tests pass (187 unit, 6 integration). Dense independent curve samples, control-history resets, collinear backtracking, coincident-endpoint loops, scaling, malformed/unsupported input, cancellation and expansion limits are tested. Desktop checked Cancel leaving zero objects, a 0.025 mm three-object import, output preview (387 segments / 799.3 mm / 44.0 s ideal) and one-step undo to the clean empty document. See [validation](../testing/milestone-13.md). Next: native curve geometry/handles, SVG arcs/transforms and continued controller execution qualification; physical streaming remains unimplemented.

## Milestone 0.12 - endpoint joining

Added Tools / Join open endpoints… for two same-layer open contours in one or two objects. The closest endpoint pair is selected deterministically; midpoint merging and cut-segment bridging have separate explanations and preview. Gap limits, layer/topology/format validation and source/selection checks prevent stale or invalid installation. Apply is one undo step; the result remains open and editable. A portable sample demonstrates a 0.447 mm gap.

Local Release build has zero warnings/errors; 170 tests pass (165 unit, 5 integration). Tests cover endpoint orientations, mirrors, gap boundaries, merge/bridge geometry and cut power, identity/order/layers, format limits, history and file/export/planning round-trips. Desktop checked rejection at 0.1 mm, accepted preview at 1 mm, bridge wording/apply, output preview and undo to the unchanged two-object fixture. See [validation](../testing/milestone-12.md). Physical output and full parity remain unfinished; next vector work is Bézier geometry, curve-aware node editing and broader SVG interoperability.

## Milestone 0.11 - independent trace regions and monitor cancellation

Added Tools / Separate selected paths and Combine selected paths. Contours become independently selectable objects that can be assigned to other vector layers; combining preserves geometry and requires a shared layer. Both are atomic undoable operations. Holes retain same-layer Fill behavior; combining does not join/weld endpoints. Source identity/order, mirrors, mixed-layer rejection, malformed/degenerate selections, native saves and history have automated coverage. Desktop separation/combination/undo and corrected simulator connection footer were checked.

Windows CI on 0.10 exposed a queued-query cancellation race that local tests had not reproduced: a query could acquire the gate during shutdown and throw ObjectDisposedException instead of cancellation. The monitor now distinguishes new calls after disconnect from already queued calls at the owning gate boundary, and disposal has one atomic initiator. Both status/settings queued orders run 16 times in regression coverage. Local build has zero warnings/errors; 158 tests pass (154 unit, 4 integration). See [validation](../testing/milestone-11.md). Physical USB and machine execution remain unverified/unimplemented, respectively. Next: endpoint joining, Bézier geometry and broader SVG interoperability.

## Milestone 0.10 - GRBL diagnostics

Added explicit read-only $$ settings retrieval with source-value preservation, report completion/duplicate/error/line-budget validation, and a desktop report showing $30/$32 alongside the profile S maximum. Added feed/spindle/buffer/pin/accessory status fields with explicit omission, serial-port refresh and a configurable 0–10000 ms serial startup delay (default 2000). Profile choice/editing is locked during connection; concurrent reads are disabled in the UI and serialized by the monitor. Serial write timeouts now surface as connection errors, and connection control changes refresh the footer state.

149 tests pass (145 unit, 4 integration), including an actual fragmented TCP exchange and cancellation of a settings read plus queued status query. Desktop simulator Connect, status display, read-only settings report and Disconnect were checked. Physical USB startup delay/reset behavior and hardware compatibility remain unverified. No physical device was opened and no motion/firing/configuration-write command was sent. See [validation](../testing/milestone-10.md). Job streaming, motion, arbitrary console commands and full parity remain outstanding.

## Milestone 0.9 - manual path cleanup

Added active node highlighting/numbering, double-click segment insertion, midpoint insertion, node deletion, contour removal and break/split at a node. Operations preserve identity/layer and participate in undo/native saves. Breaking a closed contour retains all segments and produces two separate endpoints at the break; open splits retain two contours in the same object. Invalid/minimum-node edits leave source geometry unchanged. Nodes-mode Delete edits nodes; whole-object deletion remains explicit through Edit/toolbar.

Interactive verification found and fixed selection clicks causing snapped node drift. Drags now have a movement threshold and preserve the grab offset. UI checked selection without mutation, keyboard insertion/deletion, double-click insertion, breaking via Tools, rejection of fill on open contours and undo back to the unchanged fixture. 131 tests pass (127 unit, 4 integration); see [validation](../testing/milestone-09.md). No machine execution was performed. Next gaps remain Bézier geometry, joining/separating paths and broader SVG interoperability.

## Milestone 0.8 - photograph tracing

2026-11-08. Added live photograph-oriented tracing sliders for brightness range, component area, Gaussian noise reduction, polyline corner smoothing and node optimization, plus local-contrast sketch mode, a 1024-sample ceiling, threshold-mask view and zoom/pan controls. Trace refinement retains delicate contours or the entire original trace when its checks cannot complete. Source bytes stay unchanged; separate-layer creation/source-output suppression remains one undo step.

Build succeeds with zero warnings/errors; 124 tests pass (120 unit, 4 integration), including a credited NASA photograph, uneven-illumination/noise fixtures and exhaustive small-mask refinement cases. UI checked live regeneration, sketch-mode controls, zoom, creation and undo. See [validation](../testing/milestone-08.md) and [reference distinctions](../research/photo-trace-reference.md).

REA now runs through the user's existing Docker container from the project launcher/MCP configuration. CLI and a real MCP smoke client successfully inspect the installed LightBurn PE resource inventory. Static inventory is not behavioral parity; deep Ghidra/IDA/Kuna providers are not available in this Docker configuration. Earlier host Ghidra evidence is retained separately.

Next: Bézier geometry/node workflows and wider SVG transforms/curves, then trace-region/curve fitting and further editing operations. Production work also requires bounded job streaming/recovery, physical controller qualification, camera/rotary/text/material features and packaging. Current physical machine execution remains unavailable. Historical milestone entries below describe their state at that time.

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

## Milestone 0.7 - image outline tracing

Added threshold/invert tracing, four-connected minimum-pixel-area filtering, faded grayscale/pink contour preview, optional vertex display, background cancellation and stale-result protection. Creates a separate editable compound-path layer with default Line settings and disables source-image output in the same undo step. Optional source removal affects only the project object. Pixel-edge boundaries preserve holes and mirror placement; only collinear points are simplified. No curve fitting, smoothing, centerline or sketch tracing yet. New original deterministic sample: samples/trace-outlines.lcraft, generated by scripts/make-trace-sample.py.

Build zero warnings/errors; 114 tests pass (111 unit, 3 native integration), including all 512 3x3 masks and PNG-to-trace-to-plan/export/native round-trip. UI verified 4 contours / 242 vertices / 3 speckles removed, source-output suppression, separate layer and one-step Undo. Trace plan: 246 segments, 628.8 mm cut, 35.7 s ideal. Release: artifacts/windows-x64-v0.7. Full feature parity and physical execution remain unfinished.
