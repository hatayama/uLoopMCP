/**
 * CLI commands for Device Agent tools.
 * Each tool is registered as a separate subcommand under the device- prefix.
 */

import { Command } from 'commander';
import { executeDeviceTool, DeviceToolGlobalOptions } from '../device/execute-device-tool.js';
import { DEVICE_AGENT_PORT } from '../device/device-constants.js';

function addDeviceGlobalOptions(cmd: Command): Command {
  return cmd
    .option('--token <token>', 'Auth token (or set ULOOP_DEVICE_TOKEN env var)')
    .option('-p, --port <port>', 'Device Agent port', String(DEVICE_AGENT_PORT));
}

export function registerDeviceToolCommands(program: Command): void {
  registerDeviceFindGameObjects(program);
  registerDeviceGetHierarchy(program);
  registerDeviceGetScreenshot(program);
  registerDeviceTapObject(program);
  registerDeviceTapCoordinate(program);
  registerDeviceSwipe(program);
  registerDeviceInputText(program);
  registerDeviceKeyInput(program);
  registerDevicePing(program);
}

function registerDevicePing(program: Command): void {
  const cmd: Command = program.command('device-ping').description('Health check for Device Agent');

  addDeviceGlobalOptions(cmd).action(async (options: DeviceToolGlobalOptions) => {
    await executeDeviceTool('ping', {}, options);
  });
}

interface DeviceFindOptions extends DeviceToolGlobalOptions {
  name?: string;
  tag?: string;
  componentType?: string;
  activeOnly?: string;
}

function registerDeviceFindGameObjects(program: Command): void {
  const cmd: Command = program
    .command('device-find-game-objects')
    .description('Find GameObjects on device by name, tag, or component');

  addDeviceGlobalOptions(cmd)
    .option('--name <name>', 'GameObject name to search')
    .option('--tag <tag>', 'Tag filter')
    .option('--component-type <type>', 'Component type filter')
    .option('--active-only <value>', 'Only active GameObjects', 'true')
    .action(async (options: DeviceFindOptions) => {
      const params: Record<string, unknown> = {};
      if (options.name) {
        params['name'] = options.name;
      }
      if (options.tag) {
        params['tag'] = options.tag;
      }
      if (options.componentType) {
        params['componentType'] = options.componentType;
      }
      if (options.activeOnly) {
        params['activeOnly'] = options.activeOnly === 'true';
      }
      await executeDeviceTool('find-game-objects', params, options);
    });
}

interface DeviceHierarchyOptions extends DeviceToolGlobalOptions {
  depth?: string;
  includeComponents?: string;
}

function registerDeviceGetHierarchy(program: Command): void {
  const cmd: Command = program
    .command('device-get-hierarchy')
    .description('Get scene hierarchy from device');

  addDeviceGlobalOptions(cmd)
    .option('--depth <n>', 'Max hierarchy depth', '10')
    .option('--include-components <value>', 'Include component names', 'false')
    .action(async (options: DeviceHierarchyOptions) => {
      const params: Record<string, unknown> = {};
      if (options.depth) {
        params['depth'] = parseInt(options.depth, 10);
      }
      if (options.includeComponents) {
        params['includeComponents'] = options.includeComponents === 'true';
      }
      await executeDeviceTool('get-hierarchy', params, options);
    });
}

interface DeviceScreenshotOptions extends DeviceToolGlobalOptions {
  format?: string;
  quality?: string;
  maxLongSide?: string;
}

function registerDeviceGetScreenshot(program: Command): void {
  const cmd: Command = program
    .command('device-get-screenshot')
    .description('Take a screenshot from device');

  addDeviceGlobalOptions(cmd)
    .option('--format <format>', 'Image format: png or jpg', 'png')
    .option('--quality <n>', 'JPEG quality (1-100)', '75')
    .option('--max-long-side <n>', 'Max long side in pixels', '1568')
    .action(async (options: DeviceScreenshotOptions) => {
      const params: Record<string, unknown> = {};
      if (options.format) {
        params['format'] = options.format;
      }
      if (options.quality) {
        params['quality'] = parseInt(options.quality, 10);
      }
      if (options.maxLongSide) {
        params['maxLongSide'] = parseInt(options.maxLongSide, 10);
      }
      await executeDeviceTool('get-screenshot', params, options);
    });
}

interface DeviceTapObjectOptions extends DeviceToolGlobalOptions {
  objectId?: string;
  objectPath?: string;
}

