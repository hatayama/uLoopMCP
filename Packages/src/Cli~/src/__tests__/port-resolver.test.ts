import { tmpdir } from 'os';
import {
  resolvePortFromUnitySettings,
  validateProjectPath,
  resolveUnityPort,
} from '../port-resolver.js';

describe('resolvePortFromUnitySettings', () => {
  it('returns customPort when valid', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: true,
      customPort: 8700,
    });

    expect(port).toBe(8700);
  });

  it('returns customPort regardless of isServerRunning flag', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: false,
      customPort: 8711,
    });

    expect(port).toBe(8711);
  });

  it('returns null when customPort is invalid', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: true,
      customPort: 0,
    });

    expect(port).toBeNull();
  });

  it('returns null when customPort is missing', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: true,
    });

    expect(port).toBeNull();
  });

  it('returns null when port is not an integer', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: true,
      customPort: 8700.1,
    });

    expect(port).toBeNull();
  });
});

describe('validateProjectPath', () => {
  it('throws when path does not exist', () => {
    expect(() => validateProjectPath('/nonexistent/path/to/project')).toThrow(
      'Path does not exist: /nonexistent/path/to/project',
    );
  });

  it('throws when path is not a Unity project', () => {
    expect(() => validateProjectPath(tmpdir())).toThrow('Not a Unity project');
  });
});

describe('resolveUnityPort', () => {
  it('throws when both port and projectPath are specified', async () => {
    await expect(resolveUnityPort(8700, '/some/path')).rejects.toThrow(
      'Cannot specify both --port and --project-path',
    );
  });

  it('returns explicit port when only port is specified', async () => {
    const port = await resolveUnityPort(8711);
    expect(port).toBe(8711);
  });
});
