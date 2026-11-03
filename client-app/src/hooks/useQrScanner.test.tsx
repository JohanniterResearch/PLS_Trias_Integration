import { StrictMode } from 'react';
import { act, cleanup, renderHook, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useQrScanner } from './useQrScanner';
import { classifyCameraError } from './cameraError';
import { getQrScanBox } from './qrScannerLayout';

const { instances, startMock } = vi.hoisted(() => ({
  instances: [] as { isScanning: boolean; stop: ReturnType<typeof vi.fn>; clear: ReturnType<typeof vi.fn> }[],
  startMock: vi.fn(),
}));
vi.mock('html5-qrcode', () => ({
  Html5Qrcode: class {
    isScanning = false;
    stop = vi.fn(async () => { this.isScanning = false; });
    clear = vi.fn();
    constructor() { instances.push(this); }
    async start(...args: unknown[]) {
      await startMock(...args);
      this.isScanning = true;
    }
  },
}));

function deferred() {
  let resolve!: () => void;
  const promise = new Promise<void>((done) => { resolve = done; });
  return { promise, resolve };
}

beforeEach(() => {
  instances.length = 0;
  startMock.mockReset().mockResolvedValue(undefined);
  vi.stubGlobal('isSecureContext', true);
  Object.defineProperty(navigator, 'mediaDevices', {
    configurable: true, value: { getUserMedia: vi.fn() },
  });
});
afterEach(async () => {
  cleanup();
  await act(async () => {});
  vi.unstubAllGlobals();
});

const options = () => ({ elementId: 'reader', onSuccess: vi.fn() });

describe('camera error classification', () => {
  it.each([
    ['NotAllowedError', 'permission'],
    ['SecurityError', 'permission'],
    ['NotFoundError', 'not-found'],
    ['OverconstrainedError', 'not-found'],
    ['NotReadableError', 'unavailable'],
    ['AbortError', 'unavailable'],
    ['TypeError', 'unknown'],
  ] as const)('handles %s as object and library string', (name, expected) => {
    expect(classifyCameraError(new DOMException('test', name))).toBe(expected);
    expect(classifyCameraError(`Error getting userMedia, error = ${name}: test`)).toBe(expected);
  });
  it('does not infer a denied permission from arbitrary prose', () => {
    expect(classifyCameraError('Unexpected error mentioning NotAllowedError')).toBe('unknown');
    expect(classifyCameraError(null)).toBe('unknown');
  });
});

