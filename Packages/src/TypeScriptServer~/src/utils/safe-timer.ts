/**
 * Safe Timer Utility - Prevents orphaned processes by ensuring automatic cleanup
 *
 * Design document reference: Packages/src/TypeScriptServer~/ARCHITECTURE.md
 *
 * Related classes:
 * - UnityClient: Uses safeSetTimeout function from this utility
 * - UnityDiscovery: Could potentially use this for safer timer management
 * - UnityEventHandler: Handles cleanup scenarios this utility addresses
 *
 * This class provides a foolproof way to manage timers that automatically
 * clean themselves up when the object is destroyed or when the process exits.
 *
 * Usage:
 *   const timer = new SafeTimer(() => console.log('tick'), 1000);
 *   // Timer automatically cleans up when timer goes out of scope
 *   // or when process exits
 */

export class SafeTimer {
  private static activeTimers = new Set<SafeTimer>();
  private static cleanupHandlersInstalled = false;

  private timerId: NodeJS.Timeout | null = null;
  private isActive = false;
  private callback: () => void;
  private delay: number;
  private isInterval: boolean;

  constructor(callback: () => void, delay: number, isInterval = false) {
    this.callback = callback;
    this.delay = delay;
    this.isInterval = isInterval;

    // Install global cleanup handlers on first timer creation
    SafeTimer.installCleanupHandlers();

    this.start();
  }

  /**
   * Start the timer
   */
  private start(): void {
    if (this.isActive) {
      return;
    }

    if (this.isInterval) {
      this.timerId = setInterval(this.callback, this.delay);
    } else {
      this.timerId = setTimeout(this.callback, this.delay);
    }

    this.isActive = true;
    SafeTimer.activeTimers.add(this);
  }

  /**
   * Stop and clean up the timer
   */
  stop(): void {
    if (!this.isActive || !this.timerId) {
      return;
    }

    if (this.isInterval) {
      clearInterval(this.timerId);
    } else {
      clearTimeout(this.timerId);
    }

    this.timerId = null;
    this.isActive = false;
    SafeTimer.activeTimers.delete(this);
  }

  /**
   * Check if timer is currently active
   */
  get active(): boolean {
    return this.isActive;
  }

  /**
   * Install global cleanup handlers to ensure all timers are cleaned up
   */
  private static installCleanupHandlers(): void {
    if (SafeTimer.cleanupHandlersInstalled) {
      return;
    }

    // Clean up all active timers when process exits
    const cleanup = (): void => {
      // console.log(`[SafeTimer] Cleaning up ${SafeTimer.activeTimers.size} active timers`);
      SafeTimer.cleanupAll();
    };

    process.on('exit', cleanup);
    process.on('SIGINT', cleanup);
    process.on('SIGTERM', cleanup);
    process.on('SIGHUP', cleanup);
    process.on('uncaughtException', cleanup);
    process.on('unhandledRejection', cleanup);

    SafeTimer.cleanupHandlersInstalled = true;
  }

  /**
   * Clean up all active timers
   */
  private static cleanupAll(): void {
    for (const timer of SafeTimer.activeTimers) {
      timer.stop();
    }
    SafeTimer.activeTimers.clear();
  }

  /**
   * Get count of active timers (for debugging)
   */
  static getActiveTimerCount(): number {
    return SafeTimer.activeTimers.size;
  }
}

/**
 * Convenience functions for common timer patterns
 */

/**
 * Create a safe setTimeout that automatically cleans up
 */
export function safeSetTimeout(callback: () => void, delay: number): SafeTimer {
  return new SafeTimer(callback, delay, false);
}

/**
 * Create a safe setInterval that automatically cleans up
 */
export function safeSetInterval(callback: () => void, delay: number): SafeTimer {
  return new SafeTimer(callback, delay, true);
}

/**
 * Stop and clean up a SafeTimer instance safely
 */
export function stopSafeTimer(timer: SafeTimer | null | undefined): void {
  if (!timer) {
    return;
  }

  timer.stop();
}

/**
 * Promise-based delay with automatic cleanup
 */
export function safeDelay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    new SafeTimer(resolve, ms, false);
  });
}
