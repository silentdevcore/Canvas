import React, { useState, useCallback } from 'react';
import { useDesignerStore } from './store';

interface DataNode {
  key: string;
  path: string;
  value: any;
  type: 'object' | 'array' | 'primitive';
  children?: DataNode[];
  expanded?: boolean;
}

const DataPanel: React.FC = () => {
  const { samplePayload, updateSamplePayload } = useDesignerStore();
  const [jsonText, setJsonText] = useState(JSON.stringify(samplePayload, null, 2));
  const [isValid, setIsValid] = useState(true);
  const [error, setError] = useState('');
  const [dataTree, setDataTree] = useState<DataNode[]>([]);
  const [selectedPath, setSelectedPath] = useState<string>('');

  const buildDataTree = useCallback((obj: any, path = '', expanded = true): DataNode[] => {
    if (obj === null || obj === undefined) return [];

    if (typeof obj === 'object' && !Array.isArray(obj)) {
      // Object
      return Object.entries(obj).map(([key, value]) => ({
        key,
        path: path ? `${path}.${key}` : key,
        value,
        type: 'object' as const,
        expanded,
        children: buildDataTree(value, path ? `${path}.${key}` : key, false)
      }));
    } else if (Array.isArray(obj)) {
      // Array
      return obj.map((item, index) => ({
        key: `[${index}]`,
        path: `${path}[${index}]`,
        value: item,
        type: 'array' as const,
        expanded: false,
        children: typeof item === 'object' ? buildDataTree(item, `${path}[${index}]`, false) : undefined
      }));
    } else {
      // Primitive
      return [{
        key: path.split('.').pop() || path,
        path,
        value: obj,
        type: 'primitive' as const
      }];
    }
  }, []);

  const handlePathClick = useCallback((path: string) => {
    setSelectedPath(path);
    // Copy path to clipboard for easy insertion
    navigator.clipboard.writeText(path).catch(() => {
      // Fallback: could show a tooltip or something
    });
  }, []);

  const toggleNodeExpansion = useCallback((nodePath: string) => {
    setDataTree(prevTree =>
      prevTree.map(node => {
        if (node.path === nodePath) {
          return { ...node, expanded: !node.expanded };
        }
        return node;
      })
    );
  }, []);

  const handleJsonChange = (value: string) => {
    setJsonText(value);

    try {
      const parsed = JSON.parse(value);
      updateSamplePayload(parsed);
      setIsValid(true);
      setError('');
    } catch (err) {
      setIsValid(false);
      setError(err instanceof Error ? err.message : 'Invalid JSON');
    }
  };

  const formatJson = () => {
    try {
      const parsed = JSON.parse(jsonText);
      const formatted = JSON.stringify(parsed, null, 2);
      setJsonText(formatted);
      updateSamplePayload(parsed);
      setIsValid(true);
      setError('');
    } catch (err) {
      setError('Cannot format invalid JSON');
    }
  };

  const resetToDefault = () => {
    const defaultPayload = {
      customer: {
        name: 'John Doe',
        email: 'john@example.com',
        address: {
          street: '123 Main St',
          city: 'Anytown',
          zipCode: '12345'
        }
      },
      order: {
        id: 'ORD-12345',
        date: '2024-01-15',
        items: [
          { name: 'Widget A', quantity: 2, price: 29.99 },
          { name: 'Widget B', quantity: 1, price: 49.99 }
        ],
        total: 109.97
      }
    };
    const formatted = JSON.stringify(defaultPayload, null, 2);
    setJsonText(formatted);
    updateSamplePayload(defaultPayload);
    setDataTree(buildDataTree(defaultPayload));
    setIsValid(true);
    setError('');
  };

  // Update data tree when sample payload changes
  React.useEffect(() => {
    if (isValid) {
      setDataTree(buildDataTree(samplePayload));
    }
  }, [samplePayload, isValid, buildDataTree]);

  const renderDataNode = (node: DataNode, depth = 0): React.ReactNode => {
    const hasChildren = node.children && node.children.length > 0;
    const isExpanded = node.expanded !== false;
    const isSelected = selectedPath === node.path;

    return (
      <div key={node.path} className="ui-data-node" style={{ paddingLeft: `${depth * 16}px` }}>
        <div
          className={`ui-data-node-header ${isSelected ? 'ui-data-node-selected' : ''}`}
          onClick={() => handlePathClick(node.path)}
        >
          {hasChildren && (
            <button
              className="ui-data-node-toggle"
              onClick={(e) => {
                e.stopPropagation();
                toggleNodeExpansion(node.path);
              }}
            >
              {isExpanded ? '▼' : '▶'}
            </button>
          )}
          <span className="ui-data-node-key">{node.key}</span>
          <span className="ui-data-node-type">({node.type})</span>
          <span className="ui-data-node-value">
            {node.type === 'primitive' ? JSON.stringify(node.value) : ''}
          </span>
          <button
            className="ui-data-node-copy"
            onClick={(e) => {
              e.stopPropagation();
              handlePathClick(node.path);
            }}
            title="Copy path to clipboard"
          >
            📋
          </button>
        </div>
        {hasChildren && isExpanded && (
          <div className="ui-data-node-children">
            {node.children!.map(child => renderDataNode(child, depth + 1))}
          </div>
        )}
      </div>
    );
  };

  return (
    <aside className="data-panel">
      <h2>Sample Data</h2>
      <div className="ui-data-panel-content">
        <div className="ui-data-panel-header">
          <div className="ui-data-panel-actions">
            <button
              onClick={formatJson}
              className="ui-button ui-button-secondary ui-button-small"
              disabled={!isValid}
            >
              Format JSON
            </button>
            <button
              onClick={resetToDefault}
              className="ui-button ui-button-outline ui-button-small"
            >
              Reset to Default
            </button>
          </div>
          {!isValid && (
            <div className="ui-data-panel-error">
              <span className="ui-error-icon">⚠️</span>
              {error}
            </div>
          )}
        </div>

        <div className="ui-data-panel-editor">
          <textarea
            value={jsonText}
            onChange={(e) => handleJsonChange(e.target.value)}
            className={`ui-data-panel-textarea ${!isValid ? 'ui-data-panel-textarea-error' : ''}`}
            placeholder="Enter sample JSON data for binding preview..."
            spellCheck={false}
          />
        </div>

        {isValid && dataTree.length > 0 && (
          <div className="ui-data-panel-browser">
            <div className="ui-data-panel-browser-header">
              <h3>Data Paths</h3>
              {selectedPath && (
                <div className="ui-selected-path">
                  <span className="ui-selected-path-label">Selected:</span>
                  <code className="ui-selected-path-value">{selectedPath}</code>
                  <span className="ui-selected-path-copied">📋 Copied to clipboard</span>
                </div>
              )}
            </div>
            <div className="ui-data-panel-tree">
              {dataTree.map(node => renderDataNode(node))}
            </div>
          </div>
        )}

        <div className="ui-data-panel-info">
          <div className="ui-note">
            <div className="ui-note-title">Data Binding</div>
            <div className="ui-note-text">
              Click on any data path above to copy it to your clipboard. Use these paths in element bindings
              (e.g., customer.name, order.total). This sample data is used for preview mode to show how your
              template will look with real data.
            </div>
          </div>
        </div>
      </div>
    </aside>
  );
};

export default DataPanel;