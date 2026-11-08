# Milestone 0.8 validation

2026-11-08. Photograph-oriented trace preparation/refinement, live preview controls and Docker REA integration. No physical motion, firing or machine job execution was performed.

Local solution build: zero warnings/errors. All 124 tests pass (120 unit, 4 native integration). New tests cover exact brightness-range endpoints; blur/noise suppression and input immutability; sketch selection under uneven illumination; staircase node reduction; ring/hole preservation; collapse protection; invalid/cancelled work; 1024-sample frames; and contour-count/winding retention across every nonempty 3x3 binary pattern. Existing tracing, layer, planning, transport-monitor and G-code regressions remain passing.

The native integration decodes the credited, unmodified NASA JPEG through the real desktop image boundary and samples it at 512x512. With Cutoff 20, Threshold 160, noise 0.8, smoothness 0.5, optimize 0.7 and minimum area 8, it produces 255 contours and reduces 9824 vertices to 4465, retaining 13 delicate contours. Sketch mode also produces a nonempty trace. The test checks original encoded bytes, creates the separate output layer and plans vector-only output. This is correctness evidence for one photograph, not a throughput benchmark or material qualification.

Interactive Release UI checks on the NASA sample:

- Default live result: 512x512, 358 contours, 10530 to 8994 vertices, 176 small regions removed.
- Editing Cutoff from 0 to 20 immediately disables Create and automatically regenerates. At default refinement settings that detailed result reaches the work limit and clearly retains all 11458 original vertices.
- Sketch mode disables global range controls and enables local controls. It automatically produces 421 contours / 19314 vertices at the checked settings, with the explicit original-outline budget fallback.
- Wheel zoom reveals detail with contours aligned to the source. The scrollable controls and footer actions are visible; middle-drag panning was not independently exercised.
- Create adds a distinct vector layer and suppresses image output. One Ctrl+Z removes the trace layer, restores image output and returns to the single original image. No sample file was overwritten.

Docker CLI `inspect-pe-resources` and an actual MCP client smoke call both succeed against the staged installed LightBurn executable; MCP advertises 246 tools. This verifies connection and static resource inspection, not deep-provider availability or behavioral parity. See [REA evidence and limits](../research/rea-findings.md#docker-integration-08).

Remaining verification: wider photographs and high-resolution detail, rapid-change/close stress, preview panning, Bézier/centerline/region workflows, hardware/controller qualification and production packaging. The application remains an alpha; physical streaming and machine execution are not implemented.
