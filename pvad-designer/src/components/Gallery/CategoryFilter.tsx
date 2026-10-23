import React from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'framer-motion';

interface Category {
  id: string;
  name: string;
  count: number;
}

interface CategoryFilterProps {
  categories: Category[];
  selectedCategory: string;
  onCategoryChange: (categoryId: string) => void;
}

const CategoryFilter: React.FC<CategoryFilterProps> = ({
  categories,
  selectedCategory,
  onCategoryChange
}) => {
  const { t } = useTranslation('gallery');
  return (
    <div className="pdf-category-filter" role="tablist" aria-label={t('categoryFilter.ariaLabel')}>
      {categories.map(category => {
        const isActive = selectedCategory === category.id;

        return (
          <motion.button
            key={category.id}
            className={`pdf-category-button ${isActive ? 'is-active' : ''}`}
            onClick={() => onCategoryChange(category.id)}
            role="tab"
            aria-selected={isActive}
            whileHover={{ y: -1 }}
            whileTap={{ scale: 0.98 }}
          >
            <span>{category.name}</span>
            <strong>{category.count}</strong>
          </motion.button>
        );
      })}
    </div>
  );
};

export default CategoryFilter;