describe('useQrScanner', () => {
  it('checks insecure context before browser APIs', async () => {
    vi.stubGlobal('isSecureContext', false);
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: undefined });
    const { result } = renderHook(() => useQrScanner(options()));
    await waitFor(() => expect(result.current.cameraError).toBe('insecure'));
    expect(startMock).not.toHaveBeenCalled();
  });

  it('reports missing APIs as unsupported, not a missing camera', async () => {
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: {} });
    const { result } = renderHook(() => useQrScanner(options()));
    await waitFor(() => expect(result.current.cameraError).toBe('unsupported'));
    expect(startMock).not.toHaveBeenCalled();
  });

  it('starts in a secure context and stops and clears on unmount', async () => {
    const { result, unmount } = renderHook(() => useQrScanner(options()));
    await waitFor(() => expect(result.current.isStarting).toBe(false));
    expect(result.current.cameraError).toBeNull();
    expect(startMock).toHaveBeenCalledWith(
      { facingMode: 'environment' }, { fps: 10, qrbox: getQrScanBox },
      expect.any(Function), undefined,
    );
    unmount();
    await waitFor(() => expect(instances[0].clear).toHaveBeenCalledOnce());
    expect(instances[0].stop).toHaveBeenCalledOnce();
  });

  it('clears a failed scanner before retrying and prevents duplicate starts', async () => {
    const pending = deferred();
    startMock.mockRejectedValueOnce('Error getting userMedia, error = NotAllowedError: denied')
      .mockReturnValueOnce(pending.promise);
    const { result } = renderHook(() => useQrScanner(options()));
    await waitFor(() => expect(result.current.cameraError).toBe('permission'));
    act(() => result.current.retryScanner());
    await waitFor(() => expect(startMock).toHaveBeenCalledTimes(2));
    expect(instances[0].clear).toHaveBeenCalledOnce();
    expect(result.current.cameraError).toBeNull();
    expect(result.current.isStarting).toBe(true);
    act(() => result.current.retryScanner());
    expect(startMock).toHaveBeenCalledTimes(2);
    await act(async () => pending.resolve());
    await waitFor(() => expect(result.current.isStarting).toBe(false));
  });

  it('releases a late camera start after unmount before starting another scanner', async () => {
    const pending = deferred();
    startMock.mockReturnValueOnce(pending.promise);
    const onSuccess = vi.fn();
    const first = renderHook(() => useQrScanner({ elementId: 'reader', onSuccess }));
    await waitFor(() => expect(startMock).toHaveBeenCalledOnce());
    first.unmount();
    const second = renderHook(() => useQrScanner(options()));
    expect(startMock).toHaveBeenCalledOnce();
    const callback = startMock.mock.calls[0][2] as (text: string) => void;
    callback('late-result');
    expect(onSuccess).not.toHaveBeenCalled();
    await act(async () => pending.resolve());
    await waitFor(() => expect(second.result.current.isStarting).toBe(false));
    expect(instances[0].stop).toHaveBeenCalledOnce();
    expect(instances[0].clear).toHaveBeenCalledOnce();
    expect(instances[0].stop.mock.invocationCallOrder[0]).toBeLessThan(startMock.mock.invocationCallOrder[1]);
  });

  it('restarts once after width changes and stops the previous camera first', async () => {
    let notify!: () => void;
    const disconnect = vi.fn();
    vi.stubGlobal('ResizeObserver', class {
      constructor(callback: () => void) { notify = callback; }
      observe() {}
      disconnect = disconnect;
    });
    const element = document.createElement('div');
    element.id = 'reader';
    let width = 320;
    Object.defineProperty(element, 'clientWidth', { get: () => width });
    document.body.append(element);
    const { result, unmount } = renderHook(() => useQrScanner(options()));
    await waitFor(() => expect(result.current.isStarting).toBe(false));
    act(() => {
      width = 500;
      notify();
      width = 390;
      notify();
    });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 300)); });
    await waitFor(() => expect(startMock).toHaveBeenCalledTimes(2));
    expect(instances[0].stop).toHaveBeenCalledOnce();
    expect(instances[0].stop.mock.invocationCallOrder[0]).toBeLessThan(startMock.mock.invocationCallOrder[1]);
    // Height-only changes/observer notifications must not cause a restart loop.
    act(() => notify());
    unmount();
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 300)); });
    expect(startMock).toHaveBeenCalledTimes(2);
    expect(disconnect).toHaveBeenCalled();
    element.remove();
  });

  it('supports StrictMode without overlapping or losing the camera', async () => {
    const { result } = renderHook(() => useQrScanner(options()), { wrapper: StrictMode });
    await waitFor(() => expect(result.current.isStarting).toBe(false));
    expect(startMock).toHaveBeenCalledOnce();
    expect(instances[0].isScanning).toBe(true);
  });

  it('uses the latest success callback and stops only once on explicit stop plus unmount', async () => {
    const firstCallback = vi.fn();
    const nextCallback = vi.fn();
    const { result, rerender, unmount } = renderHook(
      ({ onSuccess }) => useQrScanner({ elementId: 'reader', onSuccess }),
      { initialProps: { onSuccess: firstCallback } },
    );
    await waitFor(() => expect(result.current.isStarting).toBe(false));
    rerender({ onSuccess: nextCallback });
    act(() => startMock.mock.calls[0][2]('code'));
    expect(firstCallback).not.toHaveBeenCalled();
    expect(nextCallback).toHaveBeenCalledWith('code');
    act(() => result.current.stopScanner());
    unmount();
    await waitFor(() => expect(instances[0].clear).toHaveBeenCalledOnce());
    expect(instances[0].stop).toHaveBeenCalledOnce();
  });
});
