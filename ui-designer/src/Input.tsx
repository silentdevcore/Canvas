import React, { useState, useEffect } from 'react';

interface InputProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'onChange'> {
  label?: string;
  error?: string;
  success?: boolean;
  validate?: (value: string) => string | null;
  onChange?: (value: string, isValid: boolean) => void;
  onValidatedChange?: (value: string, isValid: boolean) => void;
}

const Input: React.FC<InputProps> = ({
  label,
  error: externalError,
  success: externalSuccess,
  validate,
  onChange,
  onValidatedChange,
  className = '',
  value: propValue,
  ...props
}) => {
  const [internalValue, setInternalValue] = useState(propValue?.toString() || '');
  const [touched, setTouched] = useState(false);
  const [internalError, setInternalError] = useState<string | null>(null);

  const value = propValue !== undefined ? propValue.toString() : internalValue;

  // Validate the current value
  const validationError = validate ? validate(value) : null;
  const hasError = externalError || (touched && validationError);
  const hasSuccess = externalSuccess && !hasError && touched && value.trim() !== '';

  useEffect(() => {
    if (propValue !== undefined) {
      setInternalValue(propValue.toString());
    }
  }, [propValue]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = e.target.value;
    setInternalValue(newValue);

    // Run validation
    const error = validate ? validate(newValue) : null;
    setInternalError(error);

    // Call onChange with validation status
    if (onChange) {
      onChange(newValue, !error);
    }

    if (onValidatedChange) {
      onValidatedChange(newValue, !error);
    }
  };

  const handleBlur = () => {
    setTouched(true);
  };

  const handleFocus = () => {
    // Clear error on focus for better UX
    if (internalError && !externalError) {
      setInternalError(null);
    }
  };

  const stateClass = hasError ? 'is-error' : hasSuccess ? 'is-success' : '';

  return (
    <div className={`ui-input ui-input-wrapper ${className}`}>
      {label && <label className="ui-input-label">{label}</label>}
      <input
        {...props}
        value={value}
        onChange={handleChange}
        onBlur={handleBlur}
        onFocus={handleFocus}
        className={`ui-input-field ${stateClass}`.trim()}
        aria-invalid={!!hasError}
        aria-describedby={hasError ? `error-${props.id || 'input'}` : hasSuccess ? `success-${props.id || 'input'}` : undefined}
        aria-required={props.required}
        aria-label={label || props['aria-label']}
        aria-labelledby={label ? `label-${props.id || 'input'}` : props['aria-labelledby']}
      />
      {hasError && (
        <div className="ui-input-error ui-input-message ui-input-message-error">
          {externalError || internalError}
        </div>
      )}
      {hasSuccess && !hasError && (
        <div className="ui-input-success ui-input-message ui-input-message-success">
          ✓ Valid
        </div>
      )}
    </div>
  );
};

export default Input;