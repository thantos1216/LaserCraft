# User-supplied Attzr observations

2026-11-10. Supplied screenshots show LaserCraft 0.14, not current 0.24. Firmware identity is displayed as [VER:1.1h.2022071501:] and Idle, MPos 0,0,0, FS0,0. Move is correctly simulator-only in that older build. These are user observations; the developer did not open the physical endpoint.

The file Attzr Firmware setting and results.txt contains 48 annotated setting entries. SHA-256: 5FF2190719B3F11F93C8ED6DB96DE5A88303870B735C31491D88C252D2AD0224. Selected reported values: $13=0, $20=0, $21=1, $22=1, $30=1000.000, $31=0.000, $32=1, $130=430.000, $131=430.000. Raw transport framing is not present in this annotated text. Tests use these decimal power values, not assumed integer formatting.

Supported conclusion: that user's older build completed GRBL query exchanges. Unknown: current COM/baud, whether homing occurred, physical direction/work zero, current clearance and optical/cut behavior. No firmware configuration was changed. Homing enabled and a displayed zero do not establish safe bounds; soft limits were reported off. User intends to test standard 5 mm plywood scraps, also has 3 mm. No material recipe or through-cut settings are claimed.
