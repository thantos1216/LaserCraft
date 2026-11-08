# Milestone 0.11 validation

Local solution build succeeds with zero warnings/errors. All 158 tests pass (154 unit, 4 native integration). This milestone builds on the read-only GRBL diagnostics and adds compound path composition plus a CI-discovered lifecycle fix.

Seven composition regressions cover world-coordinate/mirror/layer preservation, first-object identity and new identities, source immutability, same-layer even-odd Fill holes and cut-length preservation, open contours with no new laser-on bridges, stable document order, mixed-layer/nonpath rejection, degenerate contour rejection, history/native save restoration and empty/stale/no-op selections. All edits are built and validated before installation. No new format version is needed.

Desktop Release checks: the compound fixture separates into two selected objects with independent bounding boxes; Combine restores a single compound outline without extra segments; Undo restores the unchanged original three-object project. No sample was saved over. Simulator Connect displays the corrected connected footer, and Disconnect restores the disconnected controls. The firmware settings report was checked in 0.10; no physical endpoint was opened.

## Windows CI failure and fix

[0.10 Windows run](https://github.com/thantos1216/LaserCraft/actions/runs/42528863248) failed the queued-status cancellation regression: expected OperationCanceledException, observed ObjectDisposedException in QueryStatusAsync. Local full tests had passed. The full job log is retained locally in ignored tmp/ci-10.log.

The producing boundary was the post-semaphore disposed check. Cancellation callbacks can release the active query's gate before a queued query's linked cancellation callback runs. A pending query could therefore acquire the gate during disposal and be classified as a new disposed call. Both query methods now check disposal at entry for new calls and treat closure observed after gate acquisition as cancellation. An atomic disposal marker selects one cleanup initiator. The regression tests both active/queued query orders 16 times, verifies queued commands are never sent, and separately verifies that calls made after completed disposal fail without sending commands. This fixes the owning behavior instead of accepting either exception in the test.

Still missing: endpoint joining, Bézier editing, weld/boolean operations, semantic photo segmentation and broad scale qualification. Physical serial startup/reset/cancellation and firmware variants remain unverified. No homing, motion, firing, air control, settings assignments or streaming API was added. Original public-protocol implementations and prior reference research remain distinct from proprietary parity claims.
