import type { Workbook } from '../spreadsheet/types';

/** Calls the backend Spreadsheet SDK endpoints (round-trips a workbook to/from .xlsx). */
export class SpreadsheetService {
  private static readonly API_BASE_URL = '/api';

  /** Export a workbook to .xlsx and trigger a download. */
  static async exportXlsx(workbook: Workbook, fileName?: string): Promise<void> {
    const res = await fetch(`${this.API_BASE_URL}/spreadsheet/export`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(workbook),
    });
    if (!res.ok) throw new Error(`Export failed (${res.status})`);
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${(fileName || workbook.name || 'workbook').replace(/[\\/:*?"<>|]/g, '_')}.xlsx`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }

  /** Import an .xlsx file into a workbook model. */
  static async importXlsx(file: File): Promise<Workbook> {
    const fd = new FormData();
    fd.append('file', file);
    const res = await fetch(`${this.API_BASE_URL}/spreadsheet/import`, { method: 'POST', body: fd });
    if (!res.ok) throw new Error(`Import failed (${res.status})`);
    return res.json();
  }
}
