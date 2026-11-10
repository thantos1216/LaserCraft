# Milestone 0.23 validation

2026-11-10. Local material recipes.

- Release suite: 351 unit + 12 integration tests pass. Tests cover process-only application preserving layer name/color/output/visibility/artwork/group identity, Undo/Redo, line/fill output planning, full raster settings, image/vector mismatch rejection, metadata/numeric/entry-count limits, duplicate IDs, native library round-trip, backup/invalid-save preservation, malformed JSON/null entries and 2 MB input bound.
- Recipes are immutable process settings, not machine profiles. No bundled material defaults, physical calibration or laser actions are implied. Existing GRBL control tests remain unchanged and pass.
- Desktop development publish: Material Library tab enabled; empty-state instructions, name/thickness/notes inputs and preset actions accessible by scrolling. Save with an empty name showed explicit validation without changing the clean project. No user library was saved/deleted during this UI check. Capture/apply/history/file persistence have automated coverage, not a complete desktop workflow check. Final backup-message/disabled-actions adjustments were compiled without a separate UI rerun.
- Import/export, categories/search, per-device recipe binding and automated material-test grids remain incomplete.

- Portable ZIP built from clean source commit 19da4a77f07eb54a335e12ed5c9d970452916fff. All 233 archived manifest entries matched length/SHA-256; sourceDirty=false, version 0.23.0, self-contained win-x64, no shared framework requested and updated Help included. Final archive was not separately GUI-launched; desktop used the development publish.
- Archive: artifacts/LaserCraft-0.23.0-win-x64-portable.zip; 75583671 bytes; SHA-256 BC68664325A3E5F509128B067FE15A3823CAA75954202F4A3F208CB400B8D240. This verification-only update follows the packaged source commit.
