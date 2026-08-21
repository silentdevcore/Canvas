import type { Workbook } from '../spreadsheet/types';
import { designerAssetContentUrl, uploadDesignerImage } from './designerAssetApi';

export interface ValidationIssue { severity: string; path: string; message: string; }
export interface ValidationResult {
  valid: boolean;
  version: string;
  supportedVersion: string;
  issues: ValidationIssue[];
}

/** Calls the backend Spreadsheet SDK endpoints (round-trips a workbook to/from .xlsx). */
export class SpreadsheetService {
  private static readonly API_BASE_URL = '/api';
  private static readonly XLSX_MIME = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

  static safeFileStem(name: string | null | undefined): string {
    const normalized = (name ?? '').trim()
      .replace(/[<>:"/\\|?*\u0000-\u001f\s]+/g, '-')
      .replace(/^[.-]+|[.-]+$/g, '')
      .slice(0, 180);
    return normalized || 'workbook';
  }

  private static responseFileName(response: Response): string | undefined {
    const disposition = response.headers.get('Content-Disposition') ?? '';
    const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i);
    const plain = disposition.match(/filename="?([^";]+)"?/i);
    const candidate = encoded ? decodeURIComponent(encoded[1]) : plain?.[1];
    if (!candidate) return undefined;
    const leaf = candidate.split(/[\\/]/).pop() ?? '';
    const stem = leaf.replace(/\.xlsx$/i, '');
    return `${this.safeFileStem(stem)}.xlsx`;
  }

  private static downloadBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  }

  private static readPrefix(blob: Blob, length: number): Promise<Uint8Array> {
    const slice = blob.slice(0, length);
    if (typeof slice.arrayBuffer === 'function') {
      return slice.arrayBuffer().then(buffer => new Uint8Array(buffer));
    }
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(new Uint8Array(reader.result as ArrayBuffer));
      reader.onerror = () => reject(reader.error ?? new Error('Spreadsheet export could not be inspected.'));
      reader.readAsArrayBuffer(slice);
    });
  }

  /** Validate a workbook (structural + schemaVersion checks) server-side. */
  static async validate(workbook: Workbook): Promise<ValidationResult> {
    const res = await fetch(`${this.API_BASE_URL}/spreadsheet/validate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(workbook),
    });
    if (!res.ok) throw new Error(`Validation failed (${res.status})`);
    return res.json();
  }

  /** Export a workbook to .xlsx and trigger a download. */
  static async exportXlsx(workbook: Workbook, fileName?: string): Promise<void> {
    const hydrated = await this.hydrateImages(workbook);
    const res = await fetch(`${this.API_BASE_URL}/spreadsheet/export`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(hydrated),
    });
    if (!res.ok) throw new Error(`Export failed (${res.status})`);
    const blob = await res.blob();
    const contentType = (res.headers.get('Content-Type') ?? blob.type).split(';')[0].trim().toLowerCase();
    if (contentType !== this.XLSX_MIME)
      throw new Error(`Export returned '${contentType || 'unknown'}' instead of an Excel workbook.`);
    const prefix = await this.readPrefix(blob, 4);
    if (prefix[0] !== 0x50 || prefix[1] !== 0x4b)
      throw new Error('Export returned invalid Excel workbook data.');
    const fallback = `${this.safeFileStem(fileName || workbook.name)}.xlsx`;
    this.downloadBlob(blob, this.responseFileName(res) ?? fallback);
  }

  static exportText(content: string, workbookName: string | null | undefined, format: 'csv' | 'json'): void {
    const mime = format === 'csv' ? 'text/csv;charset=utf-8' : 'application/json';
    this.downloadBlob(new Blob([content], { type: mime }), `${this.safeFileStem(workbookName)}.${format}`);
  }

  /** Import an .xlsx file into a workbook model. */
  static async importXlsx(file: File): Promise<Workbook> {
    const fd = new FormData();
    fd.append('file', file);
    const res = await fetch(`${this.API_BASE_URL}/spreadsheet/import`, { method: 'POST', body: fd });
    if (!res.ok) throw new Error(`Import failed (${res.status})`);
    return this.storeEmbeddedImages(await res.json() as Workbook);
  }

  /** Migrates legacy/imported data URLs into organization-owned assets before the workbook enters the store. */
  static async storeEmbeddedImages(workbook: Workbook): Promise<Workbook> {
    for (const sheet of workbook.sheets) {
      for (const image of sheet.images ?? []) {
        if (!image.data) continue;
        const blob = await (await fetch(image.data)).blob();
        const asset = await uploadDesignerImage(new File(
          [blob],
          image.fileName || `spreadsheet-image.${assetExtension(blob.type)}`,
          { type: blob.type },
        ));
        image.assetId = asset.id;
        image.contentUrl = asset.contentUrl;
        image.contentType = asset.contentType;
        delete image.data;
      }
    }
    return workbook;
  }

  private static async hydrateImages(workbook: Workbook): Promise<Workbook> {
    const copy = structuredClone(workbook);
    await Promise.all(copy.sheets.flatMap(sheet => (sheet.images ?? []).map(async image => {
      if (!image.assetId) return;
      const response = await fetch(designerAssetContentUrl(image.assetId));
      if (!response.ok) throw new Error(`Spreadsheet image could not be loaded (${response.status}).`);
      image.data = await blobToDataUrl(await response.blob());
    })));
    return copy;
  }
}

const assetExtension = (contentType: string) => contentType === 'image/jpeg' ? 'jpg' : 'png';

const blobToDataUrl = (blob: Blob): Promise<string> => new Promise((resolve, reject) => {
  const reader = new FileReader();
  reader.onload = () => resolve(String(reader.result));
  reader.onerror = () => reject(reader.error ?? new Error('Spreadsheet image could not be read.'));
  reader.readAsDataURL(blob);
});
