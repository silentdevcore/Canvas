import React from 'react';

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';
  size?: 'sm' | 'md' | 'lg';
  children: React.ReactNode;
}

const Button: React.FC<ButtonProps> = ({
  variant = 'primary',
  size = 'md',
  children,
  className = '',
  disabled = false,
  ...props
}) => {
  const baseStyles: React.CSSProperties = {
    border: 'none',
    borderRadius: '6px',
    fontFamily: 'inherit',
    fontWeight: '500',
    cursor: disabled ? 'not-allowed' : 'pointer',
    transition: 'all 0.15s ease',
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '6px',
    outline: 'none',
    position: 'relative',
  };

  const variantStyles = {
    primary: {
      backgroundColor: 'var(--ui-color-accent)',
      color: 'var(--ui-color-text-inverse)',
      border: '1px solid var(--ui-color-accent)',
      '&:hover': {
        backgroundColor: 'color-mix(in srgb, var(--ui-color-accent) 82%, black)',
        borderColor: 'color-mix(in srgb, var(--ui-color-accent) 82%, black)',
        transform: 'translateY(-1px)',
        boxShadow: '0 4px 12px rgba(37, 99, 235, 0.3)',
      },
      '&:active': {
        backgroundColor: 'color-mix(in srgb, var(--ui-color-accent) 70%, black)',
        borderColor: 'color-mix(in srgb, var(--ui-color-accent) 70%, black)',
        transform: 'translateY(0)',
      },
      '&:focus': {
        outline: 'none',
        boxShadow: '0 0 0 2px var(--ui-color-accent), 0 0 0 4px color-mix(in srgb, var(--ui-color-accent) 20%, transparent)',
      },
    },
    secondary: {
      backgroundColor: 'var(--ui-color-bg-panel)',
      color: 'var(--ui-color-text-secondary)',
      border: '1px solid var(--ui-color-border-strong)',
      '&:hover': {
        backgroundColor: 'var(--ui-color-bg-muted)',
        borderColor: 'var(--ui-color-text-secondary)',
        transform: 'translateY(-1px)',
        boxShadow: '0 4px 12px rgba(0, 0, 0, 0.1)',
      },
      '&:active': {
        backgroundColor: 'var(--ui-color-bg-app)',
        transform: 'translateY(0)',
      },
      '&:focus': {
        boxShadow: '0 0 0 3px color-mix(in srgb, var(--ui-color-accent) 15%, transparent)',
      },
    },
    danger: {
      backgroundColor: 'var(--ui-color-danger)',
      color: 'var(--ui-color-text-inverse)',
      border: '1px solid var(--ui-color-danger)',
      '&:hover': {
        backgroundColor: 'color-mix(in srgb, var(--ui-color-danger) 82%, black)',
        borderColor: 'color-mix(in srgb, var(--ui-color-danger) 82%, black)',
        transform: 'translateY(-1px)',
        boxShadow: '0 4px 12px rgba(220, 38, 38, 0.3)',
      },
      '&:active': {
        backgroundColor: 'color-mix(in srgb, var(--ui-color-danger) 70%, black)',
        borderColor: 'color-mix(in srgb, var(--ui-color-danger) 70%, black)',
        transform: 'translateY(0)',
      },
      '&:focus': {
        boxShadow: '0 0 0 3px color-mix(in srgb, var(--ui-color-danger) 15%, transparent)',
      },
    },
    ghost: {
      backgroundColor: 'transparent',
      color: 'var(--ui-color-text-secondary)',
      border: '1px solid transparent',
      '&:hover': {
        backgroundColor: 'var(--ui-color-bg-app)',
        color: 'var(--ui-color-text-secondary)',
        transform: 'translateY(-1px)',
      },
      '&:active': {
        backgroundColor: 'var(--ui-color-border-subtle)',
        transform: 'translateY(0)',
      },
      '&:focus': {
        boxShadow: '0 0 0 3px color-mix(in srgb, var(--ui-color-accent) 15%, transparent)',
      },
    },
  };

  const sizeStyles = {
    sm: {
      padding: '6px 12px',
      fontSize: '12px',
      height: '28px',
      minWidth: '28px',
    },
    md: {
      padding: '8px 16px',
      fontSize: '14px',
      height: '36px',
      minWidth: '36px',
    },
    lg: {
      padding: '12px 20px',
      fontSize: '16px',
      height: '44px',
      minWidth: '44px',
    },
  };

  const disabledStyles: React.CSSProperties = {
    opacity: 0.5,
    cursor: 'not-allowed',
    transform: 'none',
    boxShadow: 'none',
  };

  const combinedStyles: React.CSSProperties = {
    ...baseStyles,
    ...sizeStyles[size],
    ...(disabled ? disabledStyles : {}),
  };

  // Handle hover and focus states with CSS-in-JS approach
  const handleMouseEnter = (e: React.MouseEvent<HTMLButtonElement>) => {
    if (disabled) return;
    const hoverStyles = variantStyles[variant]['&:hover'] as React.CSSProperties;
    Object.assign(e.currentTarget.style, hoverStyles);
  };

  const handleMouseLeave = (e: React.MouseEvent<HTMLButtonElement>) => {
    if (disabled) return;
    // Reset to base styles
    Object.assign(e.currentTarget.style, combinedStyles);
  };

  const handleMouseDown = (e: React.MouseEvent<HTMLButtonElement>) => {
    if (disabled) return;
    const activeStyles = variantStyles[variant]['&:active'] as React.CSSProperties;
    Object.assign(e.currentTarget.style, activeStyles);
  };

  const handleMouseUp = (e: React.MouseEvent<HTMLButtonElement>) => {
    if (disabled) return;
    // Reset to hover styles
    const hoverStyles = variantStyles[variant]['&:hover'] as React.CSSProperties;
    Object.assign(e.currentTarget.style, hoverStyles);
  };

  const handleFocus = (e: React.FocusEvent<HTMLButtonElement>) => {
    if (disabled) return;
    const focusStyles = variantStyles[variant]['&:focus'] as React.CSSProperties;
    e.currentTarget.style.boxShadow = focusStyles.boxShadow as string;
  };

  const handleBlur = (e: React.FocusEvent<HTMLButtonElement>) => {
    if (disabled) return;
    e.currentTarget.style.boxShadow = 'none';
  };

  return (
    <button
      {...props}
      className={`ui-button ui-button-${variant} ui-button-${size} ${className}`}
      style={combinedStyles}
      disabled={disabled}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      onMouseDown={handleMouseDown}
      onMouseUp={handleMouseUp}
      onFocus={handleFocus}
      onBlur={handleBlur}
      aria-disabled={disabled}
      aria-pressed={variant === 'primary' && props['aria-pressed'] !== undefined ? props['aria-pressed'] : undefined}
      role={props.role || (variant === 'primary' ? 'button' : 'button')}
    >
      {children}
    </button>
  );
};

export default Button;