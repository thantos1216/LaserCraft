# Controller interoperability baseline

Independent implementation uses the public GRBL interface specification: https://github.com/gnea/grbl/blob/master/doc/markdown/interface.md. Serial transport uses Microsoft System.IO.Ports: https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport.

The initial monitor sends only the identity query `$I\n` and real-time status query `?`. It accepts a version/build identity plus acknowledgement and parses state/position fields from bounded status frames. Unknown or missing position is not inferred. Commands that alter settings, reset/unlock/home, move, stream jobs, fire the laser or actuate air are not exposed by this monitor.

GRBL is the initial adapter target; grblHAL and FluidNC use its compatible read-only subset experimentally. Marlin, Smoothieware, Ruida and Trocen are recorded controller families with no adapter yet. Device profiles do not imply supported execution. Serial DTR/RTS are explicit, disabled by default, because opening/reset behavior differs by hardware. No physical controller has been contacted during development.

Tests use an in-process GRBL simulator and a TCP listener on loopback only. They verify exact commands, status parsing, endpoint validation, disposal/cancellation and profile persistence. They do not establish USB-driver, Wi-Fi, controller variant or Atezr behavior. Air requests are carried on planned cut segments and stored per layer/profile; there is no physical air command path.
