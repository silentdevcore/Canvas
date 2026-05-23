// Domain layer exports

// Entities
export { DesignerElement, DesignerElementFactory } from './entities/DesignerElement';
export type { ElementId, ElementProperties } from './entities/DesignerElement';

// Value Objects
export { PageSettings, PageMargins } from './value-objects/PageSettings';
export type { PageSettingsUpdate } from './value-objects/PageSettings';
export { PageDimensions } from './value-objects/PageSize';
export type { PageSize, PageOrientation } from './value-objects/PageSize';
export { getPageSizeDisplayName, getOrientationDisplayName, isValidPageSize, isValidPageOrientation } from './value-objects/PageSize';
export type { ElementType } from './value-objects/ElementType';
export { ElementTypeValues, isValidElementType, getElementTypeDisplayName } from './value-objects/ElementType';

// Services
export { ElementValidationService } from './services/ElementValidationService';
export type { ValidationResult } from './services/ElementValidationService';

// Repositories
export type { IElementRepository } from './repositories/IElementRepository';
export type { IPageRepository } from './repositories/IPageRepository';
export type { ITemplateRepository, DesignTemplate } from './repositories/ITemplateRepository';