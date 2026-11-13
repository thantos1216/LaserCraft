# Milestone 0.34 validation

## Root cause and fix

The user screenshot shows a color source, empty prepared pane and the explicit 16000-axis / 4-million output sample rejection. Image editing called the full-resolution EngravingGrid, so large photos never reached rendering and parameter changes also failed. This was a resolution-boundary problem, not evidence that the new sliders had no events.

Separated display sampling from output sampling at RasterImages. Both use the same RGBA decoder and tone/dither processor; output keeps its existing limits and exact requested interval. Edit preview uses an aspect-preserving grid bounded to 800 samples per axis, provides original grayscale, adjusted grayscale and selected laser-format samples, and reports the requested output dimensions/support status. The editor defaults to adjusted grayscale; Show laser format is a temporary display toggle. Tone controls update both adjusted and selected-mode preparations; source pixels/settings remain unchanged until Apply. Output warnings do not block saving edits, but Run/export still reject unsupported output rather than silently coarsening it. Reduced dithering is a display approximation, not exact full-resolution dots or a material burn model.

## Executed verification

467 solution tests pass: 430 unit, 37 integration. New NASA photo regressions verify that 4000 x 4000 requested output previews at 800 x 800, the screenshot's brightness/contrast/gamma changes alter grayscale tones while the baseline stays identical, Stucki output is binary, requested dimensions are preserved and real output still rejects the oversize request. Additional tests cover exact small-preview/output sample equality, rectangular aspect ratio, grayscale mode sharing and cancellation. Build completed with zero warnings/errors.

Disconnected native Debug UI used a separate QA copy of the Blue Marble project with a 500 mm image / 0.1 mm interval (5000 x 5000 requested samples). Original and adjusted views rendered grayscale; moving brightness from 0 to 55 visibly brightened the right image automatically while the reference remained unchanged. Show laser format rendered Stucki dots automatically. Both displayed the oversize-output warning and enabled editing Apply after current preparation. Cancel left the QA project clean; no source project was saved. The user's separate portable 0.33 windows were left untouched. No physical endpoint, movement or firing was invoked.

No new REA binary investigation was required for this source-level decoder/presentation fix. Full-resolution large-photo output tiling, exact high-resolution dot inspection, material appearance and broad UI/performance qualification remain unfinished.

Portable: `LaserCraft-0.34.0-win-x64-portable.zip`, 75,651,931 bytes, SHA256 `496C390649226D3EABE65296E3BAA0E833A1A51691F8261506AE8FB28E2466CD`; clean source `f164acf837430a83b54ada39213d356df5e30d8c`. Verified all 235 manifest entries by size/hash, Help version/grayscale/laser-format/bounded-display instructions, icon, sample and self-contained runtime metadata. Copied to `F:/Movies2/Personal Built Applications/LaserCraft-0.34.0-win-x64-portable.zip` with matching SHA256. Isolated QA application confirmed closed; user portable windows remained untouched. Physical qualification remains pending.
