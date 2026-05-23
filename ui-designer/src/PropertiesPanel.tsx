import React from 'react';
import { useDesignerStore } from './store';
import Tooltip from './Tooltip';
import Input from './Input';
import { isElementBindable } from './domain/value-objects/ElementType';
import { validateExpression } from './template/expressionEngine';
import { validateRepeatConfig } from './template/repeatExpander';

const PropertiesPanel: React.FC = () => {
  const { selectedIds, elements, updateElementProps, updateElementBinding, updateElementExpression, updateElementRepeat, updateElementOverflow, updateElementImage, updateElementTable, updateElementValidation, updateTemplateMetadata, templateMetadata, toggleElementLock, uploadImage, addToast } = useDesignerStore();

  return (
    <aside className="properties-panel">
      <h2>Properties</h2>

      {/* Template Metadata Section - Always visible */}
      <div className="ui-properties-section">
        <h3 className="ui-properties-section-title">Template Metadata</h3>
        <div className="ui-properties-stack-sm">
          <label>
            Template Name:
            <input
              name="templateName"
              value={templateMetadata.name || ''}
              onChange={(e) => updateTemplateMetadata({
                name: e.target.value
              })}
              type="text"
              placeholder="My Template"
            />
          </label>

          <label>
            Description:
            <textarea
              name="templateDescription"
              value={templateMetadata.description || ''}
              onChange={(e) => updateTemplateMetadata({
                description: e.target.value
              })}
              rows={2}
              placeholder="Template description"
            />
          </label>

          <div className="ui-properties-grid-2">
            <label>
              Category:
              <select
                name="templateCategory"
                value={templateMetadata.category || 'General'}
                onChange={(e) => updateTemplateMetadata({
                  category: e.target.value
                })}
              >
                <option value="General">General</option>
                <option value="Business">Business</option>
                <option value="Finance">Finance</option>
                <option value="Legal">Legal</option>
                <option value="Marketing">Marketing</option>
                <option value="Education">Education</option>
                <option value="Healthcare">Healthcare</option>
                <option value="Custom">Custom</option>
              </select>
            </label>
            <label>
              Version:
              <input
                name="templateVersion"
                value={templateMetadata.version || '1.0.0'}
                onChange={(e) => updateTemplateMetadata({
                  version: e.target.value
                })}
                type="text"
                placeholder="1.0.0"
              />
            </label>
          </div>

          <label>
            Tags:
            <input
              name="templateTags"
              value={templateMetadata.tags?.join(', ') || ''}
              onChange={(e) => updateTemplateMetadata({
                tags: e.target.value.split(',').map(tag => tag.trim()).filter(tag => tag)
              })}
              type="text"
              placeholder="invoice, business, finance"
            />
            <small className="ui-properties-hint">
              Comma-separated tags for organization
            </small>
          </label>

          <div className="ui-properties-grid-2">
            <label>
              Locale:
              <select
                name="templateLocale"
                value={templateMetadata.locale || 'en-US'}
                onChange={(e) => updateTemplateMetadata({
                  locale: e.target.value
                })}
              >
                <option value="en-US">English (US)</option>
                <option value="en-GB">English (UK)</option>
                <option value="es-ES">Spanish</option>
                <option value="fr-FR">French</option>
                <option value="de-DE">German</option>
                <option value="it-IT">Italian</option>
                <option value="pt-BR">Portuguese (BR)</option>
                <option value="ja-JP">Japanese</option>
                <option value="zh-CN">Chinese (Simplified)</option>
                <option value="ko-KR">Korean</option>
              </select>
            </label>
            <label>
              Currency:
              <select
                name="templateCurrency"
                value={templateMetadata.currency || 'USD'}
                onChange={(e) => updateTemplateMetadata({
                  currency: e.target.value
                })}
              >
                <option value="USD">USD ($)</option>
                <option value="EUR">EUR (€)</option>
                <option value="GBP">GBP (£)</option>
                <option value="JPY">JPY (¥)</option>
                <option value="CAD">CAD (C$)</option>
                <option value="AUD">AUD (A$)</option>
                <option value="CHF">CHF (Fr)</option>
                <option value="CNY">CNY (¥)</option>
                <option value="SEK">SEK (kr)</option>
                <option value="NZD">NZD (NZ$)</option>
              </select>
            </label>
          </div>

          <div className="ui-properties-grid-2">
            <label>
              Timezone:
              <select
                name="templateTimezone"
                value={templateMetadata.timezone || 'UTC'}
                onChange={(e) => updateTemplateMetadata({
                  timezone: e.target.value
                })}
              >
                <option value="UTC">UTC</option>
                <option value="America/New_York">Eastern Time</option>
                <option value="America/Chicago">Central Time</option>
                <option value="America/Denver">Mountain Time</option>
                <option value="America/Los_Angeles">Pacific Time</option>
                <option value="Europe/London">London</option>
                <option value="Europe/Paris">Paris</option>
                <option value="Europe/Berlin">Berlin</option>
                <option value="Asia/Tokyo">Tokyo</option>
                <option value="Asia/Shanghai">Shanghai</option>
                <option value="Australia/Sydney">Sydney</option>
              </select>
            </label>
            <label>
              Public Template:
              <input
                name="templateIsPublic"
                checked={templateMetadata.isPublic || false}
                onChange={(e) => updateTemplateMetadata({
                  isPublic: e.target.checked
                })}
                type="checkbox"
              />
            </label>
          </div>

          <div className="ui-properties-subsection">
            <h4 className="ui-properties-subsection-title">Formatting Profile</h4>
            <div className="ui-properties-grid-2">
              <label>
                Date Format:
                <select
                  name="dateFormat"
                  value={templateMetadata.formattingProfile?.dateFormat || 'MM/DD/YYYY'}
                  onChange={(e) => updateTemplateMetadata({
                    formattingProfile: {
                      ...templateMetadata.formattingProfile,
                      dateFormat: e.target.value
                    }
                  })}
                >
                  <option value="MM/DD/YYYY">MM/DD/YYYY</option>
                  <option value="DD/MM/YYYY">DD/MM/YYYY</option>
                  <option value="YYYY-MM-DD">YYYY-MM-DD</option>
                  <option value="DD MMM YYYY">DD MMM YYYY</option>
                  <option value="MMM DD, YYYY">MMM DD, YYYY</option>
                </select>
              </label>
              <label>
                Time Format:
                <select
                  name="timeFormat"
                  value={templateMetadata.formattingProfile?.timeFormat || 'HH:mm:ss'}
                  onChange={(e) => updateTemplateMetadata({
                    formattingProfile: {
                      ...templateMetadata.formattingProfile,
                      timeFormat: e.target.value
                    }
                  })}
                >
                  <option value="HH:mm:ss">24-hour (HH:mm:ss)</option>
                  <option value="hh:mm:ss A">12-hour (hh:mm:ss AM/PM)</option>
                  <option value="HH:mm">24-hour (HH:mm)</option>
                  <option value="hh:mm A">12-hour (hh:mm AM/PM)</option>
                </select>
              </label>
            </div>
            <div className="ui-properties-grid-2">
              <label>
                Number Format:
                <select
                  name="numberFormat"
                  value={templateMetadata.formattingProfile?.numberFormat || 'en-US'}
                  onChange={(e) => updateTemplateMetadata({
                    formattingProfile: {
                      ...templateMetadata.formattingProfile,
                      numberFormat: e.target.value
                    }
                  })}
                >
                  <option value="en-US">US (1,234.56)</option>
                  <option value="en-GB">UK (1,234.56)</option>
                  <option value="de-DE">German (1.234,56)</option>
                  <option value="fr-FR">French (1 234,56)</option>
                  <option value="es-ES">Spanish (1.234,56)</option>
                  <option value="ja-JP">Japanese (1,234.56)</option>
                </select>
              </label>
              <label>
                Currency Format:
                <select
                  name="currencyFormat"
                  value={templateMetadata.formattingProfile?.currencyFormat || 'USD'}
                  onChange={(e) => updateTemplateMetadata({
                    formattingProfile: {
                      ...templateMetadata.formattingProfile,
                      currencyFormat: e.target.value
                    }
                  })}
                >
                  <option value="USD">USD ($1,234.56)</option>
                  <option value="EUR">EUR (€1,234.56)</option>
                  <option value="GBP">GBP (£1,234.56)</option>
                  <option value="JPY">JPY (¥1,234)</option>
                  <option value="CAD">CAD (C$1,234.56)</option>
                </select>
              </label>
            </div>
          </div>

          <div className="ui-properties-subsection">
            <h4 className="ui-properties-subsection-title">Template Info</h4>
            <div className="ui-properties-info-grid">
              <div className="ui-properties-info-item">
                <span className="ui-properties-info-label">ID:</span>
                <span className="ui-properties-info-value ui-properties-note-mono">{templateMetadata.id}</span>
              </div>
              <div className="ui-properties-info-item">
                <span className="ui-properties-info-label">Schema Version:</span>
                <span className="ui-properties-info-value">{templateMetadata.schemaVersion}</span>
              </div>
              <div className="ui-properties-info-item">
                <span className="ui-properties-info-label">Created:</span>
                <span className="ui-properties-info-value">{new Date(templateMetadata.createdAt || '').toLocaleString()}</span>
              </div>
              <div className="ui-properties-info-item">
                <span className="ui-properties-info-label">Updated:</span>
                <span className="ui-properties-info-value">{new Date(templateMetadata.updatedAt || '').toLocaleString()}</span>
              </div>
              <div className="ui-properties-info-item">
                <span className="ui-properties-info-label">Created By:</span>
                <span className="ui-properties-info-value">{templateMetadata.createdBy}</span>
              </div>
              <div className="ui-properties-info-item">
                <span className="ui-properties-info-label">Updated By:</span>
                <span className="ui-properties-info-value">{templateMetadata.updatedBy}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {selectedIds.length === 0 ? (
        <div className="ui-properties-empty" role="status" aria-live="polite">
          <h3 className="ui-properties-empty-title">No element selected</h3>
          <p className="ui-properties-empty-copy">Select an element on the canvas to edit text, size, spacing, and style.</p>
          <p className="ui-properties-empty-hint">Tip: Use Shift+Click to select multiple elements.</p>
        </div>
      ) : (
        <>
          {/* Element Properties - Show when element is selected */}
          {(() => {
            // For now, show properties of the first selected element
            const selectedId = selectedIds[0];
            const element = elements[selectedId];
            if (!element) return null;

            function handleChange(e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) {
              const { name, value } = e.target;
              updateElementProps(selectedId, {
                [name]: name === 'fontSize' ? parseInt(value, 10) : value,
              });
            }

            function handleTableChange(e: React.ChangeEvent<HTMLInputElement>) {
              const { name, value } = e.target;
              let v = parseInt(value, 10);
              if (isNaN(v) || v < 1) v = 1;
              let newData = element.props.data;
              if (name === 'rows') {
                newData = Array.from({ length: v }, (_, i) => newData && newData[i] ? newData[i] : Array(element.props.columns).fill(''));
              } else if (name === 'columns') {
                newData = Array.from({ length: element.props.rows }, (_, i) => Array.from({ length: v }, (__, j) => newData && newData[i] && newData[i][j] ? newData[i][j] : ''));
              }
              updateElementProps(selectedId, {
                [name]: v,
                data: newData,
              });
            }

            function handleCellChange(row: number, col: number, value: string) {
              const newData = element.props.data.map((r: string[], i: number) =>
                i === row ? r.map((c: string, j: number) => (j === col ? value : c)) : r
              );
              updateElementProps(selectedId, { data: newData });
            }

            return (
              <>
                <div className="ui-properties-header">
                  <div><strong>Type:</strong> {element.type}</div>
                  <Tooltip content={element.locked ? "Unlock element to allow editing" : "Lock element to prevent accidental moves"} disabled={!useDesignerStore.getState().showTooltips}>
                    <button
                      onClick={() => toggleElementLock(selectedId)}
                      className={`ui-lock-toggle ${element.locked ? 'is-locked' : 'is-unlocked'}`}
                    >
                      {element.locked ? '🔒' : '🔓'} {element.locked ? 'Locked' : 'Unlocked'}
                    </button>
                  </Tooltip>
                </div>
                {/* Dynamic Properties Section - Binding, Expression, Repeat */}
                {isElementBindable(element.type) && (
                  <div className="ui-properties-section">
                    <h3 className="ui-properties-section-title">Data Binding</h3>
                    <div className="ui-properties-stack-sm">
                      <label>
                        Data Path:
                        <input
                          type="text"
                          value={element.binding?.dataPath || ''}
                          onChange={(e) => updateElementBinding(selectedId, {
                            ...element.binding,
                            dataPath: e.target.value
                          })}
                          placeholder="e.g., customer.name"
                          className="ui-input"
                        />
                        <small className="ui-properties-hint">
                          Path to data in your JSON payload (e.g., customer.name, order.total)
                        </small>
                      </label>

                      <label>
                        Fallback Value:
                        <input
                          type="text"
                          value={element.binding?.fallbackValue || ''}
                          onChange={(e) => updateElementBinding(selectedId, {
                            ...element.binding,
                            fallbackValue: e.target.value
                          })}
                          placeholder="Default value if path not found"
                          className="ui-input"
                        />
                      </label>

                      <div className="ui-properties-grid-2">
                        <label>
                          Value Type:
                          <select
                            value={element.binding?.valueType || 'string'}
                            onChange={(e) => updateElementBinding(selectedId, {
                              ...element.binding,
                              valueType: e.target.value as any
                            })}
                          >
                            <option value="string">String</option>
                            <option value="number">Number</option>
                            <option value="boolean">Boolean</option>
                            <option value="date">Date</option>
                            <option value="image-url">Image URL</option>
                          </select>
                        </label>

                        <label>
                          Binding Scope:
                          <select
                            value={element.binding?.bindingScope || 'root'}
                            onChange={(e) => updateElementBinding(selectedId, {
                              ...element.binding,
                              bindingScope: e.target.value as any
                            })}
                          >
                            <option value="root">Root</option>
                            <option value="loop-item">Loop Item</option>
                            <option value="parent">Parent</option>
                          </select>
                        </label>
                      </div>

                      <label className="ui-checkbox-label">
                        <input
                          type="checkbox"
                          checked={element.binding?.required || false}
                          onChange={(e) => updateElementBinding(selectedId, {
                            ...element.binding,
                            required: e.target.checked
                          })}
                        />
                        Required field
                      </label>

                      {element.binding?.required && (
                        <label>
                          Required Message:
                          <input
                            type="text"
                            value={element.binding?.requiredMessage || ''}
                            onChange={(e) => updateElementBinding(selectedId, {
                              ...element.binding,
                              requiredMessage: e.target.value
                            })}
                            placeholder="Error message when field is missing"
                            className="ui-input"
                          />
                        </label>
                      )}
                    </div>
                  </div>
                )}

                {/* Expression Properties Section */}
                <div className="ui-properties-section">
                  <h3 className="ui-properties-section-title">Expressions & Conditions</h3>
                  <div className="ui-properties-stack-sm">
                    <label>
                      Visible When:
                      <input
                        type="text"
                        value={element.expression?.visibleWhen || ''}
                        onChange={(e) => updateElementExpression(selectedId, {
                          ...element.expression,
                          visibleWhen: e.target.value
                        })}
                        placeholder="e.g., status === 'active'"
                        className="ui-input"
                      />
                      <small className="ui-properties-hint">
                        Expression that determines if element is visible (leave empty for always visible)
                      </small>
                    </label>

                    <label>
                      Value Expression:
                      <input
                        type="text"
                        value={element.expression?.valueExpression || ''}
                        onChange={(e) => updateElementExpression(selectedId, {
                          ...element.expression,
                          valueExpression: e.target.value
                        })}
                        placeholder="e.g., formatCurrency(amount)"
                        className="ui-input"
                      />
                      <small className="ui-properties-hint">
                        Expression to compute the element's value
                      </small>
                    </label>

                    <label>
                      Style Expression:
                      <textarea
                        value={JSON.stringify(element.expression?.styleExpression || {}, null, 2)}
                        onChange={(e) => {
                          try {
                            const styleObj = JSON.parse(e.target.value);
                            updateElementExpression(selectedId, {
                              ...element.expression,
                              styleExpression: styleObj
                            });
                          } catch (err) {
                            // Invalid JSON, don't update
                          }
                        }}
                        placeholder='{"color": "status === \"error\" ? \"red\" : \"black\""}'
                        className="ui-textarea"
                        rows={3}
                      />
                      <small className="ui-properties-hint">
                        JSON object with conditional style expressions
                      </small>
                    </label>

                    <label className="ui-checkbox-label">
                      <input
                        type="checkbox"
                        checked={element.expression?.safeExpressionMode !== false}
                        onChange={(e) => updateElementExpression(selectedId, {
                          ...element.expression,
                          safeExpressionMode: e.target.checked
                        })}
                      />
                      Safe expression mode (recommended)
                    </label>
                  </div>
                </div>

                {/* Repeat Properties Section */}
                {(element.type === 'Table' || element.type === 'List' || element.type === 'Grid' || element.type === 'Column') && (
                  <div className="ui-properties-section">
                    <h3 className="ui-properties-section-title">Repeat & Collections</h3>
                    <div className="ui-properties-stack-sm">
                      <label>
                        Repeat Source:
                        <input
                          type="text"
                          value={element.repeat?.repeatSource || ''}
                          onChange={(e) => updateElementRepeat(selectedId, {
                            ...element.repeat,
                            repeatSource: e.target.value
                          })}
                          placeholder="e.g., order.items"
                          className="ui-input"
                        />
                        <small className="ui-properties-hint">
                          Array path to repeat this element for each item
                        </small>
                      </label>

                      <div className="ui-properties-grid-2">
                        <label>
                          Item Alias:
                          <input
                            type="text"
                            value={element.repeat?.itemAlias || 'item'}
                            onChange={(e) => updateElementRepeat(selectedId, {
                              ...element.repeat,
                              itemAlias: e.target.value
                            })}
                            placeholder="item"
                            className="ui-input"
                          />
                        </label>

                        <label>
                          Index Alias:
                          <input
                            type="text"
                            value={element.repeat?.indexAlias || 'index'}
                            onChange={(e) => updateElementRepeat(selectedId, {
                              ...element.repeat,
                              indexAlias: e.target.value
                            })}
                            placeholder="index"
                            className="ui-input"
                          />
                        </label>
                      </div>

                      <label>
                        Empty Behavior:
                        <select
                          value={element.repeat?.emptyBehavior || 'hide'}
                          onChange={(e) => updateElementRepeat(selectedId, {
                            ...element.repeat,
                            emptyBehavior: e.target.value as any
                          })}
                        >
                          <option value="hide">Hide element</option>
                          <option value="show-placeholder">Show placeholder</option>
                          <option value="keep-template">Keep template</option>
                        </select>
                      </label>

                      <div className="ui-properties-grid-2">
                        <label>
                          Max Items:
                          <input
                            type="number"
                            min="1"
                            value={element.repeat?.maxItems || ''}
                            onChange={(e) => updateElementRepeat(selectedId, {
                              ...element.repeat,
                              maxItems: e.target.value ? parseInt(e.target.value) : undefined
                            })}
                            className="ui-input"
                          />
                        </label>

                        <label>
                          Page Break Between:
                          <select
                            value={element.repeat?.pageBreakBetweenItems ? 'yes' : 'no'}
                            onChange={(e) => updateElementRepeat(selectedId, {
                              ...element.repeat,
                              pageBreakBetweenItems: e.target.value === 'yes'
                            })}
                          >
                            <option value="no">No</option>
                            <option value="yes">Yes</option>
                          </select>
                        </label>
                      </div>
                    </div>
                  </div>
                )}

                {/* Element-Specific Properties */}
                {element.type === 'Text' && (
                   <>
                     <label>
                       Text:
                       <input
                         name="text"
                         value={element.props.text}
                         onChange={handleChange}
                         type="text"
                       />
                     </label>
                    <Input
                      label="Font Size"
                      name="fontSize"
                      value={element.props.fontSize}
                      onValidatedChange={(value, isValid) => {
                        if (isValid) {
                          updateElementProps(selectedId, { fontSize: parseInt(value, 10) });
                        }
                      }}
                      type="number"
                      min={8}
                      max={72}
                      validate={(value) => {
                        const num = parseInt(value, 10);
                        if (isNaN(num)) return "Font size must be a number";
                        if (num < 8) return "Font size must be at least 8px";
                        if (num > 72) return "Font size cannot exceed 72px";
                        return null;
                      }}
                    />
                    <label>
                      Font Family:
                      <select
                        name="fontFamily"
                        value={element.props.fontFamily || 'Arial'}
                        onChange={handleChange}
                      >
                        <option value="Arial">Arial</option>
                        <option value="Helvetica">Helvetica</option>
                        <option value="Times New Roman">Times New Roman</option>
                        <option value="Georgia">Georgia</option>
                        <option value="Verdana">Verdana</option>
                        <option value="Courier New">Courier New</option>
                        <option value="Impact">Impact</option>
                        <option value="Comic Sans MS">Comic Sans MS</option>
                      </select>
                    </label>
                    <label>
                      Font Color:
                      <input
                        name="color"
                        value={element.props.color || '#000000'}
                        onChange={handleChange}
                        type="color"
                      />
                    </label>
                    <label>
                      Font Weight:
                      <select
                        name="fontWeight"
                        value={element.props.fontWeight || 'normal'}
                        onChange={handleChange}
                      >
                        <option value="normal">Normal</option>
                        <option value="bold">Bold</option>
                        <option value="lighter">Lighter</option>
                        <option value="bolder">Bolder</option>
                      </select>
                    </label>
                    <label>
                      Font Style:
                      <select
                        name="fontStyle"
                        value={element.props.fontStyle || 'normal'}
                        onChange={handleChange}
                      >
                        <option value="normal">Normal</option>
                        <option value="italic">Italic</option>
                        <option value="oblique">Oblique</option>
                      </select>
                    </label>
                    <label>
                      Text Align:
                      <select
                        name="textAlign"
                        value={element.props.textAlign || 'left'}
                        onChange={handleChange}
                      >
                        <option value="left">Left</option>
                        <option value="center">Center</option>
                        <option value="right">Right</option>
                        <option value="justify">Justify</option>
                      </select>
                    </label>
                    <label>
                      Background Color:
                      <input
                        name="backgroundColor"
                        value={element.props.backgroundColor || 'transparent'}
                        onChange={handleChange}
                        type="color"
                      />
                    </label>
                    <label>
                      Opacity:
                      <input
                        name="opacity"
                        value={element.props.opacity || 1}
                        onChange={(e) => updateElementProps(selectedId, { opacity: parseFloat(e.target.value) })}
                        type="range"
                        min="0"
                        max="1"
                        step="0.1"
                      />
                      <span className="ui-properties-percentage">{Math.round((element.props.opacity || 1) * 100)}%</span>
                    </label>
                    <label>
                      Shadow:
                      <select
                        name="boxShadow"
                        value={element.props.boxShadow || 'none'}
                        onChange={handleChange}
                      >
                        <option value="none">None</option>
                        <option value="0 2px 4px rgba(0,0,0,0.1)">Small</option>
                        <option value="0 4px 8px rgba(0,0,0,0.15)">Medium</option>
                        <option value="0 8px 16px rgba(0,0,0,0.2)">Large</option>
                        <option value="0 0 20px rgba(0,0,0,0.3)">Glow</option>
                      </select>
                    </label>
                    <label>
                      Border:
                      <div className="ui-properties-inline-tight">
                        <input
                          name="borderWidth"
                          value={element.props.borderWidth || 0}
                          onChange={(e) => updateElementProps(selectedId, { borderWidth: parseInt(e.target.value) || 0 })}
                          type="number"
                          min="0"
                          max="20"
                          className="ui-properties-width-50"
                        />
                        <select
                          name="borderStyle"
                          value={element.props.borderStyle || 'solid'}
                          onChange={handleChange}
                          className="ui-properties-width-70"
                        >
                          <option value="solid">Solid</option>
                          <option value="dashed">Dashed</option>
                          <option value="dotted">Dotted</option>
                          <option value="double">Double</option>
                        </select>
                        <input
                          name="borderColor"
                          value={element.props.borderColor || '#000000'}
                          onChange={handleChange}
                          type="color"
                          className="ui-properties-color-compact"
                        />
                      </div>
                    </label>
                    <label>
                      Border Radius:
                      <input
                        name="borderRadius"
                        value={element.props.borderRadius || 0}
                        onChange={(e) => updateElementProps(selectedId, { borderRadius: parseInt(e.target.value) || 0 })}
                        type="number"
                        min="0"
                        max="50"
                      />
                    </label>
                    <label>
                      Width:
                      <input
                        name="width"
                        value={element.props.width || 'auto'}
                        onChange={(e) => updateElementProps(selectedId, { width: e.target.value === 'auto' ? 'auto' : parseInt(e.target.value) || 'auto' })}
                        type="text"
                        placeholder="auto or number"
                      />
                    </label>
                    <label>
                      Height:
                      <input
                        name="height"
                        value={element.props.height || 'auto'}
                        onChange={(e) => updateElementProps(selectedId, { height: e.target.value === 'auto' ? 'auto' : parseInt(e.target.value) || 'auto' })}
                        type="text"
                        placeholder="auto or number"
                      />
                    </label>
                    <Input
                      label="Position X"
                      name="x"
                      value={element.x || 0}
                      onValidatedChange={(value, isValid) => {
                        if (isValid) {
                          updateElementProps(selectedId, { x: parseInt(value, 10) });
                        }
                      }}
                      type="number"
                      min={-1000}
                      max={2000}
                      validate={(value) => {
                        const num = parseInt(value, 10);
                        if (isNaN(num)) return "Position must be a number";
                        if (num < -1000) return "Position cannot be less than -1000";
                        if (num > 2000) return "Position cannot exceed 2000";
                        return null;
                      }}
                    />
                    <Input
                      label="Position Y"
                      name="y"
                      value={element.y || 0}
                      onValidatedChange={(value, isValid) => {
                        if (isValid) {
                          updateElementProps(selectedId, { y: parseInt(value, 10) });
                        }
                      }}
                      type="number"
                      min={-1000}
                      max={2000}
                      validate={(value) => {
                        const num = parseInt(value, 10);
                        if (isNaN(num)) return "Position must be a number";
                        if (num < -1000) return "Position cannot be less than -1000";
                        if (num > 2000) return "Position cannot exceed 2000";
                        return null;
                      }}
                    />
                    <div className="ui-card-muted ui-properties-top-gap-lg">
                      <div className="ui-note-title">
                        Element ID
                      </div>
                      <div className="ui-note-text ui-properties-note-mono">
                        {selectedId}
                      </div>
                    </div>
                  </>
                )}
                {element.type === 'QRCode' && (
                  <>
                    <label>
                      Value:
                      <input
                        name="value"
                        value={element.props.value || 'https://example.com'}
                        onChange={handleChange}
                        type="text"
                        placeholder="Enter URL or text for QR code"
                      />
                    </label>
                    <Input
                      label="Size"
                      name="size"
                      value={element.props.size || 100}
                      onValidatedChange={(value, isValid) => {
                        if (isValid) {
                          updateElementProps(selectedId, { size: parseInt(value, 10) });
                        }
                      }}
                      type="number"
                      min={50}
                      max={500}
                      validate={(value) => {
                        const num = parseInt(value, 10);
                        if (isNaN(num)) return "Size must be a number";
                        if (num < 50) return "Size must be at least 50px";
                        if (num > 500) return "Size cannot exceed 500px";
                        return null;
                      }}
                    />
                    <label>
                      ECC Level:
                      <select
                        name="eccLevel"
                        value={element.props.eccLevel || 'M'}
                        onChange={handleChange}
                      >
                        <option value="L">Low (7%)</option>
                        <option value="M">Medium (15%)</option>
                        <option value="Q">Quartile (25%)</option>
                        <option value="H">High (30%)</option>
                      </select>
                    </label>
                    <Input
                      label="Quiet Zone"
                      name="quietZone"
                      value={element.props.quietZone || 4}
                      onValidatedChange={(value, isValid) => {
                        if (isValid) {
                          updateElementProps(selectedId, { quietZone: parseInt(value, 10) });
                        }
                      }}
                      type="number"
                      min={0}
                      max={10}
                    />
                  </>
                )}
                {element.type === 'Barcode' && (
                  <>
                    <label>
                      Value:
                      <input
                        name="value"
                        value={element.props.value || '123456789'}
                        onChange={handleChange}
                        type="text"
                        placeholder="Enter barcode value"
                      />
                    </label>
                    <label>
                      Symbology:
                      <select
                        name="symbology"
                        value={element.props.symbology || 'CODE128'}
                        onChange={handleChange}
                      >
                        <option value="CODE128">Code 128</option>
                        <option value="CODE39">Code 39</option>
                        <option value="EAN13">EAN-13</option>
                        <option value="UPCA">UPC-A</option>
                        <option value="QRCODE">QR Code</option>
                      </select>
                    </label>
                    <Input
                      label="Width"
                      name="width"
                      value={element.props.width || 200}
                      onValidatedChange={(value, isValid) => {
                        if (isValid) {
                          updateElementProps(selectedId, { width: parseInt(value, 10) });
                        }
                      }}
                      type="number"
                      min={100}
                      max={500}
                    />
                    <Input
                      label="Height"
                      name="height"
                      value={element.props.height || 60}
                      onValidatedChange={(value, isValid) => {
                        if (isValid) {
                          updateElementProps(selectedId, { height: parseInt(value, 10) });
                        }
                      }}
                      type="number"
                      min={30}
                      max={200}
                    />
                    <label>
                      Include Checksum:
                      <input
                        name="checksum"
                        checked={element.props.checksum || false}
                        onChange={(e) => updateElementProps(selectedId, { checksum: e.target.checked })}
                        type="checkbox"
                      />
                    </label>
                  </>
                )}
                {element.type === 'Signature' && (
                  <>
                    <label>
                      Label:
                      <input
                        name="label"
                        value={element.props.label || 'Signature'}
                        onChange={handleChange}
                        type="text"
                        placeholder="Signature label"
                      />
                    </label>
                    <label>
                      Signer Name Path:
                      <input
                        name="signerNamePath"
                        value={element.props.signerNamePath || ''}
                        onChange={handleChange}
                        type="text"
                        placeholder="e.g., signer.name"
                      />
                    </label>
                    <label>
                      Date Path:
                      <input
                        name="datePath"
                        value={element.props.datePath || ''}
                        onChange={handleChange}
                        type="text"
                        placeholder="e.g., signedAt"
                      />
                    </label>
                    <label>
                      Image Path:
                      <input
                        name="imagePath"
                        value={element.props.imagePath || ''}
                        onChange={handleChange}
                        type="text"
                        placeholder="e.g., signatureImage"
                      />
                    </label>
                  </>
                )}
                {element.type === 'RichText' && (
                  <>
                    <label>
                      HTML Content:
                      <textarea
                        name="html"
                        value={element.props.html || '<p>Rich text content</p>'}
                        onChange={handleChange}
                        rows={6}
                        placeholder="Enter HTML content"
                      />
                    </label>
                    <label>
                      Style Profile:
                      <select
                        name="styleProfile"
                        value={element.props.styleProfile || 'default'}
                        onChange={handleChange}
                      >
                        <option value="default">Default</option>
                        <option value="compact">Compact</option>
                        <option value="spacious">Spacious</option>
                        <option value="formal">Formal</option>
                      </select>
                    </label>
                  </>
                )}
              </>
            );
          })()}
        </>
      )}
    </aside>
  );
};

export default PropertiesPanel;