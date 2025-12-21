/**
 * Terminal spinner for showing loading state during async operations.
 */

// Array index access is safe here (modulo operation keeps it in bounds)
/* eslint-disable security/detect-object-injection */

const SPINNER_FRAMES = ['⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏'] as const;
const FRAME_INTERVAL_MS = 80;

export interface Spinner {
  update(message: string): void;
  stop(): void;
}

/**
 * Create a terminal spinner that displays a rotating animation with a message.
 * Returns a Spinner object with update() and stop() methods.
 */
export function createSpinner(initialMessage: string): Spinner {
  if (!process.stderr.isTTY) {
    return {
      update: (): void => {},
      stop: (): void => {},
    };
  }

  let frameIndex = 0;
  let currentMessage = initialMessage;

  const render = (): void => {
    const frame = SPINNER_FRAMES[frameIndex];
    process.stderr.write(`\r\x1b[K${frame} ${currentMessage}`);
    frameIndex = (frameIndex + 1) % SPINNER_FRAMES.length;
  };

  render();
  const intervalId = setInterval(render, FRAME_INTERVAL_MS);

  return {
    update(message: string): void {
      currentMessage = message;
    },
    stop(): void {
      clearInterval(intervalId);
      process.stderr.write('\r\x1b[K');
    },
  };
}
