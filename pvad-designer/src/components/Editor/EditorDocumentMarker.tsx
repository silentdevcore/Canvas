import { FiBookmark } from 'react-icons/fi';
import type { SimpleElement } from '@/types';

export default function EditorDocumentMarker({ element }: { element: SimpleElement }) {
  if (element.type === 'bookmark') return (
    <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', gap: 4, padding: '0 6px', fontSize: 12, color: '#0891b2' }}>
      <FiBookmark size={12} />
      <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{element.bookmarkName || 'bookmark'}</span>
    </div>
  );
  const color = element.style?.color || '#7c3aed';
  return <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', gap: 8, color }}>
    <div style={{ flex: 1, borderTop: `2px ${element.style?.dashStyle || 'dashed'} ${color}` }} />
    <strong style={{ fontSize: 10, textTransform: 'uppercase', letterSpacing: 0 }}>
      {element.pageBoundaryMode === 'end' ? 'Page end' : 'Page start'}
    </strong>
    <div style={{ flex: 1, borderTop: `2px ${element.style?.dashStyle || 'dashed'} ${color}` }} />
  </div>;
}
