# Milestone 0.16 validation

2026-11-09. Transferable Windows testing and connection diagnostics, original implementation.

- Release build: zero warnings/errors. 226 unit + 6 integration tests pass. Journal regressions cover exact TX/RX ordering, simulator ownership, write exceptions, rejected identity responses, active-read cancellation, one close, detached snapshots, truncation/drop accounting and escaped control characters. Status alarms now fail immediately with source text retained.
- Desktop: Save connection log before Connect reports no attempt; simulator Connect/Disconnect followed by export produced tmp/connection-ui-016.txt. File includes version/profile facts, escaped $I newline, identity/ok/status replies and transport closure. Reopened export dialog and cancelled; disconnected state and saved-log success message remained. No physical endpoint was opened.
- Self-contained ZIP packaging is implemented with PowerShell 7, .NET runtime/native dependencies, Help, USB test instructions, build/source identity and SHA-256 file manifest. Archive/extraction/launch verification is pending the final package build; it is not established by passing source tests.

No physical USB-driver, reset/startup, baud, controller identity or burn test has been performed. A server-hosted folder does not forward USB COM. Execution scheduling, physical jog/fire/shutdown and cuts remain unavailable. Existing REA/VM inventory remains the reference control audit; these packaging/diagnostic changes did not require additional proprietary analysis.
