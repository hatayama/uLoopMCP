import { isTransportDisconnectError } from '../execute-tool.js';

describe('isTransportDisconnectError', () => {
  it('returns true for UNITY_NO_RESPONSE', () => {
    expect(isTransportDisconnectError(new Error('UNITY_NO_RESPONSE'))).toBe(true);
  });

  it('returns true for Connection lost with details', () => {
    expect(isTransportDisconnectError(new Error('Connection lost: read ECONNRESET'))).toBe(true);
  });

  it('returns true for Connection lost with EPIPE', () => {
    expect(isTransportDisconnectError(new Error('Connection lost: write EPIPE'))).toBe(true);
  });

  it('returns false for JSON-RPC error from Unity', () => {
    expect(isTransportDisconnectError(new Error('Unity error: compilation failed'))).toBe(false);
  });

  it('returns false for connection refused (pre-dispatch error)', () => {
    expect(isTransportDisconnectError(new Error('connect ECONNREFUSED 127.0.0.1:8711'))).toBe(
      false,
    );
  });

  it('returns false for non-Error values', () => {
    expect(isTransportDisconnectError('UNITY_NO_RESPONSE')).toBe(false);
    expect(isTransportDisconnectError(null)).toBe(false);
    expect(isTransportDisconnectError(undefined)).toBe(false);
  });
});
