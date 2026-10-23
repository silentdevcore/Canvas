import type { SimpleElement } from '@/types';

export default function HighlightRenderer({ element }: { element: SimpleElement }) {
  return <div style={{
    width: '100%',
    height: '100%',
    background: element.style?.backgroundColor ?? '#fef08a',
  }} />;
}
