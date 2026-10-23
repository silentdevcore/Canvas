export const safePdfFileName = (name: string | null | undefined): string => {
  const withoutExtension = (name ?? '').replace(/\.pdf$/i, '');
  const stem = withoutExtension.trim()
    .replace(/[<>:"/\\|?*\u0000-\u001f\s]+/g, '-')
    .replace(/^[.-]+|[.-]+$/g, '')
    .slice(0, 180);
  return `${stem || 'document'}.pdf`;
};

export const assertPdfBytes = (bytes: ArrayBuffer | Uint8Array, message = 'The source does not contain valid PDF data.'): Uint8Array => {
  const view = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
  const limit = Math.min(view.length - 5, 1024);
  for (let index = 0; index <= limit; index += 1) {
    if (view[index] === 0x25 && view[index + 1] === 0x50 && view[index + 2] === 0x44 &&
        view[index + 3] === 0x46 && view[index + 4] === 0x2d) {
      return view;
    }
  }
  throw new Error(message);
};

export const createPdfBlob = (bytes: ArrayBuffer | Uint8Array, invalidMessage?: string): Blob => {
  const validBytes = assertPdfBytes(bytes, invalidMessage);
  const copy = new Uint8Array(validBytes.byteLength);
  copy.set(validBytes);
  return new Blob([copy.buffer], { type: 'application/pdf' });
};

export const downloadPdfBytes = (bytes: ArrayBuffer | Uint8Array, name: string, invalidMessage?: string): void => {
  const url = URL.createObjectURL(createPdfBlob(bytes, invalidMessage));
  const link = document.createElement('a');
  link.href = url;
  link.download = safePdfFileName(name);
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
};
