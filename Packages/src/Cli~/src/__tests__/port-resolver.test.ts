import { resolvePortFromUnitySettings } from '../port-resolver.js';

describe('resolvePortFromUnitySettings', () => {
  it('returns serverPort when server is running and serverPort is valid', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: true,
      serverPort: 8711,
      customPort: 8700,
    });

    expect(port).toBe(8711);
  });

  it('returns customPort when server is not running', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: false,
      serverPort: 8711,
      customPort: 8700,
    });

    expect(port).toBe(8700);
  });

  it('returns customPort when serverPort is invalid', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: false,
      serverPort: 0,
      customPort: 8711,
    });

    expect(port).toBe(8711);
  });

  it('falls back to serverPort when customPort is invalid', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: false,
      serverPort: 8711,
      customPort: 0,
    });

    expect(port).toBe(8711);
  });

  it('returns null when both ports are invalid', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: false,
      serverPort: 0,
      customPort: 0,
    });

    expect(port).toBeNull();
  });

  it('returns null when port is not an integer', () => {
    const port = resolvePortFromUnitySettings({
      isServerRunning: true,
      serverPort: 8711.5,
      customPort: 8700.1,
    });

    expect(port).toBeNull();
  });
});
