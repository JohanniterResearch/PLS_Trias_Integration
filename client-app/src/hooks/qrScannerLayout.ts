// Use the actual rendered camera dimensions, not the screen dimensions.
// Keeping a margin on every side makes it easier to place the complete QR code.
export function getQrScanBox(viewfinderWidth: number, viewfinderHeight: number) {
  const side = Math.floor(Math.min(viewfinderWidth, viewfinderHeight) * 0.8);
  return { width: side, height: side };
}
