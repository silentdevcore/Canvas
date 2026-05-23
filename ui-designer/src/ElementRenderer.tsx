import React, { useState, useRef, useEffect, memo, useMemo } from 'react';
import { useDesignerStore } from './store';
import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { evaluateExpression, ExpressionContext } from './template/expressionEngine';

// Helper function to resolve data binding paths
function resolveBindingValue(dataPath: string | undefined, samplePayload: Record<string, any>, fallbackValue?: any): any {
  if (!dataPath) return fallbackValue;

  const pathParts = dataPath.split('.');
  let current = samplePayload;

  for (const part of pathParts) {
    if (current && typeof current === 'object' && part in current) {
      current = current[part];
    } else {
      return fallbackValue;
    }
  }

  return current !== undefined ? current : fallbackValue;
}

interface ElementRendererProps {
  elementId: string;
  documentView?: boolean;
}

const ElementRenderer: React.FC<ElementRendererProps> = memo(({ elementId, documentView = false }) => {
  const { elements, selectElement, selectedIds, updateElementPosition, updateElementSize, deleteElement, samplePayload, previewMode, previewErrors } = useDesignerStore();
  const element = elements[elementId];
  if (!element) return null;

  const isSelected = selectedIds.includes(elementId);
  const elementRef = useRef<HTMLDivElement>(null);
  const [isResizing, setIsResizing] = useState(false);
  const [resizeStart, setResizeStart] = useState({ x: 0, y: 0, width: 0, height: 0, direction: '' });

  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: elementId,
    disabled: element.locked || isResizing, // Disable dragging if element is locked or being resized
  });

  // Screen reader support
  const elementLabel = useMemo(() => {
    const typeLabel = element.type.charAt(0).toUpperCase() + element.type.slice(1);
    const positionLabel = `at position ${Math.round(element.x || 0)}, ${Math.round(element.y || 0)}`;
    const sizeLabel = element.width && element.height ? `size ${element.width} by ${element.height}` : '';
    const selectedLabel = isSelected ? 'selected' : '';
    const lockedLabel = element.locked ? 'locked' : '';

    return [typeLabel, positionLabel, sizeLabel, selectedLabel, lockedLabel].filter(Boolean).join(', ');
  }, [element.type, element.x, element.y, element.width, element.height, isSelected, element.locked]);

  // Memoize position calculation
  const position = useMemo(() => {
    let left = element.x || 0;
    let top = element.y || 0;

    if (element.groupId) {
      // Element is part of a group, find parent group position
      const groupElement = elements[element.groupId];
      if (groupElement) {
        left += groupElement.x || 0;
        top += groupElement.y || 0;
      }
    }

    return { left, top };
  }, [element.x, element.y, element.groupId, elements]);

  // Memoize style object
  const style = useMemo(() => ({
    transform: CSS.Translate.toString(transform),
    position: 'absolute' as const,
    left: position.left,
    top: position.top,
    width: element.width || 'auto',
    height: element.height || 'auto',
    zIndex: isDragging ? 'var(--ui-layer-canvas-guides)' : 'var(--ui-layer-canvas-base)',
    opacity: element.props.opacity || 1,
    boxShadow: element.props.boxShadow || 'none',
    border: element.props.borderWidth && element.props.borderWidth > 0
      ? `${element.props.borderWidth}px ${element.props.borderStyle || 'solid'} ${element.props.borderColor || 'var(--ui-color-text-primary)'}`
      : 'none',
    borderRadius: element.props.borderRadius || 0,
  }), [transform, position.left, position.top, element.width, element.height, isDragging, element.props.opacity, element.props.boxShadow, element.props.borderWidth, element.props.borderStyle, element.props.borderColor, element.props.borderRadius]);

  const containerStyle = useMemo(() => {
    const overflowStyles: Record<string, any> = {};

    // Apply overflow and layout behavior properties
    if (element.overflow) {
      // Keep together and page break controls
      if (element.overflow.keepTogether) {
        overflowStyles.pageBreakInside = 'avoid';
        overflowStyles.breakInside = 'avoid';
      }

      if (element.overflow.avoidPageBreakInside) {
        overflowStyles.pageBreakInside = 'avoid';
        overflowStyles.breakInside = 'avoid';
      }

      // Vertical and horizontal alignment
      if (element.overflow.verticalAlign) {
        overflowStyles.alignItems = element.overflow.verticalAlign === 'top' ? 'flex-start' :
                                   element.overflow.verticalAlign === 'middle' ? 'center' : 'flex-end';
      }

      if (element.overflow.horizontalAlign) {
        overflowStyles.justifyContent = element.overflow.horizontalAlign === 'left' ? 'flex-start' :
                                       element.overflow.horizontalAlign === 'center' ? 'center' : 'flex-end';
      }

      // Anchor point positioning (for absolute positioning)
      if (element.overflow.anchor) {
        // This would be used for positioning calculations
        // For now, we'll use it to set transform-origin
        const anchorMap: Record<string, string> = {
          'top-left': '0 0',
          'top-center': '50% 0',
          'top-right': '100% 0',
          'middle-left': '0 50%',
          'middle-center': '50% 50%',
          'middle-right': '100% 50%',
          'bottom-left': '0 100%',
          'bottom-center': '50% 100%',
          'bottom-right': '100% 100%'
        };
        overflowStyles.transformOrigin = anchorMap[element.overflow.anchor] || '0 0';
      }
    }

    return {
      ...style,
      ...overflowStyles,
      outline: (isSelected && !documentView) ? '2px solid var(--ui-color-accent)' : undefined,
      cursor: documentView ? 'default' : (element.locked ? 'not-allowed' : (isDragging ? 'grabbing' : 'grab')),
      opacity: element.locked ? 0.7 : 1,
      pointerEvents: documentView ? ('none' as const) : ('auto' as const),
    };
  }, [style, isSelected, documentView, element.locked, isDragging, element.overflow]);

  const handleClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    const multiSelect = e.ctrlKey || e.metaKey; // Ctrl on Windows/Linux, Cmd on Mac
    selectElement(elementId, multiSelect);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Delete' || e.key === 'Backspace') {
      deleteElement(elementId);
    }
  };

  const handleResizeStart = (e: React.MouseEvent, direction: string = 'se') => {
    console.log('Resize start:', direction);
    e.stopPropagation();
    e.preventDefault();
    setIsResizing(true);
    const rect = elementRef.current?.getBoundingClientRect();
    if (rect) {
      setResizeStart({
        x: e.clientX,
        y: e.clientY,
        width: rect.width,
        height: rect.height,
        direction,
      });
    }
  };

  const handleResizeMove = (e: MouseEvent) => {
    if (!isResizing) return;

    console.log('Resize move:', resizeStart.direction, e.clientX, e.clientY);

    const deltaX = e.clientX - resizeStart.x;
    const deltaY = e.clientY - resizeStart.y;

    let newWidth = resizeStart.width;
    let newHeight = resizeStart.height;
    let newX = element.x || 0;
    let newY = element.y || 0;

    // Handle different resize directions
    switch (resizeStart.direction) {
      case 'se': // Bottom-right (default)
        newWidth = Math.max(50, resizeStart.width + deltaX);
        newHeight = Math.max(30, resizeStart.height + deltaY);
        break;
      case 'sw': // Bottom-left
        newWidth = Math.max(50, resizeStart.width - deltaX);
        newHeight = Math.max(30, resizeStart.height + deltaY);
        newX = (element.x || 0) + (resizeStart.width - newWidth);
        break;
      case 'ne': // Top-right
        newWidth = Math.max(50, resizeStart.width + deltaX);
        newHeight = Math.max(30, resizeStart.height - deltaY);
        newY = (element.y || 0) + (resizeStart.height - newHeight);
        break;
      case 'nw': // Top-left
        newWidth = Math.max(50, resizeStart.width - deltaX);
        newHeight = Math.max(30, resizeStart.height - deltaY);
        newX = (element.x || 0) + (resizeStart.width - newWidth);
        newY = (element.y || 0) + (resizeStart.height - newHeight);
        break;
      case 'n': // Top
        newHeight = Math.max(30, resizeStart.height - deltaY);
        newY = (element.y || 0) + (resizeStart.height - newHeight);
        break;
      case 's': // Bottom
        newHeight = Math.max(30, resizeStart.height + deltaY);
        break;
      case 'w': // Left
        newWidth = Math.max(50, resizeStart.width - deltaX);
        newX = (element.x || 0) + (resizeStart.width - newWidth);
        break;
      case 'e': // Right
        newWidth = Math.max(50, resizeStart.width + deltaX);
        break;
    }

    // Update both size and position if needed
    updateElementSize(elementId, newWidth, newHeight);
    if (newX !== (element.x || 0) || newY !== (element.y || 0)) {
      updateElementPosition(elementId, newX, newY);
    }
  };

  const handleResizeEnd = () => {
    setIsResizing(false);
  };

  useEffect(() => {
    if (isResizing) {
      document.addEventListener('mousemove', handleResizeMove);
      document.addEventListener('mouseup', handleResizeEnd);
      return () => {
        document.removeEventListener('mousemove', handleResizeMove);
        document.removeEventListener('mouseup', handleResizeEnd);
      };
    }
  }, [isResizing, resizeStart]);



  // Evaluate expressions for dynamic behavior
  const expressionContext: ExpressionContext = useMemo(() => ({
    data: samplePayload,
    element: element.props,
    index: undefined, // TODO: Add loop context
    parent: undefined, // TODO: Add parent context
  }), [samplePayload, element.props]);

  const expressionResults = useMemo(() => {
    const results = {
      isVisible: true,
      isEnabled: true,
      computedValue: undefined as any,
      computedStyles: {} as Record<string, any>,
    };

    if (element.expression) {
      const safeMode = element.expression.safeExpressionMode !== false;

      // Evaluate visibility expression
      if (element.expression.visibleWhen) {
        const result = evaluateExpression(element.expression.visibleWhen, expressionContext, { safeMode });
        results.isVisible = result.isValid ? Boolean(result.value) : true;
      }

      // Evaluate enabled expression (for interactive elements)
      if (element.expression.enabledWhen && (element.type === 'Button' || element.type === 'Checkbox' || element.type === 'Radio')) {
        const result = evaluateExpression(element.expression.enabledWhen, expressionContext, { safeMode });
        results.isEnabled = result.isValid ? Boolean(result.value) : true;
      }

      // Evaluate value expression
      if (element.expression.valueExpression) {
        const result = evaluateExpression(element.expression.valueExpression, expressionContext, { safeMode });
        if (result.isValid) {
          results.computedValue = result.value;
        }
      }

      // Evaluate style expressions
      if (element.expression.styleExpression) {
        const computedStyles: Record<string, any> = {};
        Object.entries(element.expression.styleExpression).forEach(([styleProp, expression]) => {
          if (expression && typeof expression === 'string') {
            const result = evaluateExpression(expression, expressionContext, { safeMode });
            if (result.isValid) {
              computedStyles[styleProp] = result.value;
            }
          }
        });
        results.computedStyles = computedStyles;
      }
    }

    return results;
  }, [element.expression, expressionContext]);

  // Skip rendering if element should not be visible
  if (!expressionResults.isVisible && !documentView) {
    return null;
  }

  // Get preview errors for this element
  const elementErrors = previewErrors.filter(error => error.elementId === elementId);

  // Helper function to render element content
  const renderElementContent = (isDataPreview: boolean) => {
    if (element.type === 'Text') {
      const textStyle: Record<string, any> = {
        fontSize: element.props.fontSize,
        fontFamily: element.props.fontFamily || 'var(--ui-font-sans)',
        color: element.props.color || 'var(--ui-color-text-primary)',
        fontWeight: element.props.fontWeight || 'normal',
        fontStyle: element.props.fontStyle || 'normal',
        textAlign: element.props.textAlign || 'left',
        backgroundColor: element.props.backgroundColor || 'transparent',
        padding: element.props.backgroundColor && element.props.backgroundColor !== 'transparent' ? '4px' : '0',
        borderRadius: element.props.backgroundColor && element.props.backgroundColor !== 'transparent' ? '2px' : '0',
      };

      // Use computed value from expression, then binding, then static text
      const displayText = expressionResults.computedValue !== undefined
        ? expressionResults.computedValue
        : element.binding?.dataPath
        ? resolveBindingValue(element.binding.dataPath, samplePayload, element.props.text || element.binding.fallbackValue || 'Text')
        : element.props.text;

      // Merge static styles with computed styles
      const finalTextStyle = {
        ...textStyle,
        ...expressionResults.computedStyles,
      };

      // Apply text overflow behavior
      if (element.overflow?.textOverflow) {
        switch (element.overflow.textOverflow) {
          case 'clip':
            finalTextStyle.overflow = 'hidden';
            finalTextStyle.textOverflow = 'clip';
            finalTextStyle.whiteSpace = 'nowrap';
            break;
          case 'ellipsis':
            finalTextStyle.overflow = 'hidden';
            finalTextStyle.textOverflow = 'ellipsis';
            finalTextStyle.whiteSpace = 'nowrap';
            break;
          case 'shrink':
            // Font size shrinking would be implemented with a more complex algorithm
            // For now, we'll just set a minimum font size
            finalTextStyle.fontSize = Math.min(element.props.fontSize || 16, 12);
            break;
          case 'wrap':
          default:
            finalTextStyle.whiteSpace = 'normal';
            finalTextStyle.wordWrap = 'break-word';
            break;
        }
      }

      // Apply max lines and line clamping
      if (element.overflow?.maxLines && element.overflow.maxLines > 1) {
        finalTextStyle.display = '-webkit-box';
        finalTextStyle.WebkitLineClamp = element.overflow.maxLines;
        finalTextStyle.WebkitBoxOrient = 'vertical';
        finalTextStyle.overflow = 'hidden';
      }

      if (element.overflow?.lineClamp) {
        finalTextStyle.display = '-webkit-box';
        finalTextStyle.WebkitBoxOrient = 'vertical';
        finalTextStyle.overflow = 'hidden';
      }

      return (
        <span style={finalTextStyle}>
          {displayText}
        </span>
      );
    } else if (element.type === 'Column') {
      return (
        <div className="ui-element-column">
          {(element.children || []).map((childId) => (
          <ElementRenderer key={childId} elementId={childId} documentView={documentView} />
          ))}
        </div>
      );
  } else if (element.type === 'Table') {
    const { rows, columns, data, borderColor, backgroundColor } = element.props;

    // Get table data from binding or static data
    let tableData = data;
    if (element.table?.tableDataPath) {
      const resolvedData = resolveBindingValue(element.table.tableDataPath, samplePayload, []);
      if (Array.isArray(resolvedData)) {
        tableData = resolvedData;
      }
    }

    // Apply min/max rows constraints
    let displayRows = tableData?.length || 0;
    if (element.table?.minRows && displayRows < element.table.minRows) {
      displayRows = element.table.minRows;
    }
    if (element.table?.maxRows && displayRows > element.table.maxRows) {
      displayRows = element.table.maxRows;
    }

    // Handle empty state
    if (displayRows === 0 && element.table?.emptyRowsPolicy === 'hide-table') {
      return null;
    }

    const tableStyle = {
      ['--ui-element-table-bg' as any]: backgroundColor || 'var(--ui-color-bg-panel)',
      ['--ui-element-table-border' as any]: borderColor || 'var(--ui-color-border-strong)',
    };

    return (
      <table className="ui-element-table" style={tableStyle}>
        {element.table?.showHeader !== false && (
          <thead>
            <tr>
              {element.table?.columns?.map((column, colIdx) => (
                <th
                  key={colIdx}
                  className="ui-element-table-header"
                  style={{
                    width: column.width || 'auto',
                    textAlign: column.alignment || 'left',
                    backgroundColor: element.table?.headerStyle?.backgroundColor || '#f5f5f5'
                  }}
                >
                  {column.header || `Column ${colIdx + 1}`}
                </th>
              )) || Array.from({ length: columns }).map((_, colIdx) => (
                <th key={colIdx} className="ui-element-table-header">
                  Column {colIdx + 1}
                </th>
              ))}
            </tr>
          </thead>
        )}
        <tbody>
          {Array.from({ length: displayRows }).map((_, rowIdx) => {
            const rowData = tableData?.[rowIdx];
            const isEvenRow = rowIdx % 2 === 0;

            // Apply row striping
            let rowBackgroundColor = element.table?.rowStyle?.backgroundColor || '#ffffff';
            if (element.table?.rowStriping?.enabled) {
              if (isEvenRow && element.table.rowStriping.evenRowStyle?.backgroundColor) {
                rowBackgroundColor = element.table.rowStriping.evenRowStyle.backgroundColor;
              } else if (!isEvenRow && element.table.rowStriping.oddRowStyle?.backgroundColor) {
                rowBackgroundColor = element.table.rowStriping.oddRowStyle.backgroundColor;
              }
            }

            // Check conditional row styles
            if (element.table?.conditionalRowStyles) {
              for (const conditionalStyle of element.table.conditionalRowStyles) {
                if (conditionalStyle.condition && rowData) {
                  // Simple evaluation for now - in real implementation would use expression engine
                  try {
                    // This is a simplified check - real implementation would evaluate expressions
                    if (conditionalStyle.condition.includes('true')) {
                      rowBackgroundColor = conditionalStyle.style.backgroundColor || rowBackgroundColor;
                    }
                  } catch (e) {
                    // Ignore evaluation errors
                  }
                }
              }
            }

            return (
              <tr key={rowIdx} style={{ backgroundColor: rowBackgroundColor }}>
                {element.table?.columns?.map((column, colIdx) => {
                  let cellValue = '';

                  if (rowData) {
                    // Use column data path if specified
                    if (column.dataPath) {
                      cellValue = resolveBindingValue(column.dataPath, { item: rowData }, '');
                    } else {
                      // Fallback to array index
                      cellValue = rowData[colIdx] || '';
                    }

                    // Apply formatter if specified
                    if (column.formatter && cellValue) {
                      // This would use the formatter engine in a real implementation
                      // For now, just show the value
                    }
                  } else if (element.table?.emptyRowsPolicy === 'show-placeholder-text' && rowIdx === 0) {
                    cellValue = element.table.emptyRowText || 'No data available';
                  }

                  return (
                    <td
                      key={colIdx}
                      className="ui-element-table-cell"
                      style={{
                        textAlign: column.alignment || 'left',
                        width: column.width || 'auto'
                      }}
                    >
                      {cellValue}
                    </td>
                  );
                }) || Array.from({ length: columns }).map((_, colIdx) => (
                  <td key={colIdx} className="ui-element-table-cell">
                    {tableData && tableData[rowIdx] && tableData[rowIdx][colIdx] ? tableData[rowIdx][colIdx] : ''}
                  </td>
                ))}
              </tr>
            );
          })}
        </tbody>
      </table>
    );
  } else if (element.type === 'Image') {
    const { src, width, height, alt } = element.props;

    // Apply image-specific styling based on image configuration
    const imageStyle: Record<string, any> = {
      width: width || 'auto',
      height: height || 'auto',
      objectFit: element.image?.imageFit || 'contain',
    };

    // Apply focal point for object-position when using cover or contain
    if (element.image?.focalPoint && (element.image.imageFit === 'cover' || element.image.imageFit === 'contain')) {
      const focalX = element.image.focalPoint.x * 100;
      const focalY = element.image.focalPoint.y * 100;
      imageStyle.objectPosition = `${focalX}% ${focalY}%`;
    }

    // Handle placeholder and fallback
    let imageSrc = src;
    let imageAlt = alt || 'Image';

    if (element.image?.placeholder) {
      // For now, we'll handle placeholder in CSS/styling
      // In a real implementation, this would involve loading states and error handling
      if (element.image.placeholder.type === 'color') {
        imageStyle.backgroundColor = element.image.placeholder.backgroundColor || '#f3f4f6';
      }
    }

    return <img src={imageSrc} alt={imageAlt} className="ui-element-image" style={imageStyle} />;
  } else if (element.type === 'Rectangle') {
    const { width, height, fillColor, strokeColor, strokeWidth, borderRadius } = element.props;
    const rectangleStyle = {
      width: width || 200,
      height: height || 100,
      backgroundColor: fillColor || 'var(--ui-color-bg-panel)',
      border: `${strokeWidth || 1}px solid ${strokeColor || 'var(--ui-color-text-primary)'}`,
      borderRadius: borderRadius || 0,
    };

    return (
      <div style={rectangleStyle} />
    );
  } else if (element.type === 'Circle') {
    const { radius, fillColor, strokeColor, strokeWidth } = element.props;
    const diameter = (radius || 50) * 2;
    const circleStyle = {
      width: diameter,
      height: diameter,
      backgroundColor: fillColor || 'var(--ui-color-bg-panel)',
      border: `${strokeWidth || 1}px solid ${strokeColor || 'var(--ui-color-text-primary)'}`,
      borderRadius: '50%',
    } as const;

    return (
      <div style={circleStyle} />
    );
  } else if (element.type === 'Line') {
    const { x1, y1, x2, y2, strokeColor, strokeWidth, lineCap } = element.props;
    const width = Math.abs((x2 || 100) - (x1 || 0));
    const height = Math.abs((y2 || 100) - (y1 || 0));
    return (
      <svg width={width || 100} height={height || 100} className="ui-element-line-svg">
        <line
          x1={x1 || 0}
          y1={y1 || 0}
          x2={x2 || 100}
          y2={y2 || 100}
          stroke={strokeColor || 'var(--ui-color-text-primary)'}
          strokeWidth={strokeWidth || 2}
          strokeLinecap={lineCap || 'butt'}
        />
      </svg>
    );
  } else if (element.type === 'Link') {
    const { url, text, width, height } = element.props;
    const linkStyle = {
      width: width || 100,
      height: height || 30,
      lineHeight: `${(height || 30) - 8}px`,
    };

    return (
      <a
        className="ui-element-link"
        href={url || '#'}
        target="_blank"
        rel="noopener noreferrer"
        style={linkStyle}
      >
        {text || 'Link'}
      </a>
    );
  } else if (element.type === 'List') {
    const { items, ordered, markerStyle } = element.props;
    const ListTag = ordered ? 'ol' : 'ul';
    const listStyleClass = ordered
      ? 'ui-element-list--decimal'
      : `ui-element-list--${markerStyle || 'disc'}`;

    return (
      <ListTag
        className={`ui-element-list ${listStyleClass}`}
      >
        {(items || []).map((item: string, index: number) => (
          <li key={index} className="ui-element-list-item">
            {item}
          </li>
        ))}
      </ListTag>
    );
  } else if (element.type === 'PageBreak') {
    const { style, color } = element.props;
    const pageBreakStyle = {
      ['--ui-element-page-break-color' as any]: color || 'var(--ui-color-danger)',
      borderStyle: style || 'dashed',
    };

    return (
      <div className="ui-element-page-break" style={pageBreakStyle}>
        <div className="ui-element-page-break-label">
          📄 PAGE BREAK
        </div>
      </div>
    );
  } else if (element.type === 'Grid') {
    const { rows, columns, gap, justifyContent, alignItems } = element.props;
    const gridStyle = {
      gridTemplateRows: `repeat(${rows || 2}, 1fr)`,
      gridTemplateColumns: `repeat(${columns || 3}, 1fr)`,
      gap: gap || 10,
      justifyContent: justifyContent || 'start',
      alignItems: alignItems || 'start',
    };

    return (
      <div className="ui-element-grid" style={gridStyle}>
        {(element.children || []).map((childId) => (
          <ElementRenderer key={childId} elementId={childId} documentView={documentView} />
        ))}
      </div>
    );
  } else if (element.type === 'Spacer') {
    const { width, height, flexGrow } = element.props;
    const spacerStyle = {
      width: width || 100,
      height: height || 20,
      flexGrow: flexGrow || 0,
    };

    return (
      <div className="ui-element-spacer" style={spacerStyle}>
        Spacer
      </div>
    );
  } else if (element.type === 'Button') {
    const { text, style } = element.props;
    const buttonClassName = style === 'secondary'
      ? 'ui-element-button ui-element-button-secondary'
      : 'ui-element-button ui-element-button-primary';

    return (
      <button
        className={buttonClassName}
        onClick={(e) => {
          e.preventDefault();
          // Button action would be handled here in a real application
          console.log('Button clicked:', text);
        }}
      >
        {text || 'Button'}
      </button>
    );
  } else if (element.type === 'Checkbox') {
    const { label, checked } = element.props;
    return (
      <label className="ui-element-choice-label">
        <input
          type="checkbox"
          checked={checked || false}
          onChange={(e) => {
            // Checkbox state would be handled here in a real application
            console.log('Checkbox changed:', label, e.target.checked);
          }}
          className="ui-element-choice-input"
        />
        {label || 'Checkbox'}
      </label>
    );
   } else if (element.type === 'Radio') {
     const { label, checked, groupName } = element.props;
     return (
       <label className="ui-element-choice-label">
         <input
           type="radio"
           name={groupName || 'radio-group'}
           checked={checked || false}
           onChange={(e) => {
             // Radio state would be handled here in a real application
             console.log('Radio changed:', label, e.target.checked);
           }}
           className="ui-element-choice-input"
         />
         {label || 'Radio Button'}
       </label>
     );
   } else if (element.type === 'QRCode') {
     const { value, size, eccLevel, quietZone } = element.props;
     const qrSize = size || 100;
     const qrValue = value || 'https://example.com';
     // For now, show a placeholder. In a real implementation, you'd use a QR code library
     return (
       <div
         className="ui-element-qrcode"
         style={{
           width: qrSize,
           height: qrSize,
           border: '1px solid #ccc',
           display: 'flex',
           alignItems: 'center',
           justifyContent: 'center',
           fontSize: '12px',
           color: '#666'
         }}
       >
         QR: {qrValue.length > 20 ? qrValue.substring(0, 20) + '...' : qrValue}
       </div>
     );
   } else if (element.type === 'Barcode') {
     const { value, symbology, width, height, checksum } = element.props;
     const barcodeValue = value || '123456789';
     // For now, show a placeholder. In a real implementation, you'd use a barcode library
     return (
       <div
         className="ui-element-barcode"
         style={{
           width: width || 200,
           height: height || 60,
           border: '1px solid #ccc',
           display: 'flex',
           alignItems: 'center',
           justifyContent: 'center',
           fontSize: '12px',
           color: '#666'
         }}
       >
         Barcode: {barcodeValue}
       </div>
     );
   } else if (element.type === 'Signature') {
     const { label, signerNamePath, datePath, imagePath } = element.props;
     return (
       <div className="ui-element-signature">
         <div style={{ borderBottom: '1px solid #000', padding: '10px', minHeight: '60px' }}>
           {label || 'Signature'}
         </div>
         <div style={{ fontSize: '12px', color: '#666', marginTop: '5px' }}>
           Sign here
         </div>
       </div>
     );
   } else if (element.type === 'RichText') {
     const { html, styleProfile } = element.props;
     const richTextHtml = html || '<p>Rich text content</p>';
     return (
       <div
         className="ui-element-richtext"
         dangerouslySetInnerHTML={{ __html: richTextHtml }}
         style={{
           padding: '10px',
           border: '1px solid #ccc',
           minHeight: '50px'
         }}
       />
     );
   }
     return null;
   };

  // Memoize content rendering to prevent unnecessary re-renders
  const content = useMemo(() => {
    // Error preview mode - show validation errors
    if (previewMode === 'error' && elementErrors.length > 0) {
      return (
        <div className="ui-preview-error-overlay">
          <div className="ui-preview-error-icon">⚠️</div>
          <div className="ui-preview-error-messages">
            {elementErrors.map((error, index) => (
              <div key={index} className={`ui-preview-error-message ui-preview-error-${error.severity}`}>
                {error.message}
              </div>
            ))}
          </div>
        </div>
      );
    }

    // Data preview mode - show resolved values with visual indicators
    if (previewMode === 'data') {
      // Add data binding indicators
      const hasBinding = element.binding?.dataPath;
      const hasExpression = element.expression?.valueExpression || element.expression?.visibleWhen;
      const bindingResolved = hasBinding ? resolveBindingValue(element.binding!.dataPath!, samplePayload, null) !== null : false;

      return (
        <div className="ui-preview-data-container">
          {/* Data binding indicators */}
          {(hasBinding || hasExpression) && (
            <div className="ui-preview-data-indicators">
              {hasBinding && (
                <div className={`ui-preview-data-indicator ${bindingResolved ? 'resolved' : 'unresolved'}`}>
                  📊 {bindingResolved ? '✓' : '✗'} {element.binding!.dataPath}
                </div>
              )}
              {hasExpression && (
                <div className="ui-preview-data-indicator expression">
                  🔧 Expression active
                </div>
              )}
            </div>
          )}

          {/* Render normal content but with data resolution */}
          <div className="ui-preview-data-content">
            {renderElementContent(true)}
          </div>
        </div>
      );
    }

    // Design mode - show placeholders and edit controls
    return renderElementContent(false);
  }, [element.type, element.props, element.children, element.binding, element.expression, samplePayload, previewMode, elementErrors, expressionResults]);

  return (
    <div
      ref={setNodeRef}
      className={`canvas-element${isSelected ? ' selected' : ''}${element.locked ? ' locked' : ''}`}
      onClick={documentView ? undefined : handleClick}
      onKeyDown={documentView ? undefined : handleKeyDown}
      style={containerStyle}
      {...(documentView ? {} : { ...listeners, ...attributes })}
      role="button"
      tabIndex={documentView ? -1 : 0}
      aria-label={elementLabel}
      aria-selected={isSelected}
      aria-disabled={element.locked}
      aria-describedby={`element-${elementId}-description`}
    >
      {content}
      {!documentView && element.locked && (
        <div
          className="ui-element-lock-badge"
          title="Element is locked"
        >
          🔒
        </div>
      )}
      {!documentView && isSelected && !element.locked && (
        <>
          {/* Corner resize handles */}
          <div
            className="resize-handle resize-handle-corner resize-handle-nw"
            onMouseDown={(e) => {
              console.log('NW handle clicked');
              e.stopPropagation();
              e.preventDefault();
              handleResizeStart(e, 'nw');
            }}
            title="Resize from top-left"
          />
          <div
            className="resize-handle resize-handle-corner resize-handle-ne"
            onMouseDown={(e) => {
              console.log('NE handle clicked');
              e.stopPropagation();
              e.preventDefault();
              handleResizeStart(e, 'ne');
            }}
            title="Resize from top-right"
          />
          <div
            className="resize-handle resize-handle-corner resize-handle-sw"
            onMouseDown={(e) => {
              console.log('SW handle clicked');
              e.stopPropagation();
              e.preventDefault();
              handleResizeStart(e, 'sw');
            }}
            title="Resize from bottom-left"
          />
          <div
            className="resize-handle resize-handle-corner resize-handle-se"
            onMouseDown={(e) => {
              console.log('SE handle clicked');
              e.stopPropagation();
              e.preventDefault();
              handleResizeStart(e, 'se');
            }}
            title="Resize from bottom-right"
          />

          {/* Edge resize handles */}
          <div
            className="resize-handle resize-handle-edge resize-handle-n"
            onMouseDown={(e) => {
              console.log('N handle clicked');
              e.stopPropagation();
              e.preventDefault();
              handleResizeStart(e, 'n');
            }}
            title="Resize from top"
          />
          <div
            className="resize-handle resize-handle-edge resize-handle-s"
            onMouseDown={(e) => {
              console.log('S handle clicked');
              e.stopPropagation();
              e.preventDefault();
              handleResizeStart(e, 's');
            }}
            title="Resize from bottom"
          />
          <div
            className="resize-handle resize-handle-edge resize-handle-w"
            onMouseDown={(e) => {
              console.log('W handle clicked');
              e.stopPropagation();
              e.preventDefault();
              handleResizeStart(e, 'w');
            }}
            title="Resize from left"
          />
          <div
            className="resize-handle resize-handle-edge resize-handle-e"
            onMouseDown={(e) => {
              console.log('E handle clicked');
              e.stopPropagation();
              e.preventDefault();
              handleResizeStart(e, 'e');
            }}
            title="Resize from right"
          />
        </>
      )}
    </div>
  );
});

ElementRenderer.displayName = 'ElementRenderer';

export default ElementRenderer;
