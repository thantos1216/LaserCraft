# Milestone 0.7 validation

2026-11-08. Independent threshold pixel-edge tracing now creates closed editable compound paths from images. Local build/tests: zero warnings/errors; 114 tests pass (111 unit, 3 native integration). No machine commands were sent.

Domain tests exhaustively enumerate every 3x3 binary mask, independently checking polygon area against foreground pixel count and point-in-polygon classification at every pixel center. Separate cases verify four-corner rectangle simplification, hole preservation for line/fill, diagonal contacts, four-connected speckle removal, X/Y mirroring and physical mapping, input immutability, cancellation, invalid/oversized work, empty results, source replacement/movement rejection, separate layer creation, source output suppression, optional object removal and history restoration.

Native integration decodes the original generated PNG fixture, samples it, traces it, applies the vector result, plans only vector output, exports GRBL and round-trips the project while comparing embedded source bytes. Fixture generation uses only Python's standard library and original geometry, not proprietary imagery.

UI checks: default trace preview reports 256x192 samples, 4 contours, 242 vertices, 9409 retained foreground pixels and 3 speckles removed. Pink overlay includes the ring's hole, triangle and circle. Invert changes disable Create until Update trace recalculates. Create adds a separate Line vector layer, disables source-image output and selects editable nodes. The trace-only plan has 246 segments, 628.8 mm cut length and 35.7 s ideal duration. One Undo restores the clean one-image document and its original output setting; no sample was saved over.

Limits: pixel-edge polylines, exact collinear-point removal only; no smoothing/curve fitting/centerlines/sketch or LightBurn-equivalent parameter semantics. Sampling capped at 512 per axis; boundary/contour/vertex budgets reject excessive detail. Minimum area uses sampled foreground pixels with four-connectivity, not physical area. Outline result is a single compound object. More photographic/large-image performance benchmarks and rapid-close stress coverage remain needed.

REA inspect-managed-artifact also inspected the newly built Domain assembly and recorded observed static identity evidence in ignored tmp/trace-domain-07.json. This did not execute or decompile code and does not certify algorithm correctness or LightBurn parity; correctness evidence comes from the tests and UI checks above.
