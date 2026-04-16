import {
  prewarmDynamicCodeAfterCompile,
  shouldPrewarmDynamicCodeAfterCompile,
} from '../../tools/dynamic-code-post-compile-warmup.js';
import { UnityClient } from '../../types/tool-types.js';

function createUnityClient(
  executeTool: jest.MockedFunction<UnityClient['executeTool']>,
): UnityClient {
  return {
    connected: true,
    executeTool,
  };
}

describe('shouldPrewarmDynamicCodeAfterCompile', () => {
  it('returns true when compile succeeded without errors', () => {
    expect(
      shouldPrewarmDynamicCodeAfterCompile({
        Success: true,
        ErrorCount: 0,
      }),
    ).toBe(true);
  });

  it('returns true for force-recompile responses that stay indeterminate across domain reload', () => {
    expect(
      shouldPrewarmDynamicCodeAfterCompile({
        Success: null,
        ErrorCount: 0,
        Message: 'Force compilation executed. Use get-logs tool to retrieve compilation messages.',
      }),
    ).toBe(true);
  });

  it('returns false for failed compile results', () => {
    expect(
      shouldPrewarmDynamicCodeAfterCompile({
        Success: false,
        ErrorCount: 0,
      }),
    ).toBe(false);
  });
});

describe('prewarmDynamicCodeAfterCompile', () => {
  it('runs three hidden execute-dynamic-code passes after compile', async () => {
    const executeTool = jest.fn().mockResolvedValue({ Success: true });

    await expect(
      prewarmDynamicCodeAfterCompile(createUnityClient(executeTool), async (): Promise<void> => {}),
    ).resolves.toBeUndefined();

    expect(executeTool).toHaveBeenCalledTimes(3);
    expect(executeTool).toHaveBeenNthCalledWith(1, 'execute-dynamic-code', {
      Code: 'using UnityEngine; bool previous = Debug.unityLogger.logEnabled; Debug.unityLogger.logEnabled = false; try { Debug.Log("Unity CLI Loop dynamic code prewarm"); return "Unity CLI Loop dynamic code prewarm"; } finally { Debug.unityLogger.logEnabled = previous; }',
      CompileOnly: false,
      YieldToForegroundRequests: true,
    });
  });

  it('retries transient warmup failures until the current pass succeeds', async () => {
    const executeTool = jest
      .fn()
      .mockResolvedValueOnce({
        Success: false,
        ErrorMessage: 'Another execution is already in progress',
      })
      .mockResolvedValue({ Success: true });

    await expect(
      prewarmDynamicCodeAfterCompile(createUnityClient(executeTool), async (): Promise<void> => {}),
    ).resolves.toBeUndefined();

    expect(executeTool).toHaveBeenCalledTimes(4);
  });

  it('throws when warmup keeps failing with a non-retryable error', async () => {
    const executeTool = jest.fn().mockResolvedValue({
      Success: false,
      ErrorMessage: 'Compilation failed permanently',
    });

    await expect(
      prewarmDynamicCodeAfterCompile(createUnityClient(executeTool), async (): Promise<void> => {}),
    ).rejects.toThrow('Compilation failed permanently');
  });
});
