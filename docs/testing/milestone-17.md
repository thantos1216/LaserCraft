# Milestone 0.17 validation

2026-11-09. Editor arrangement operations, original implementation.

- Release tests: 252 unit + 8 integration pass. Checks cover row-major cell offsets, strict selections, retained originals, unique IDs, independent image-layer settings/pixels/mirrors, object/layer/image/encoded-data/path-vertex expansion checks, coordinates, zero-extent pitch, stable distribution anchors, both axes/modes, negative-gap rejection, rotation center/direction/holes/mirrors/ellipse sampling, raster rejection and reversible contour traversal.
- File integration: the portable original arrangement fixture loads, distributes, rotates, reverses and arrays; history Undo/Redo, native identity/topology, SVG import/export and offline G-code pass. A real PNG array decodes unchanged pixels, plans separate ordered image layers and retains power/air settings.
- Desktop: Ctrl+A fixture -> default 2 × 3 summary: +15 objects, pitch 125 × 15 mm, bounds 370 × 25 mm. Rows=100000 disabled Create with explicit limit error. Restored Rows=2 and created 18 selected objects. Preview: 90 segments / 1080.0 mm cut / 65.2 s ideal. Undo restored three objects.
- Desktop: Horizontal gaps visibly redistributed the fixture. Collective +90° rotation produced X75/Y45 and 10 × 120 mm bounds. Reversed resulting paths; Preview: 15 segments / 180.0 mm cut / 10.9 s ideal. Three Undo steps returned to the clean, unchanged source fixture. No file overwritten or physical endpoint opened.
- Packaging validation is pending the new self-contained 0.17 build. Automated/UI geometry checks do not establish physical precision, complete reference parity or production readiness. The previous serial connection workflow remains available for user testing independently of this editor work.
