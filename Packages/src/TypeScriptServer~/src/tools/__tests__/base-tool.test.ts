import { BaseTool } from '../base-tool';
import { ToolContext } from '../../types/tool-types.js';

// Explicitly import Jest types
import { describe, it, expect } from '@jest/globals';

class DummyTool extends BaseTool {
  readonly name = 'dummy';
  readonly description = 'dummy tool';
  readonly inputSchema = {};
  protected validateArgs(_args: unknown): Record<string, unknown> {
    return {};
  }
  protected execute(_args: Record<string, unknown>): Promise<string> {
    return Promise.resolve('ok');
  }
}

describe('BaseTool', (): void => {
  it('should format response as text', async (): Promise<void> => {
    const context: ToolContext = { unityClient: { executeTool: jest.fn() } };
    const tool = new DummyTool(context);
    const res = await tool.handle({});
    expect(res.content[0].type).toBe('text');
    expect(res.content[0].text).toBe('ok');
  });

  it('should format error response if execute throws', async (): Promise<void> => {
    class ErrorTool extends DummyTool {
      protected execute(_args: Record<string, unknown>): Promise<string> {
        return Promise.reject(new Error('fail!'));
      }
    }
    const context: ToolContext = { unityClient: { executeTool: jest.fn() } };
    const tool = new ErrorTool(context);
    const res = await tool.handle({});
    expect(res.content[0].text).toContain('Error in dummy: fail!');
  });
});
