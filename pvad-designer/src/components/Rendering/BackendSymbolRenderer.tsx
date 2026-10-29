import { useEffect, useRef, useState } from 'react';
import JsBarcode from 'jsbarcode';
import { QRCodeSVG } from 'qrcode.react';
import type { SimpleElement } from '@/types';

type SymbolType = 'qrcode' | 'barcode';
type LoadState = 'loading' | 'ready' | 'fallback';

const renderEndpoint = '/api/pvad/v1/designer/symbols/render';

async function responseToPngDataUrl(response: Response): Promise<string> {
  const bytes = new Uint8Array(await response.arrayBuffer());
  let binary = '';
  for (let offset = 0; offset < bytes.length; offset += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(offset, offset + 0x8000));
  }
  return `data:image/png;base64,${btoa(binary)}`;
}

export function buildSymbolRequest(element: SimpleElement, type: SymbolType) {
  return {
    type,
    value: type === 'qrcode'
      ? element.qrValue || 'https://example.com'
      : element.barcodeValue || '123456789012',
    format: type === 'barcode' ? element.barcodeType || 'CODE128' : undefined,
    width: type === 'barcode' ? Math.max(1, Math.floor(element.width)) : undefined,
    height: type === 'barcode' ? Math.max(1, Math.floor(element.height * 0.7)) : undefined,
  };
}

function LocalBarcode({ element }: { element: SimpleElement }) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  useEffect(() => {
    if (!canvasRef.current) return;
    JsBarcode(canvasRef.current, element.barcodeValue || '123456789012', {
      format: element.barcodeType || 'CODE128',
      lineColor: '#000000',
      width: 2,
      height: Math.max(1, element.height * 0.7),
      displayValue: false,
      margin: 2,
    });
  }, [element.barcodeType, element.barcodeValue, element.height]);

  return <canvas ref={canvasRef} style={{ width: '100%', height: '100%', display: 'block' }} />;
}

export default function BackendSymbolRenderer({
  element,
  type,
}: {
  element: SimpleElement;
  type: SymbolType;
}) {
  const [state, setState] = useState<LoadState>('loading');
  const [source, setSource] = useState<string | null>(null);
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const request = buildSymbolRequest(element, type);
  const requestKey = JSON.stringify(request);

  useEffect(() => {
    const abortController = new AbortController();
    let disposed = false;
    setState('loading');
    setSource(null);
    setErrorCode(null);

    void fetch(renderEndpoint, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: requestKey,
      signal: abortController.signal,
    }).then(async response => {
      if (!response.ok || !response.headers.get('content-type')?.startsWith('image/png'))
        throw new Error(`Symbol rendering failed with HTTP ${response.status}.`);
      const dataUrl = await responseToPngDataUrl(response);
      if (disposed) return;
      setSource(dataUrl);
      setState('ready');
    }).catch(error => {
      if (disposed || abortController.signal.aborted || (error instanceof DOMException && error.name === 'AbortError')) return;
      setErrorCode(error instanceof Error && error.message.startsWith('Symbol rendering failed')
        ? 'response'
        : error instanceof TypeError ? 'request' : 'source-decode');
      setState('fallback');
    });

    return () => {
      disposed = true;
      abortController.abort();
    };
  }, [requestKey]);

  const symbolHeight = type === 'barcode' ? '70%' : '100%';
  return (
    <div data-symbol-status={state} data-symbol-type={type} data-symbol-error={errorCode ?? undefined} style={{
      width: '100%',
      height: symbolHeight,
      overflow: 'hidden',
      background: '#ffffff',
    }}>
      {source ? (
        <img src={source} alt="" onError={() => {
          setErrorCode('image-decode');
          setSource(null);
          setState('fallback');
        }} style={{
          width: '100%',
          height: '100%',
          display: 'block',
          objectFit: 'fill',
          imageRendering: 'pixelated',
        }} />
      ) : type === 'qrcode' ? (
        <QRCodeSVG
          value={request.value}
          width="100%"
          height="100%"
          level="M"
          includeMargin
          boostLevel={false}
        />
      ) : (
        <LocalBarcode element={element} />
      )}
    </div>
  );
}
