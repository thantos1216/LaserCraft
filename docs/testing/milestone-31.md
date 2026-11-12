# Milestone 0.31 validation

## Scope and implementation

User reports that cuts, saved/reloaded layer settings and connections work. The supplied 0.30 screenshots show connection/report and playback clutter occupying the sidebar, and Preview replacing the editor. This milestone changes workspace/output presentation; it does not establish complete LightBurn parity or production qualification.

Connection profile/COM, Connect/Disconnect, status/settings/log and full Details are now in a top-right header box. The report is labelled as a last query snapshot. Cuts / Layers, Console, Move and Materials share one sidebar above compact physical job controls. Embedded preview playback and duplicated connection text were removed. The sidebar has a scroll fallback for insufficient space. Disconnected physical controls are initialized after layout creation, preventing initially enabled Run/Home buttons.

Output scope explicitly defaults to All enabled artwork. Selected objects only requires a nonempty selection. The same selection resolver serves Preview/export/Run/frame; relative placement still uses output bounds. Independent preview preparation copies the document's shape/layer lists and runs the existing vector/raster planner under a bounded preparation gate. Document/scope changes discard stale preparation. Export no longer opens or changes Preview. Existing successful-job return, pass scheduling and hardware protocol ownership are unchanged.

Preview opens an owned read-only snapshot window with Play/resume, Pause, Restart, Show all, seek, speed, travel, fit and Back to Design/Escape. It uses the processed raster scan plan and power-weighted black marks. All planned segments are drawn up to the existing 200000-segment planner bound; dense playback refreshes at 5 fps. No machine transport/control is reachable through this window. The main document remains editable; reopen Preview to regenerate a changed snapshot. Disconnected frames use their own preview window; connected frames remain laser-off hardware operations. Ideal timing excludes acceleration, materials and the live captured-head return.

## Executed verification

Solution tests: 444 passing (418 unit, 26 integration). Solution build: zero warnings/errors. New integration coverage checks all/selected scope and empty-selection rejection, the real embedded PNG/vector sample against the output planner without source mutation, and seek/pause/resume/restart/clamping semantics. Existing physical protocol, repeat/pass and return regressions remain passing.

Disconnected native Debug UI at the normal 1480 x 940 window: observed the header connection box, full visible sidebar/job controls without vertical scrolling, and disabled disconnected Run/Home/Abort. Drew a temporary rectangle and opened a separate preview: playback ran; numeric speed displayed 10; seeking paused at 2.0 seconds; Back to Design restored the original editor with the rectangle intact. Undid the temporary drawing to the clean document and closed the QA window. No physical port, motion or laser firing was invoked.

The attempted outer-corner automation drag did not resize the window. Small-window scroll behavior and DPI/multimonitor layouts remain manually unverified. Dense-photo UI performance and raster burn appearance require broader qualification; no performance measurement is claimed. User-reported hardware success belongs to earlier builds, not this package. This ordinary source/UI refactor required no new REA target analysis, and uses no LightBurn code/assets.
