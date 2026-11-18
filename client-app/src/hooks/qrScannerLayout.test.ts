import { describe, expect, it } from 'vitest';
import { getQrScanBox } from './qrScannerLayout';

describe('responsive QR scan box', () => {
  it.each([
    [320, 240, 192],
    [390, 520, 312],
    [500, 375, 300],
    [844, 390, 312],
  ])('fits a %s × %s viewfinder', (width, height, expectedSide) => {
    const box = getQrScanBox(width, height);
    expect(box).toEqual({ width: expectedSide, height: expectedSide });
    expect(box.width).toBeLessThan(width);
    expect(box.height).toBeLessThan(height);
  });
});
