import { Html5Qrcode } from 'html5-qrcode';
import { useCallback, useEffect, useRef, useState } from 'react';
import { classifyCameraError, type CameraErrorKind } from './cameraError';
import { getQrScanBox } from './qrScannerLayout';

interface UseQrScannerOptions {
  elementId: string;
  fps?: number;
  onSuccess: (decodedText: string) => void;
}

// Serialize camera ownership, including pending permission requests, across
// page changes and React StrictMode's setup/cleanup/setup cycle.
let cameraQueue = Promise.resolve();
function enqueue(operation: () => Promise<void>) {
  cameraQueue = cameraQueue.then(operation).catch((error) => {
    console.warn('Kamera konnte nicht vollständig freigegeben werden.', error);
  });
}

export function useQrScanner({
  elementId,
  fps = 10,
  onSuccess,
}: UseQrScannerOptions) {
  const [cameraError, setCameraError] = useState<CameraErrorKind | null>(null);
  const [isStarting, setIsStarting] = useState(true);
  const [attempt, setAttempt] = useState(0);
  const scannerRef = useRef<Html5Qrcode | null>(null);
  const cancelSessionRef = useRef<() => void>(() => {});
  const onSuccessRef = useRef(onSuccess);
  useEffect(() => { onSuccessRef.current = onSuccess; }, [onSuccess]);

  useEffect(() => {
    let cancelled = false;
    let scanner: Html5Qrcode | null = null;
    let resizeTimer: ReturnType<typeof setTimeout> | undefined;
    const element = document.getElementById(elementId);
    let lastWidth = element?.clientWidth;
    // The library fixes video/canvas geometry at start. Recreate both together
    // after a width change instead of stretching just the video with CSS.
    const resizeObserver = typeof ResizeObserver !== 'undefined' && element
      ? new ResizeObserver(() => {
          const nextWidth = element.clientWidth;
          if (nextWidth === lastWidth) return;
          lastWidth = nextWidth;
          clearTimeout(resizeTimer);
          resizeTimer = setTimeout(() => {
            if (cancelled || !scanner?.isScanning || nextWidth <= 0) return;
            setCameraError(null);
            setIsStarting(true);
            setAttempt((value) => value + 1);
          }, 250);
        })
      : null;
    if (element) resizeObserver?.observe(element);
    const cancel = () => {
      if (cancelled) return;
      cancelled = true;
      clearTimeout(resizeTimer);
      resizeObserver?.disconnect();
      enqueue(async () => {
        if (!scanner) return;
        if (scanner.isScanning) await scanner.stop();
        scanner.clear();
        if (scannerRef.current === scanner) scannerRef.current = null;
      });
    };
    cancelSessionRef.current = cancel;

    enqueue(async () => {
      if (cancelled) return;
      try {
        // localhost may be secure over HTTP. Missing APIs alone do not
        // establish that the device has no camera.
        if (window.isSecureContext === false) {
          setCameraError('insecure');
          return;
        }
        if (!navigator.mediaDevices?.getUserMedia) {
          setCameraError('unsupported');
          return;
        }
        scanner = new Html5Qrcode(elementId);
        scannerRef.current = scanner;
        await scanner.start(
          { facingMode: 'environment' },
          { fps, qrbox: getQrScanBox },
          (decodedText) => {
            if (!cancelled) onSuccessRef.current(decodedText);
          },
          // An unrecognized frame is not a camera access error.
          undefined,
        );
      } catch (error) {
        if (!cancelled) setCameraError(classifyCameraError(error));
      } finally {
        if (!cancelled) setIsStarting(false);
      }
    });
    return cancel;
  }, [elementId, fps, attempt]);

  const stopScanner = useCallback(() => cancelSessionRef.current(), []);
  const retryScanner = () => {
    if (isStarting || !cameraError) return;
    setCameraError(null);
    setIsStarting(true);
    setAttempt((value) => value + 1);
  };

  return { cameraError, isStarting, retryScanner, stopScanner, scannerRef };
}
