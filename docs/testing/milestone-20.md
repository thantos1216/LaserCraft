# Milestone 0.20 validation

2026-11-09. Flat artwork groups.

- Release build and suite: 303 unit + 11 integration tests pass. Focused cases cover mixed object types/layers/source metadata, hidden companions, Ctrl/Alt selection, stale IDs, group merges, unit alignment/distribution, independent array/copy IDs, path/text regeneration, empty-ID rejection, singleton groups, separate identity namespaces, native round-trip, history and unchanged planned segments.
- Desktop development publish: selected the three rectangles in arrangement-demo.lcraft; Ctrl+G created one cyan-bounded group. Cleared selection and clicked near one member's outline: all three selected. Dragged 10 mm right: combined X changed from 20 to 30 while width stayed 120. Ctrl+Shift+G removed grouping without moving artwork. Three Undo operations restored the clean original sample. No file was overwritten. Alt selection and copied group identities have automated coverage; no separate desktop verification was performed for those paths.
- This is an original metadata/editor implementation. No proprietary code/assets were extracted for this milestone. Existing REA/VM inventories remain reference evidence, not proof of complete behavioral parity.
- Nested hierarchy, group names/locks and SVG group preservation remain incomplete. Physical USB/controller behavior, guided hardware motion/fire and job streaming remain unqualified/incomplete. No physical laser commands were sent.
