import React, { useRef, useState } from 'react';
import { FiX, FiUpload, FiShield, FiLoader, FiCheck, FiAlertCircle } from 'react-icons/fi';
import ExportService from '@/services/ExportService';

interface Props {
  onClose: () => void;
}

type SignState = 'idle' | 'signing' | 'done' | 'error';

const SignDocxModal: React.FC<Props> = ({ onClose }) => {
  const docxRef = useRef<HTMLInputElement>(null);
  const certRef = useRef<HTMLInputElement>(null);

  const [docxFile, setDocxFile] = useState<File | null>(null);
  const [certFile, setCertFile] = useState<File | null>(null);
  const [password, setPassword] = useState('');
  const [state, setState] = useState<SignState>('idle');
  const [error, setError] = useState('');
  const [signedSize, setSignedSize] = useState(0);

  const canSign = docxFile !== null && certFile !== null && state !== 'signing';

  const handleSign = async () => {
    if (!docxFile || !certFile) return;
    setState('signing');
    setError('');
    try {
      const signed = await ExportService.signDocx(docxFile, certFile, password || undefined);
      setSignedSize(signed.size);

      const url = URL.createObjectURL(signed);
      const a = document.createElement('a');
      a.href = url;
      a.download = docxFile.name.replace(/\.docx$/i, '') + '_signed.docx';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      setState('done');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Signing failed');
      setState('error');
    }
  };

  return (
    <div className="sign-modal-backdrop" onClick={onClose}>
      <div className="sign-modal" onClick={e => e.stopPropagation()} role="dialog" aria-label="Sign DOCX">
        <div className="sign-modal-header">
          <span className="sign-modal-icon"><FiShield size={18} /></span>
          <h2>Sign DOCX</h2>
          <button className="sign-modal-close" onClick={onClose} aria-label="Close"><FiX size={18} /></button>
        </div>

        <div className="sign-modal-body">
          <p className="sign-modal-intro">
            Apply an X.509 digital signature to a DOCX file using your PFX/P12 certificate.
            The signed file will be downloaded automatically.
          </p>

          {/* DOCX file */}
          <div className="sign-modal-field">
            <label>DOCX file</label>
            <input
              ref={docxRef}
              type="file"
              accept=".docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
              style={{ display: 'none' }}
              onChange={e => { const f = e.target.files?.[0]; if (f) setDocxFile(f); }}
            />
            <button
              className={`sign-file-btn${docxFile ? ' sign-file-btn--set' : ''}`}
              onClick={() => docxRef.current?.click()}
              type="button"
            >
              <FiUpload size={14} />
              {docxFile ? docxFile.name : 'Choose DOCX file…'}
            </button>
          </div>

          {/* Certificate */}
          <div className="sign-modal-field">
            <label>PFX / P12 certificate</label>
            <input
              ref={certRef}
              type="file"
              accept=".pfx,.p12"
              style={{ display: 'none' }}
              onChange={e => { const f = e.target.files?.[0]; if (f) setCertFile(f); }}
            />
            <button
              className={`sign-file-btn${certFile ? ' sign-file-btn--set' : ''}`}
              onClick={() => certRef.current?.click()}
              type="button"
            >
              <FiUpload size={14} />
              {certFile ? certFile.name : 'Choose certificate…'}
            </button>
          </div>

          {/* Password */}
          <div className="sign-modal-field">
            <label>Certificate password <span className="sign-optional">(optional)</span></label>
            <input
              type="password"
              className="sign-password-input"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder="Leave blank if certificate has no password"
            />
          </div>

          {/* Error */}
          {state === 'error' && (
            <div className="sign-error">
              <FiAlertCircle size={14} />
              {error}
            </div>
          )}

          {/* Success */}
          {state === 'done' && (
            <div className="sign-success">
              <FiCheck size={14} />
              Signed and downloaded ({(signedSize / 1024).toFixed(1)} KB)
            </div>
          )}
        </div>

        <div className="sign-modal-footer">
          <button className="sign-cancel-btn" onClick={onClose}>Cancel</button>
          <button
            className={`sign-action-btn sign-action-btn--${state}`}
            onClick={handleSign}
            disabled={!canSign}
          >
            {state === 'signing' ? <FiLoader className="spin" size={15} /> : <FiShield size={15} />}
            {state === 'signing' ? 'Signing…' : state === 'done' ? 'Sign again' : 'Sign & Download'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default SignDocxModal;
