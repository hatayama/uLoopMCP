import { UnityToolManager } from '../../unity-tool-manager.js';
import { UnityClient } from '../../unity-client.js';

describe('UnityToolManager.refreshDynamicToolsSafe (integration-light)', () => {
  test('invokes notification callback once after tools initialization', async () => {
    const mockUnityClient = {
      connected: true,
      ensureConnected: jest.fn().mockResolvedValue(undefined),
      fetchToolDetailsFromUnity: jest.fn().mockResolvedValue([
        { name: 'compile', description: 'Compile project', parameterSchema: {} },
        { name: 'get-logs', description: 'Fetch logs', parameterSchema: {} },
      ]),
    } satisfies Pick<UnityClient, 'connected' | 'ensureConnected' | 'fetchToolDetailsFromUnity'>;

    const manager = new UnityToolManager(mockUnityClient as unknown as UnityClient);

    const notify = jest.fn();

    await manager.refreshDynamicToolsSafe(notify);

    expect(notify).toHaveBeenCalledTimes(1);
    expect(manager.getToolsCount()).toBe(2);
  });
});
