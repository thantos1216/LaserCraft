# Milestone 0.33 validation

## Implementation

Image preparation previously required Update preview and allowed Apply before the displayed settings finished preparing. It now uses synchronized brightness/contrast/gamma/threshold sliders and numbers, automatic 150 ms debounce, per-request cancellation and one preparation gate. Obsolete results are disposed rather than published; displayed preparation is cleared on edits. Apply requires a successfully prepared matching request and the unchanged owning document/session. Numeric text and values are validated; fractional thresholds fail. Reset restores opening settings, rather than factory defaults. Close stops debounce, cancels pending work and awaits tracked jobs before disposing session resources.

ImagePreparationSettings owns validated raster/interval application by image identity, avoiding writes through a captured layer index. Source assets/artwork and unrelated process settings remain unchanged. The existing raster decoder/processor still produces output-resolution samples, with a reduced display bitmap and unchanged sample budgets. No new algorithms, sharpening, scan angle or hardware execution changes are included.

Materials adds case-insensitive all-word name/notes search, optional Vector/Image filter, stable sorted matches and matching/total count. Hidden selections cannot apply/replace through a stale list entry. Search does not modify library storage. Recipe capture now validates current layer editor values first. A failed library load retains its error and disabled writes when filters change.

## Executed checks and limits

464 solution tests pass (430 unit, 34 integration). Added real PNG sample tests for grayscale/Stucki/threshold settings application and output sample dimensions, unchanged source/other layer values, Undo/Redo, invalid interval/missing image, cancelled decoding and sample limits. Search tests cover multiple words across fields, case, whitespace, type combinations, duplicate names, stable ordering, original preservation and invalid type rejection. They do not establish UI request timing or all cancellation interleavings.

Native disconnected Debug UI using the supplied vector/image example: observed toolbar 0.33, paired tone sliders/numbers, current 40 x 40 preview and removal of Update preview. Brightness slider to 50 changed its number, disabled Apply while pending, then updated the image/current status and enabled Apply without another click. Reset restored opening brightness 0 and a current preview. Number entry 20 prepared automatically; Apply marked the project changed and Undo restored the clean original. Materials showed search/type controls and 0 of 0 presets; populated filtering is automated coverage only. No user library entries were created, changed or deleted, no sample file saved, and no physical port/motion/firing invoked.

Large-photo cancellation/rapid-slider stress, layout/DPI variants, invalid-text UI scenarios and physical material appearance remain unqualified. The new behavior is an original implementation over existing source contracts, so no new REA binary analysis was needed; no LightBurn code/assets were used. Full feature parity and production qualification are not claimed.
