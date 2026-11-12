# Milestone 0.32 validation

## Changes and scope

Added Arrange / Circular array, with total cell count, signed sweep, display-unit center X/Y and optional vector-cell rotation. Full circles exclude duplicate endpoints; partial arcs include both endpoints. Fixed-orientation cells orbit the combined selection center and preserve source geometry/text/mirrors. Rotated copies consume vector mirrors and become normalized paths, preserving contours, groups and layer assignments; originals remain unchanged. Raster rotation fails explicitly. Every copied image gets an independent layer with its original processing/cut settings. Array creation is one Undo step and selects originals plus copies.

Extracted the existing rectangular-array copy budget into a shared owner. Circular arrays check object/image/layer/embedded-data and expanded path-vertex budgets before transformation; coordinate checks run before copy creation. Live bounds are informational and off-bed results warn; output bounds validation remains unchanged. No overlap removal or new scheduling is implemented; output continues in layer order. The toolbar now derives its major/minor version from the assembly rather than a second hard-coded version string.

## Executed checks

454 solution tests pass: 428 unit, 26 integration. New coverage checks positive full circles, negative partial arcs, original preservation, unique identities, group remapping, mirrored compound contours, perimeter multiplication, image settings/asset preservation and separate layers, unsupported raster rotation, invalid parameters/selection, expanded vertices, coordinate limits, Undo/Redo and native project round-trip with matching toolpaths. Existing rectangular-array budget regressions remain passing.

Disconnected native Debug UI: drew a temporary 21 x 14 mm rectangle, selected it and opened Circular array. Defaults showed six cells/60 degrees; changing count to four immediately showed four cells/90 degrees and bounds 178 x 171 mm. Create produced four selected rectangles around center 215,215. One Undo restored the original; another restored the clean zero-object document. No physical port, motion or firing was invoked. Native raster/mixed-group and imperial-dialog scenarios were not manually exercised; dense-array responsiveness and broader layout qualification remain pending.

This original editing feature uses existing source geometry contracts and required no new REA binary investigation. No LightBurn code/assets were used, and equivalence to its complete array controls is not claimed. Existing hardware output behavior is unchanged; physical qualification of the package remains user work.

Solution build completed with zero warnings/errors. Portable: `LaserCraft-0.32.0-win-x64-portable.zip`, 75,645,466 bytes, SHA256 `70D2F7FDEC4A822A33F2BCBB198BE94092361FE3DA23025487011DAB07F09810`; clean source `b893b485f7f177561600b11b0001d54e1007907c`. Verified all 235 manifest entries by size/hash, Help version/circular-array instructions, icon, sample and self-contained runtime metadata. Copied to `F:/Movies2/Personal Built Applications/LaserCraft-0.32.0-win-x64-portable.zip` with matching SHA256. The disconnected QA window was confirmed closed after restoring its clean document. Physical qualification remains pending.
