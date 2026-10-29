import definition from '../../../product-metadata/brand.json';

export const pvadBrand = Object.freeze({
  ...definition,
  products: Object.freeze({ ...definition.products }),
});
