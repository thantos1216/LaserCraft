# Milestone 0.36 validation

## Changes

The user reports disappearing image previews during slider edits. Changed explicitly cleared and disposed the displayed bitmap before the debounce/background work. It now invalidates readiness and cancels obsolete work while retaining the owned display bitmap, replacing it only when a current result is ready. Failed/cancelled preparations cannot enable Apply or publish an obsolete frame. Final close still clears/disposes owned images after cancellation. Text preview had the same visible-clearing pattern; it now retains its last completed shape with current-only Create/Update. Status wording distinguishes current and previous previews.

Native inspection also showed controls moving as pending text changed the footer height. Reserved image/text status space keeps ordinary pending/result messages stable. Projects now initially select the first populated/explicit draft layer rather than displaying a hidden fallback layer's settings. The implementation does not change raster settings, output resolution or physical execution.

Continued the prior requested grid/grouping work with a real .lcraft save/load regression: a grouped 2 x 3 Fill test keeps independent layer settings and group IDs, selecting one cell selects all six, collective translation preserves spacing, Ungroup retains positions/settings, and planned length/power remain correct.

## Verification

474 Release tests passed (435 unit, 39 integration); solution build zero warnings/errors. The image display fix is verified through native UI rather than a test that merely asserts its implementation. Existing raster, text, cleanup, fill, grid, grouping and device tests remain included.

Disconnected native Release build: opened an isolated Blue Marble QA copy at 5000 x 5000 requested output samples. Observed the previous right-hand grayscale image still visible with pending status and Apply disabled after a brightness-slider change, then the brightened result with Apply enabled. Repeated after reserving footer space: before/pending/finished snapshots retained both image frames and slider/control positions. Cancel left the sample clean; the isolated QA app was closed. Verified initial settings select the visible image layer on reopen. No user project was saved and no physical connection, motion or firing was invoked. Continuous video/frame-rate measurement and broad DPI/small-window qualification were not performed. The text retention path was source-reviewed; its new native pending-frame behavior was not separately exercised.

The 0.35 material-test generation, cleanup, complex Fill, text editing and sizing fixes remain included. This source-level UI correction required no new REA target analysis or proprietary assets/code. Physical qualification and full feature parity remain unfinished.
