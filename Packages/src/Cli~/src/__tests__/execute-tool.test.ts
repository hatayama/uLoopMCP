import {
  appendCliTimingsToDynamicCodeResult,
  isTransportDisconnectError,
} from '../execute-tool.js';
import { UnityNotRunningError } from '../port-resolver.js';
import { ProjectMismatchError } from '../project-validator.js';

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

  it('returns false for UnityNotRunningError', () => {
    expect(isTransportDisconnectError(new UnityNotRunningError('/project'))).toBe(false);
  });

  it('returns false for ProjectMismatchError', () => {
    expect(isTransportDisconnectError(new ProjectMismatchError('/a', '/b'))).toBe(false);
  });
});

describe('appendCliTimingsToDynamicCodeResult', () => {
  it('appends CLI total and overhead when RequestTotal is present', () => {
    const result: Record<string, unknown> = {
      Timings: ['[Perf] RequestTotal: 84.2ms'],
    };

    appendCliTimingsToDynamicCodeResult(result, 310.4, 415.9);

    expect(result['Timings']).toEqual([
      '[Perf] RequestTotal: 84.2ms',
      '[Perf] CliTotal: 310.4ms',
      '[Perf] CliProcessTotal: 415.9ms',
      '[Perf] CliBootstrap: 105.5ms',
      '[Perf] CliOverhead: 226.2ms',
    ]);
  });

  it('appends only CLI total when RequestTotal is missing', () => {
    const result: Record<string, unknown> = {
      Timings: ['[Perf] Backend: SharedRoslynWorker'],
    };

    appendCliTimingsToDynamicCodeResult(result, 180.0, 260.0);

    expect(result['Timings']).toEqual([
      '[Perf] Backend: SharedRoslynWorker',
      '[Perf] CliTotal: 180.0ms',
      '[Perf] CliProcessTotal: 260.0ms',
      '[Perf] CliBootstrap: 80.0ms',
    ]);
  });
});
