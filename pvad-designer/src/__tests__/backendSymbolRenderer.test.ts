import { buildSymbolRequest } from '@/components/Rendering/BackendSymbolRenderer';
import type { SimpleElement } from '@/types';

const element = {
  id: 'symbol',
  type: 'barcode',
  name: 'Symbol',
  x: 0,
  y: 0,
  width: 370.9,
  height: 100.8,
  barcodeValue: 'PXA-2026-0001',
  barcodeType: 'CODE128',
  qrValue: 'https://powerdoxautomation.com/parity',
} as SimpleElement;

describe('backend symbol rendering contract', () => {
  test('uses the exact PDF barcode dimensions', () => {
    expect(buildSymbolRequest(element, 'barcode')).toEqual({
      type: 'barcode',
      value: 'PXA-2026-0001',
      format: 'CODE128',
      width: 370,
      height: 70,
    });
  });

  test('does not place QR content in the endpoint URL', () => {
    expect(buildSymbolRequest(element, 'qrcode')).toEqual({
      type: 'qrcode',
      value: 'https://powerdoxautomation.com/parity',
      format: undefined,
      width: undefined,
      height: undefined,
    });
  });
});
