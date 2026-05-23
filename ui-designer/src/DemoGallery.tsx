import React, { useState } from 'react';
import { getDemoTemplates, getDemoCategories, DemoTemplate } from './demo/DemoTemplates';

interface DemoGalleryProps {
  onLoadTemplate: (template: any, sampleData: any) => void;
}

export const DemoGallery: React.FC<DemoGalleryProps> = ({ onLoadTemplate }) => {
  const [selectedCategory, setSelectedCategory] = useState<string>('All');
  const [selectedTemplate, setSelectedTemplate] = useState<DemoTemplate | null>(null);

  const categories = ['All', ...getDemoCategories()];
  const templates = selectedCategory === 'All'
    ? getDemoTemplates()
    : getDemoTemplates().filter(t => t.category === selectedCategory);

  const handleLoadTemplate = (template: DemoTemplate) => {
    onLoadTemplate(template.template, template.sampleData);
  };

  const handlePreviewTemplate = (template: DemoTemplate) => {
    setSelectedTemplate(template);
  };

  return (
    <div className="demo-gallery">
      <div className="demo-header">
        <h2 className="text-2xl font-bold text-gray-900 mb-4">Template Gallery</h2>
        <p className="text-gray-600 mb-6">
          Explore pre-built templates showcasing the full capabilities of the UI Designer.
          Each template demonstrates different features like data binding, expressions, conditionals, and loops.
        </p>

        {/* Category Filter */}
        <div className="category-filter mb-6">
          <div className="flex flex-wrap gap-2">
            {categories.map(category => (
              <button
                key={category}
                onClick={() => setSelectedCategory(category)}
                className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                  selectedCategory === category
                    ? 'bg-blue-600 text-white'
                    : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                }`}
              >
                {category}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Template Grid */}
      <div className="template-grid grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {templates.map(template => (
          <div key={template.id} className="template-card bg-white border border-gray-200 rounded-lg shadow-sm p-4">
            <div className="template-preview bg-gray-50 h-48 rounded-lg mb-4 flex items-center justify-center">
              <div className="text-center text-gray-500">
                <div className="text-4xl mb-2">
                  {template.category === 'Business' && '📄'}
                  {template.category === 'Education' && '🎓'}
                  {template.category === 'Marketing' && '📢'}
                </div>
                <div className="text-sm font-medium">{template.category}</div>
              </div>
            </div>

            <div className="template-info">
              <h3 className="text-lg font-semibold text-gray-900 mb-2">{template.name}</h3>
              <p className="text-gray-600 text-sm mb-4">{template.description}</p>

              <div className="template-features mb-4">
                <div className="flex flex-wrap gap-1">
                  {template.id.includes('invoice') && (
                    <>
                      <span className="px-2 py-1 bg-blue-100 text-blue-800 text-xs rounded">Data Binding</span>
                      <span className="px-2 py-1 bg-green-100 text-green-800 text-xs rounded">Repeats</span>
                      <span className="px-2 py-1 bg-purple-100 text-purple-800 text-xs rounded">Formatters</span>
                    </>
                  )}
                  {template.id.includes('certificate') && (
                    <>
                      <span className="px-2 py-1 bg-yellow-100 text-yellow-800 text-xs rounded">Conditionals</span>
                      <span className="px-2 py-1 bg-green-100 text-green-800 text-xs rounded">Expressions</span>
                      <span className="px-2 py-1 bg-indigo-100 text-indigo-800 text-xs rounded">Templates</span>
                    </>
                  )}
                  {template.id.includes('report') && (
                    <>
                      <span className="px-2 py-1 bg-blue-100 text-blue-800 text-xs rounded">Complex Data</span>
                      <span className="px-2 py-1 bg-red-100 text-red-800 text-xs rounded">Calculations</span>
                      <span className="px-2 py-1 bg-purple-100 text-purple-800 text-xs rounded">Multiple Tables</span>
                    </>
                  )}
                </div>
              </div>

              <div className="template-actions flex gap-2">
                <button
                  onClick={() => handlePreviewTemplate(template)}
                  className="flex-1 px-3 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
                >
                  Preview
                </button>
                <button
                  onClick={() => handleLoadTemplate(template)}
                  className="flex-1 px-3 py-2 bg-blue-600 border border-transparent rounded-md text-sm font-medium text-white hover:bg-blue-700"
                >
                  Load Template
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Template Preview Modal */}
      {selectedTemplate && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg max-w-4xl w-full mx-4 max-h-[90vh] overflow-y-auto">
            <div className="p-6">
              <div className="flex justify-between items-center mb-4">
                <h3 className="text-xl font-bold text-gray-900">{selectedTemplate.name}</h3>
                <button
                  onClick={() => setSelectedTemplate(null)}
                  className="text-gray-400 hover:text-gray-600"
                >
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>

              <div className="mb-6">
                <h4 className="text-lg font-semibold mb-2">Description</h4>
                <p className="text-gray-600">{selectedTemplate.description}</p>
              </div>

              <div className="mb-6">
                <h4 className="text-lg font-semibold mb-2">Sample Data Structure</h4>
                <pre className="bg-gray-100 p-4 rounded-lg text-sm overflow-x-auto">
                  {JSON.stringify(selectedTemplate.sampleData, null, 2)}
                </pre>
              </div>

              <div className="mb-6">
                <h4 className="text-lg font-semibold mb-2">Template Features</h4>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                  {selectedTemplate.id.includes('invoice') && (
                    <>
                      <div className="bg-blue-50 p-3 rounded-lg text-center">
                        <div className="text-blue-600 font-semibold">Data Binding</div>
                        <div className="text-sm text-blue-800">Dynamic content from JSON</div>
                      </div>
                      <div className="bg-green-50 p-3 rounded-lg text-center">
                        <div className="text-green-600 font-semibold">Repeating Sections</div>
                        <div className="text-sm text-green-800">Loop through arrays</div>
                      </div>
                      <div className="bg-purple-50 p-3 rounded-lg text-center">
                        <div className="text-purple-600 font-semibold">Formatters</div>
                        <div className="text-sm text-purple-800">Currency, dates, numbers</div>
                      </div>
                      <div className="bg-orange-50 p-3 rounded-lg text-center">
                        <div className="text-orange-600 font-semibold">Calculations</div>
                        <div className="text-sm text-orange-800">Automatic totals</div>
                      </div>
                    </>
                  )}
                  {selectedTemplate.id.includes('certificate') && (
                    <>
                      <div className="bg-yellow-50 p-3 rounded-lg text-center">
                        <div className="text-yellow-600 font-semibold">Conditionals</div>
                        <div className="text-sm text-yellow-800">Show/hide based on data</div>
                      </div>
                      <div className="bg-green-50 p-3 rounded-lg text-center">
                        <div className="text-green-600 font-semibold">Expressions</div>
                        <div className="text-sm text-green-800">JavaScript evaluation</div>
                      </div>
                      <div className="bg-indigo-50 p-3 rounded-lg text-center">
                        <div className="text-indigo-600 font-semibold">Template Literals</div>
                        <div className="text-sm text-indigo-800">Dynamic string interpolation</div>
                      </div>
                      <div className="bg-pink-50 p-3 rounded-lg text-center">
                        <div className="text-pink-600 font-semibold">Professional Design</div>
                        <div className="text-sm text-pink-800">Certificate styling</div>
                      </div>
                    </>
                  )}
                  {selectedTemplate.id.includes('report') && (
                    <>
                      <div className="bg-blue-50 p-3 rounded-lg text-center">
                        <div className="text-blue-600 font-semibold">Complex Data</div>
                        <div className="text-sm text-blue-800">Nested objects & arrays</div>
                      </div>
                      <div className="bg-red-50 p-3 rounded-lg text-center">
                        <div className="text-red-600 font-semibold">Multiple Tables</div>
                        <div className="text-sm text-red-800">Different data sources</div>
                      </div>
                      <div className="bg-purple-50 p-3 rounded-lg text-center">
                        <div className="text-purple-600 font-semibold">Business Logic</div>
                        <div className="text-sm text-purple-800">Complex expressions</div>
                      </div>
                      <div className="bg-teal-50 p-3 rounded-lg text-center">
                        <div className="text-teal-600 font-semibold">Metrics Display</div>
                        <div className="text-sm text-teal-800">KPI visualization</div>
                      </div>
                    </>
                  )}
                </div>
              </div>

              <div className="flex gap-3">
                <button
                  onClick={() => handleLoadTemplate(selectedTemplate)}
                  className="flex-1 px-4 py-2 bg-blue-600 border border-transparent rounded-md text-sm font-medium text-white hover:bg-blue-700"
                >
                  Load This Template
                </button>
                <button
                  onClick={() => setSelectedTemplate(null)}
                  className="px-4 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};