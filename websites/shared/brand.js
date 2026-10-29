import definition from '../../product-metadata/brand.json' with { type: 'json' };

export const pvadBrand = Object.freeze({
  ...definition,
  products: Object.freeze({ ...definition.products }),
});
