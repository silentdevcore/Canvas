import {
  FiAlertCircle,
  FiCheckCircle,
  FiHelpCircle,
  FiMinusCircle,
  FiSlash,
} from 'react-icons/fi';
import type { PdfTrustStatus } from './pdfTrustApi';

interface TrustStatusBadgeProps {
  status: PdfTrustStatus;
  label: string;
  explanation: string;
}

const icons = [FiCheckCircle, FiAlertCircle, FiHelpCircle, FiSlash, FiMinusCircle] as const;

export function TrustStatusBadge({ status, label, explanation }: TrustStatusBadgeProps) {
  const Icon = icons[status] ?? FiHelpCircle;
  return (
    <span
      className="pdfv-trust-status"
      data-status={status}
      title={explanation}
      aria-label={`${label}. ${explanation}`}
    >
      <Icon aria-hidden="true" />
      <span>{label}</span>
    </span>
  );
}
