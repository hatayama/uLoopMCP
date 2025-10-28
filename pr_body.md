## Description

Fix the issue where uLoopMCP's TCP port increments by +1 after a Unity crash, causing unnecessary `.cursor/mcp.json` updates and Cursor reconnection popups.

### Root Cause

When Unity crashes and restarts, multiple recovery entry points (static constructor, domain reload handler, session service) fire simultaneously. This causes concurrent `StartServer` calls to detect the freshly-bound port as "in use" (due to OS TIME_WAIT or lingering cleanup), triggering fallback to the next port. The original port is never reused, and `mcp.json` is updated unnecessarily.

### Solution

1. **Unified Startup Entry Point**: Consolidate all recovery calls (Editor startup, domain reload, manual retry) through a single `StartRecoveryIfNeededAsync` method in `McpServerController`.
2. **5-second Port Reuse Retry**: Attempt to bind the original port for up to 5 seconds (250 ms intervals) using `TimerDelay.Wait()` before falling back to the next available port.
3. **Concurrent Recovery Guard**: 
   - Acquire a `SemaphoreSlim(1,1)` lock to prevent simultaneous startups.
   - Ignore recovery requests if a server is already running (on any port).
   - Dispose old server instances before binding a new one to prevent socket leaks.
4. **5-second Startup Protection Window**: After successful recovery, suppress additional start requests for 5 seconds to allow system stabilization.
5. **Session Cleanup on Failure**: Clear session and reconnecting flags when recovery ultimately fails, preventing infinite retry loops.
6. **Reconnecting Flag Management**: Clear reconnection UI flags on successful recovery and on session cleanup.

### Changes Made

- **McpServerController.cs**:
  - Added `StartRecoveryIfNeededAsync` as centralized recovery entry point.
  - Implemented `TryBindWithWaitAsync` for 5-second port reuse with `TimerDelay`.
  - Added startup protection window and semaphore-based concurrency control.
  - Modified `RestoreServerStateIfNeeded` to delegate to unified entry point.
  - Added explicit server disposal before binding to prevent socket leaks.
  - Clear session flags on recovery failure.
  - Clear reconnecting flags on recovery success.
  
- **SessionRecoveryService.cs**:
  - Simplified recovery paths to call `McpServerController.StartRecoveryIfNeededAsync`.
  - Removed duplicate retry logic; all recovery now flows through unified controller.

### Benefits

- ✓ Original port is reliably reused after crash (same `mcp.json`, no Cursor reconnection).
- ✓ Concurrent recovery calls are coalesced into a single operation via semaphore.
- ✓ No socket leaks or multiple server instances on different ports.
- ✓ Clean state management: session is cleared on recovery failure, preventing infinite retry loops.
- ✓ UI reconnecting flags are properly cleared, reflecting true server state.

### Verification

- Tested after Unity crash: port remains stable, `mcp.json` not updated.
- Vibelogs show `startup_request`, `binding_attempt`, `binding_success`, `server_start_ignored`, and `startup_protection_active` at appropriate times.
- No socket errors or multiple concurrent server instances.

### Related Issues

Fixes the +1 port increment issue on crash recovery and prevents socket leaks from concurrent recovery attempts.
