# Milestone 0.2 verification

Build: dotnet build LaserCraft.slnx. Tests: dotnet test --no-build. 24 tests passed with zero failures. Latest clean build has zero warnings/errors; an intermediate build encountered debug-process file locks, resolved by stopping only the development instances created during this session.

New domain tests cover the 30-layer palette, SVG mm and 96-DPI pixel conversion, supported SVG round-trip, atomic rejection of unsupported elements/transforms, prohibited XML external entities, line perimeter and passes, output independent from visibility, off-bed rejection, even-odd fill holes, deterministic pause/resume/stop/completion, alignment/mirroring identity preservation, old layer default compatibility and invalid passes.

Interactive checks on original studio-demo.lcraft: successful project load (4 shapes, 3 layers); populated layer table and color palette; rendered path preview (590 segments, 17524.3 mm cut, 758.0 s ideal); clear nested fill hole with grey travel crossings; Running and Paused states and progress display; Ctrl+A selects all four objects; Ctrl+D duplicates the selection and invalidates the plan. Undo visually restored the original four shapes. Both dark and light compact layouts were checked. Numeric text and tab headers were resized after detecting clipping in the first visual pass.

Native file-picker automation could open the dialog but could not reliably target its filename control through the current Computer Use helper. Sample loading was verified through the added startup .lcraft argument. File-picker save/open interaction still needs manual regression coverage; no successful picker-selection test is claimed.

Remaining validation: measured rendering performance, UI tests for drag alignment and layer toggles, per-feature LightBurn behavioral equivalence, acceleration timing and any hardware protocol. G-code and hardware execution remain unavailable.
