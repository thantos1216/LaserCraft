# Milestone 0.23 validation

2026-11-10. Local material recipes.

- Release suite: 351 unit + 12 integration tests pass. Tests cover process-only application preserving layer name/color/output/visibility/artwork/group identity, Undo/Redo, line/fill output planning, full raster settings, image/vector mismatch rejection, metadata/numeric/entry-count limits, duplicate IDs, native library round-trip, backup/invalid-save preservation, malformed JSON/null entries and 2 MB input bound.
- Recipes are immutable process settings, not machine profiles. No bundled material defaults, physical calibration or laser actions are implied. Existing GRBL control tests remain unchanged and pass.
- Desktop development publish: Material Library tab enabled; empty-state instructions, name/thickness/notes inputs and preset actions accessible by scrolling. Save with an empty name showed explicit validation without changing the clean project. No user library was saved/deleted during this UI check. Capture/apply/history/file persistence have automated coverage, not a complete desktop workflow check. Final backup-message/disabled-actions adjustments were compiled without a separate UI rerun.
- Import/export, categories/search, per-device recipe binding and automated material-test grids remain incomplete.
