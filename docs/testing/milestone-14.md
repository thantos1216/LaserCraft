# Milestone 0.14 validation

2026-11-08. Original implementation; no proprietary code/assets reused.

- Release test run: 198 unit + 6 integration tests passed. Tests cover physical-profile refusal, jog bounds/invalid inputs with no partial change, off-before-jog transcript, downward power quantization, pulse duration/power limits, expiry/Stop, rejection while pulsing, bounded transcript, shared simulator status/disconnect shutdown, M7 export/M9 off and profile persistence/enum validation.
- Desktop 0.14: connected the built-in simulator; X+/Y+ produced X1/Y1. A 1% 100 ms simulated pulse showed S10 then automatically S0/M5. Read status showed MPos 1,1,0 and S0. Disconnect disabled manual controls. No hardware endpoint was opened.
- Photograph sample: default trace 358 contours, 10530 -> 8994 vertices. Numeric Cutoff 20 updated automatically; slider interaction also invalidated/recomputed without an Update button. Noise 0.8 / optimize 0.7 produced 255 contours, 9824 -> 4465 vertices with 13 delicate contours retained. Create was disabled during stale work and enabled on completion. Cancel retained the unchanged one-object sample. A high-detail intermediate setting explicitly retained original geometry when the refinement budget was reached.
- Slider keyboard increments now use each parameter's configured step rather than a universal unit increment; final compilation/tests cover that adjustment, with no separate post-adjustment keyboard UI run.
- Running VM LightBurn Pro Trial 2.2.01: inspected Move and five Device Settings tabs, then Cancel. No reference settings saved or device commands invoked. See the separate observation ledger.
- REA Docker providers queried successfully. Native macOS UI providers unavailable on Windows; VM UI evidence came from computer use, not REA capture. No target code extracted.

Not verified: physical USB startup/reset behavior, firmware compatibility, live command scheduling, real pulse shutdown or material output. Physical jogging/firing/streaming remain unavailable; this milestone is not approval for first cuts or production readiness.
