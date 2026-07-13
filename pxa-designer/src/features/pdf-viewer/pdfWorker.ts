import { pdfjs } from 'react-pdf';

export function configurePdfWorker(): void {
  pdfjs.GlobalWorkerOptions.workerSrc = new URL(
    'pdfjs-dist/build/pdf.worker.min.mjs',
    import.meta.url,
  ).toString();
}
