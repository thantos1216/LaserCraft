# Milestone 0.4 validation

Build and test commands remain dotnet build/test LaserCraft.slnx. Final build has zero warnings/errors; 65 tests pass (63 unit, 2 native integration). A separate IntegrationTests project references the desktop decoder without launching a UI and uses the original PNG fixture.

Domain fixtures verify black/white threshold mapping, fully transparent pixels under inversion for every algorithm, deterministic Floyd-Steinberg diffusion, Bayer half-tone density, gamma and input immutability, invalid/cancelled processing, bidirectional/unidirectional scan direction, mirror mapping, proportional power, repeated passes, off-power overscan/bed bounds, skipped white rows, settings persistence and rejection of excessive resolution before decoding. Existing layer, vector and device regression tests remain active.

Native integration verifies PNG import, sampling, grayscale processing and complete toolpath generation while retaining original bytes. Invalid encoded data is rejected. It does not initialize a GUI or contact hardware.

Interactive checks show original and prepared Floyd-Steinberg samples side by side. The raster demo produced 13675 mixed raster/vector segments, 11509.0 mm enabled path length and 774.3 s ideal time. Release playback advanced while Running, paused successfully, and Stop reset progress to zero. Native source colors remain visible behind the path overlay as editing context. Image dialog preview is the monochrome sample representation. No physical engraving result or machine compatibility is claimed.

Limits: high-density photos can exceed the current sample/work/200000-segment budgets; complete data still controls playback while dense viewport drawings are sampled. Extra algorithms, high-bit-depth processing, material calibration, scan angles, acceleration, tiling and physical output need further work. Background stale-result/cancellation handling was implemented; repeated rapid-edit/close UI stress coverage remains pending.
