const assetBase = '/api/pxa/v1/designer/assets';
export interface DesignerAsset {
  id: string;
  fileName: string | null;
  contentType: string;
  length: number;
  checksum: string;
  width: number | null;
  height: number | null;
  contentUrl: string;
  createdAt: string;
}

export class DesignerAssetApiError extends Error {
  status = 0;
  code?: string;
}

export async function uploadDesignerImage(file: File): Promise<DesignerAsset> {
  const form = new FormData();
  form.append('file', file);
  const response = await fetch(assetBase, { method: 'POST', body: form });
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    const error = new DesignerAssetApiError(
      body?.detail || body?.title || 'The image upload failed.',
    );
    error.status = response.status;
    error.code = body?.code;
    throw error;
  }
  return body as DesignerAsset;
}

export const designerAssetContentUrl = (assetId: string): string =>
  `${assetBase}/${encodeURIComponent(assetId)}/content`;
