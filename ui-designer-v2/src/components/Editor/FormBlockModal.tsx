import React, { useState } from 'react';
import { FiX, FiGrid } from 'react-icons/fi';
import type { SimpleElement } from '@/types';

interface Props {
  onClose: () => void;
  onInsert: (elements: SimpleElement[]) => void;
}

type BlockType = 'address' | 'contact' | 'personal' | 'custom';

interface FieldDef { label: string; name: string; type: 'field' | 'dropdown'; options?: string[] }

const BLOCK_DEFS: Record<BlockType, { title: string; description: string; fields: FieldDef[] }> = {
  address: {
    title: 'Address Block',
    description: 'Full postal address: name, street, city, postal code, country.',
    fields: [
      { label: 'Full Name',    name: 'name',        type: 'field' },
      { label: 'Street',       name: 'street',      type: 'field' },
      { label: 'City',         name: 'city',        type: 'field' },
      { label: 'Postal Code',  name: 'postalCode',  type: 'field' },
      { label: 'Country',      name: 'country',     type: 'field' },
    ],
  },
  contact: {
    title: 'Contact Block',
    description: 'Phone number, email address and website.',
    fields: [
      { label: 'Phone',   name: 'phone',   type: 'field' },
      { label: 'Email',   name: 'email',   type: 'field' },
      { label: 'Website', name: 'website', type: 'field' },
    ],
  },
  personal: {
    title: 'Personal Block',
    description: 'Date of birth, gender and nationality.',
    fields: [
      { label: 'Date of Birth', name: 'dob',         type: 'field' },
      { label: 'Gender',        name: 'gender',      type: 'dropdown', options: ['Male', 'Female', 'Non-binary', 'Prefer not to say'] },
      { label: 'Nationality',   name: 'nationality', type: 'field' },
    ],
  },
  custom: {
    title: 'Custom Block',
    description: 'Choose the number of fields and enter their labels.',
    fields: [],
  },
};

let _idCounter = Date.now();
const uid = (type: string) => `${type}-${(_idCounter++).toString(36)}`;

const FormBlockModal: React.FC<Props> = ({ onClose, onInsert }) => {
  const [selectedBlock, setSelectedBlock] = useState<BlockType>('address');
  const [customFields, setCustomFields] = useState<string[]>(['Field 1', 'Field 2', 'Field 3']);
  const [startX, setStartX] = useState(48);
  const [startY, setStartY] = useState(120);

  const buildElements = (): SimpleElement[] => {
    const def = BLOCK_DEFS[selectedBlock];
    const fields: FieldDef[] = selectedBlock === 'custom'
      ? customFields.map((label, i) => ({ label, name: `custom_${i}`, type: 'field' as const }))
      : def.fields;

    const GAP = 56;
    return fields.map((f, i) => {
      const base: SimpleElement = {
        id: uid(f.type),
        type: f.type,
        x: startX,
        y: startY + i * GAP,
        width: 260,
        height: 40,
        fieldLabel: f.label,
        fieldName: f.name,
        tabIndex: i + 1,
        style: { fontSize: 13, color: '#1f2937' },
      };
      if (f.type === 'dropdown' && f.options) {
        base.options = f.options;
      }
      return base;
    });
  };

  const handleInsert = () => onInsert(buildElements());

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-dialog" style={{ maxWidth: 540 }} onClick={e => e.stopPropagation()} role="dialog" aria-modal="true" aria-label="Insert Form Block">
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <FiGrid />
            <strong>Insert Form Block</strong>
          </div>
          <button className="modal-close" onClick={onClose} aria-label="Close"><FiX /></button>
        </div>

        <div className="modal-body" style={{ padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* Block type selector */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
            {(Object.keys(BLOCK_DEFS) as BlockType[]).map(type => (
              <button
                key={type}
                onClick={() => setSelectedBlock(type)}
                style={{
                  padding: '10px 14px',
                  border: `2px solid ${selectedBlock === type ? '#2563eb' : '#e2e8f0'}`,
                  borderRadius: 8,
                  background: selectedBlock === type ? '#eff6ff' : '#fff',
                  textAlign: 'left',
                  cursor: 'pointer',
                }}
              >
                <div style={{ fontWeight: 700, fontSize: 13, color: selectedBlock === type ? '#1d4ed8' : '#1f2937' }}>
                  {BLOCK_DEFS[type].title}
                </div>
                <div style={{ fontSize: 11, color: '#64748b', marginTop: 2 }}>
                  {BLOCK_DEFS[type].description}
                </div>
              </button>
            ))}
          </div>

          {/* Custom fields editor */}
          {selectedBlock === 'custom' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              <label style={{ fontWeight: 600, fontSize: 13 }}>Field labels</label>
              {customFields.map((label, i) => (
                <div key={i} style={{ display: 'flex', gap: 6 }}>
                  <input
                    type="text"
                    value={label}
                    onChange={e => setCustomFields(prev => prev.map((l, j) => j === i ? e.target.value : l))}
                    style={{ flex: 1, padding: '6px 10px', border: '1px solid #e2e8f0', borderRadius: 6 }}
                  />
                  <button
                    onClick={() => setCustomFields(prev => prev.filter((_, j) => j !== i))}
                    style={{ padding: '4px 8px', border: '1px solid #fca5a5', borderRadius: 6, background: '#fff1f2', color: '#dc2626', cursor: 'pointer' }}
                  >×</button>
                </div>
              ))}
              <button
                onClick={() => setCustomFields(prev => [...prev, `Field ${prev.length + 1}`])}
                style={{ alignSelf: 'flex-start', padding: '4px 12px', border: '1px dashed #94a3b8', borderRadius: 6, background: '#f8fafc', cursor: 'pointer', fontSize: 12 }}
              >+ Add field</button>
            </div>
          )}

          {/* Placement */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 13 }}>
              <span style={{ fontWeight: 600 }}>Start X (px)</span>
              <input type="number" value={startX} onChange={e => setStartX(Number(e.target.value))}
                style={{ padding: '6px 10px', border: '1px solid #e2e8f0', borderRadius: 6 }} />
            </label>
            <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 13 }}>
              <span style={{ fontWeight: 600 }}>Start Y (px)</span>
              <input type="number" value={startY} onChange={e => setStartY(Number(e.target.value))}
                style={{ padding: '6px 10px', border: '1px solid #e2e8f0', borderRadius: 6 }} />
            </label>
          </div>

          {/* Preview */}
          <div style={{ background: '#f8fafc', borderRadius: 8, padding: '10px 14px', border: '1px solid #e2e8f0' }}>
            <div style={{ fontWeight: 600, fontSize: 12, color: '#64748b', marginBottom: 8, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Fields to insert</div>
            {(selectedBlock === 'custom' ? customFields.map((l, i) => ({ label: l, name: `custom_${i}`, type: 'field' })) : BLOCK_DEFS[selectedBlock].fields).map((f, i) => (
              <div key={i} style={{ fontSize: 12, color: '#374151', padding: '2px 0', display: 'flex', gap: 6 }}>
                <span style={{ color: '#94a3b8', minWidth: 18 }}>{i + 1}.</span>
                <span>{f.label}</span>
                <span style={{ color: '#94a3b8' }}>({f.type})</span>
              </div>
            ))}
          </div>
        </div>

        <div className="modal-footer">
          <button className="modal-cancel-btn" onClick={onClose}>Cancel</button>
          <button className="modal-confirm-btn" onClick={handleInsert}>Insert Block</button>
        </div>
      </div>
    </div>
  );
};

export default FormBlockModal;