function registerDeviceTapObject(program: Command): void {
  const cmd: Command = program
    .command('device-tap-object')
    .description('Tap a GameObject on device by objectId or path');

  addDeviceGlobalOptions(cmd)
    .option('--object-id <id>', 'Object ID from find-game-objects')
    .option('--object-path <path>', 'Hierarchy path (e.g., Canvas/Button[0])')
    .action(async (options: DeviceTapObjectOptions) => {
      const params: Record<string, unknown> = {};
      if (options.objectId) {
        params['objectId'] = parseInt(options.objectId, 10);
      }
      if (options.objectPath) {
        params['objectPath'] = options.objectPath;
      }
      await executeDeviceTool('tap-object', params, options);
    });
}

interface DeviceTapCoordinateOptions extends DeviceToolGlobalOptions {
  x?: string;
  y?: string;
}

function registerDeviceTapCoordinate(program: Command): void {
  const cmd: Command = program
    .command('device-tap-coordinate')
    .description('Tap at normalized screen coordinates on device');

  addDeviceGlobalOptions(cmd)
    .option('--x <value>', 'Normalized X coordinate (0.0-1.0)')
    .option('--y <value>', 'Normalized Y coordinate (0.0-1.0)')
    .action(async (options: DeviceTapCoordinateOptions) => {
      const params: Record<string, unknown> = {};
      if (options.x) {
        params['x'] = parseFloat(options.x);
      }
      if (options.y) {
        params['y'] = parseFloat(options.y);
      }
      await executeDeviceTool('tap-coordinate', params, options);
    });
}

interface DeviceSwipeOptions extends DeviceToolGlobalOptions {
  startX?: string;
  startY?: string;
  endX?: string;
  endY?: string;
  durationMs?: string;
}

function registerDeviceSwipe(program: Command): void {
  const cmd: Command = program
    .command('device-swipe')
    .description('Perform a swipe gesture on device');

  addDeviceGlobalOptions(cmd)
    .option('--start-x <value>', 'Start X (0.0-1.0)')
    .option('--start-y <value>', 'Start Y (0.0-1.0)')
    .option('--end-x <value>', 'End X (0.0-1.0)')
    .option('--end-y <value>', 'End Y (0.0-1.0)')
    .option('--duration-ms <ms>', 'Swipe duration in milliseconds', '300')
    .action(async (options: DeviceSwipeOptions) => {
      const params: Record<string, unknown> = {};
      if (options.startX) {
        params['startX'] = parseFloat(options.startX);
      }
      if (options.startY) {
        params['startY'] = parseFloat(options.startY);
      }
      if (options.endX) {
        params['endX'] = parseFloat(options.endX);
      }
      if (options.endY) {
        params['endY'] = parseFloat(options.endY);
      }
      if (options.durationMs) {
        params['durationMs'] = parseInt(options.durationMs, 10);
      }
      await executeDeviceTool('swipe', params, options);
    });
}

interface DeviceInputTextOptions extends DeviceToolGlobalOptions {
  text?: string;
  targetObjectPath?: string;
}

function registerDeviceInputText(program: Command): void {
  const cmd: Command = program
    .command('device-input-text')
    .description('Input text to an InputField on device');

  addDeviceGlobalOptions(cmd)
    .option('--text <text>', 'Text to input')
    .option('--target-object-path <path>', 'Target InputField path (optional)')
    .action(async (options: DeviceInputTextOptions) => {
      const params: Record<string, unknown> = {};
      if (options.text) {
        params['text'] = options.text;
      }
      if (options.targetObjectPath) {
        params['targetObjectPath'] = options.targetObjectPath;
      }
      await executeDeviceTool('input-text', params, options);
    });
}

interface DeviceKeyInputOptions extends DeviceToolGlobalOptions {
  keyCode?: string;
}

function registerDeviceKeyInput(program: Command): void {
  const cmd: Command = program
    .command('device-key-input')
    .description('Send a key input on device');

  addDeviceGlobalOptions(cmd)
    .option('--key-code <key>', 'Unity KeyCode name (e.g., Return, Escape, Space)')
    .action(async (options: DeviceKeyInputOptions) => {
      const params: Record<string, unknown> = {};
      if (options.keyCode) {
        params['keyCode'] = options.keyCode;
      }
      await executeDeviceTool('key-input', params, options);
    });
}
