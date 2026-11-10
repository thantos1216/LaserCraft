# Milestone 0.21 validation

2026-11-09. Directional bounds-box selection.

- Release build: zero warnings/errors. Suite: 307 unit + 11 integration tests pass. New cases cover containment versus overlap, inclusive boundaries, group bounds/hidden companions, individual override, additive selection/stale IDs, hidden standalone exclusion, zero-height paths and invalid numeric boxes.
- Desktop development publish: dragged a window around three sample rectangles (three selected), then around the first two (two selected, combined width 55), then a right-to-left crossing box overlapping part of the third (one selected, X110/width30). Title remained clean; no saved artwork/output settings changed. Modifier/hidden/degenerate cases have automated coverage only. Desktop publish preceded final version-label/help changes.
- Selection tests stored rectangular bounds, not exact contour intersections or opaque raster pixels. Minimum desktop drag is three screen pixels. No REA binary analysis was needed for this original editor operation; no proprietary code/assets were extracted and behavioral equivalence is not claimed.
- No physical laser commands were sent. Physical USB/controller testing, guided hardware motion/fire and job streaming remain unqualified/incomplete.

- Portable ZIP built from clean source commit 203fa54e050528f73102056592bcc82a4c28ecd4. All 233 archived manifest entries matched length/SHA-256; sourceDirty=false, version 0.21.0, self-contained win-x64, no shared framework requested. Final archive was not separately GUI-launched.
- Archive: artifacts/LaserCraft-0.21.0-win-x64-portable.zip; 75564974 bytes; SHA-256 30410AFFFC93DAF6683A06BC3BEBE180CCA5E9CDF240646612C0462EDFB29222. This verification-only update follows the packaged source commit.
