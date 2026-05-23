/**
 * Domain value object representing the type of UI elements that can be created in the designer.
 * This is a closed set of valid element types with business rules.
 */
export type ElementType =
  | 'Text'
  | 'Column'
  | 'Table'
  | 'Image'
  | 'Rectangle'
  | 'Circle'
  | 'Line'
  | 'Link'
  | 'List'
  | 'PageBreak'
  | 'Grid'
  | 'Spacer'
  | 'Button'
  | 'Checkbox'
  | 'Radio'
  | 'QRCode'
  | 'Barcode'
  | 'Signature'
  | 'RichText';

/**
 * Enum-like object for ElementType values (for use in code)
 */
export const ElementTypeValues = {
  Text: 'Text' as ElementType,
  Column: 'Column' as ElementType,
  Table: 'Table' as ElementType,
  Image: 'Image' as ElementType,
  Rectangle: 'Rectangle' as ElementType,
  Circle: 'Circle' as ElementType,
  Line: 'Line' as ElementType,
  Link: 'Link' as ElementType,
  List: 'List' as ElementType,
  PageBreak: 'PageBreak' as ElementType,
  Grid: 'Grid' as ElementType,
  Spacer: 'Spacer' as ElementType,
  Button: 'Button' as ElementType,
  Checkbox: 'Checkbox' as ElementType,
  Radio: 'Radio' as ElementType,
  QRCode: 'QRCode' as ElementType,
  Barcode: 'Barcode' as ElementType,
  Signature: 'Signature' as ElementType,
  RichText: 'RichText' as ElementType
} as const;

/**
 * Validates if a string is a valid ElementType
 */
export function isValidElementType(type: string): type is ElementType {
  const validTypes: ElementType[] = [
    'Text', 'Column', 'Table', 'Image', 'Rectangle', 'Circle', 'Line',
    'Link', 'List', 'PageBreak', 'Grid', 'Spacer', 'Button', 'Checkbox', 'Radio',
    'QRCode', 'Barcode', 'Signature', 'RichText'
  ];
  return validTypes.includes(type as ElementType);
}

/**
 * Gets the display name for an element type
 */
export function getElementTypeDisplayName(type: ElementType): string {
  const displayNames: Record<ElementType, string> = {
    Text: 'Text',
    Column: 'Column',
    Table: 'Table',
    Image: 'Image',
    Rectangle: 'Rectangle',
    Circle: 'Circle',
    Line: 'Line',
    Link: 'Link',
    List: 'List',
    PageBreak: 'Page Break',
    Grid: 'Grid',
    Spacer: 'Spacer',
    Button: 'Button',
    Checkbox: 'Checkbox',
    Radio: 'Radio Button',
    QRCode: 'QR Code',
    Barcode: 'Barcode',
    Signature: 'Signature',
    RichText: 'Rich Text'
  };
  return displayNames[type];
}

/**
 * Determines if an element type supports data binding
 */
export function isElementBindable(type: ElementType): boolean {
  const bindableTypes: ElementType[] = [
    'Text', 'Image', 'QRCode', 'Barcode', 'Signature', 'RichText',
    'Button', 'Checkbox', 'Radio', 'Link', 'List'
  ];
  return bindableTypes.includes(type);
}
