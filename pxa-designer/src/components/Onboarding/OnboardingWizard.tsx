import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { FiChevronLeft, FiChevronRight, FiPlay, FiEdit, FiEye, FiDownload, FiCheck } from 'react-icons/fi';

interface Template {
  id: string;
  name: string;
  category: string;
  thumbnail: string;
  description: string;
}

interface OnboardingWizardProps {
  template: Template;
  onComplete: () => void;
  onBack: () => void;
}

type Step = 'welcome' | 'add-elements' | 'customize' | 'preview' | 'export';

const OnboardingWizard: React.FC<OnboardingWizardProps> = ({ template, onComplete, onBack }) => {
  const [currentStep, setCurrentStep] = useState<Step>('welcome');
  const [completedSteps, setCompletedSteps] = useState<Set<Step>>(new Set());

  const steps: { id: Step; title: string; description: string; icon: React.ReactNode }[] = [
    {
      id: 'welcome',
      title: 'Welcome to Your Template',
      description: 'You\'ve selected a professional template. Let\'s get you started!',
      icon: <FiPlay className="w-6 h-6" />
    },
    {
      id: 'add-elements',
      title: 'Add Your Content',
      description: 'Use the toolbar to add text, QR codes, barcodes, signatures, and rich text elements.',
      icon: <FiEdit className="w-6 h-6" />
    },
    {
      id: 'customize',
      title: 'Customize Everything',
      description: 'Click on any element to edit its properties, position, and styling.',
      icon: <FiEdit className="w-6 h-6" />
    },
    {
      id: 'preview',
      title: 'Preview Your Design',
      description: 'See exactly how your document will look before exporting.',
      icon: <FiEye className="w-6 h-6" />
    },
    {
      id: 'export',
      title: 'Export Your PDF',
      description: 'Generate a professional PDF ready for printing or sharing.',
      icon: <FiDownload className="w-6 h-6" />
    }
  ];

  const handleNext = () => {
    const currentIndex = steps.findIndex(step => step.id === currentStep);
    if (currentIndex < steps.length - 1) {
      setCompletedSteps(prev => new Set([...prev, currentStep]));
      setCurrentStep(steps[currentIndex + 1].id);
    } else {
      onComplete();
    }
  };

  const handlePrevious = () => {
    const currentIndex = steps.findIndex(step => step.id === currentStep);
    if (currentIndex > 0) {
      setCurrentStep(steps[currentIndex - 1].id);
    }
  };

  const handleStepClick = (stepId: Step) => {
    // Allow jumping to completed steps or adjacent steps
    const currentIndex = steps.findIndex(step => step.id === currentStep);
    const targetIndex = steps.findIndex(step => step.id === stepId);

    if (completedSteps.has(stepId) || Math.abs(targetIndex - currentIndex) <= 1) {
      setCurrentStep(stepId);
    }
  };

  const currentStepData = steps.find(step => step.id === currentStep);
  const currentIndex = steps.findIndex(step => step.id === currentStep);
  const isLastStep = currentIndex === steps.length - 1;

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 flex items-center justify-center p-4">
      <div className="max-w-4xl w-full">
        {/* Progress Indicator */}
        <div className="mb-8">
          <div className="flex items-center justify-center space-x-4 mb-6">
            {steps.map((step, index) => {
              const isCompleted = completedSteps.has(step.id);
              const isCurrent = step.id === currentStep;
              const isClickable = isCompleted || Math.abs(index - currentIndex) <= 1;

              return (
                <React.Fragment key={step.id}>
                  <motion.button
                    onClick={() => isClickable && handleStepClick(step.id)}
                    className={`flex items-center justify-center w-12 h-12 rounded-full border-2 transition-all duration-200 ${
                      isCompleted
                        ? 'bg-green-500 border-green-500 text-white'
                        : isCurrent
                        ? 'bg-blue-500 border-blue-500 text-white'
                        : isClickable
                        ? 'border-gray-300 text-gray-400 hover:border-gray-400'
                        : 'border-gray-200 text-gray-300 cursor-not-allowed'
                    }`}
                    whileHover={isClickable ? { scale: 1.05 } : {}}
                    whileTap={isClickable ? { scale: 0.95 } : {}}
                  >
                    {isCompleted ? (
                      <FiCheck className="w-5 h-5" />
                    ) : (
                      <span className="text-sm font-medium">{index + 1}</span>
                    )}
                  </motion.button>
                  {index < steps.length - 1 && (
                    <div
                      className={`flex-1 h-0.5 transition-colors duration-200 ${
                        isCompleted ? 'bg-green-500' : 'bg-gray-200'
                      }`}
                    />
                  )}
                </React.Fragment>
              );
            })}
          </div>
          <div className="text-center">
            <h2 className="text-2xl font-bold text-gray-900 mb-2">
              {currentStepData?.title}
            </h2>
            <p className="text-gray-600 max-w-md mx-auto">
              {currentStepData?.description}
            </p>
          </div>
        </div>

        {/* Step Content */}
        <AnimatePresence mode="wait">
          <motion.div
            key={currentStep}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -20 }}
            transition={{ duration: 0.3 }}
            className="bg-white rounded-2xl shadow-xl p-8 text-center"
          >
            {/* Template Preview */}
            <div className="mb-8">
              <div className="inline-block p-4 bg-gray-50 rounded-xl">
                <div className="w-32 h-24 bg-gradient-to-br from-blue-100 to-purple-100 rounded-lg flex items-center justify-center mb-3">
                  <span className="text-2xl font-bold text-gray-600">
                    {template.name.charAt(0).toUpperCase()}
                  </span>
                </div>
                <h3 className="font-semibold text-gray-900 mb-1">{template.name}</h3>
                <p className="text-sm text-gray-600">{template.category}</p>
              </div>
            </div>

            {/* Step-specific content */}
            {currentStep === 'welcome' && (
              <div className="space-y-4">
                <div className="text-4xl mb-4">🎉</div>
                <p className="text-gray-700">
                  Great choice! The <strong>{template.name}</strong> template is perfect for creating professional documents.
                </p>
              </div>
            )}

            {currentStep === 'add-elements' && (
              <div className="space-y-4">
                <div className="text-4xl mb-4">🛠️</div>
                <p className="text-gray-700 mb-4">
                  Use the colorful buttons in the toolbar to add different types of content:
                </p>
                <div className="grid grid-cols-2 md:grid-cols-3 gap-3 text-sm">
                  <div className="flex items-center space-x-2 p-3 bg-blue-50 rounded-lg">
                    <span className="w-3 h-3 bg-blue-500 rounded-full"></span>
                    <span>Text elements</span>
                  </div>
                  <div className="flex items-center space-x-2 p-3 bg-purple-50 rounded-lg">
                    <span className="w-3 h-3 bg-purple-500 rounded-full"></span>
                    <span>QR codes</span>
                  </div>
                  <div className="flex items-center space-x-2 p-3 bg-indigo-50 rounded-lg">
                    <span className="w-3 h-3 bg-indigo-500 rounded-full"></span>
                    <span>Barcodes</span>
                  </div>
                  <div className="flex items-center space-x-2 p-3 bg-pink-50 rounded-lg">
                    <span className="w-3 h-3 bg-pink-500 rounded-full"></span>
                    <span>Signatures</span>
                  </div>
                  <div className="flex items-center space-x-2 p-3 bg-orange-50 rounded-lg">
                    <span className="w-3 h-3 bg-orange-500 rounded-full"></span>
                    <span>Rich text</span>
                  </div>
                </div>
              </div>
            )}

            {currentStep === 'customize' && (
              <div className="space-y-4">
                <div className="text-4xl mb-4">✨</div>
                <p className="text-gray-700">
                  Click on any element you've added to customize its content, position, and appearance.
                  The properties panel will show options specific to each element type.
                </p>
              </div>
            )}

            {currentStep === 'preview' && (
              <div className="space-y-4">
                <div className="text-4xl mb-4">👁️</div>
                <p className="text-gray-700">
                  Use the "Preview" button to see exactly how your final document will look.
                  Make sure everything is positioned correctly before exporting.
                </p>
              </div>
            )}

            {currentStep === 'export' && (
              <div className="space-y-4">
                <div className="text-4xl mb-4">📄</div>
                <p className="text-gray-700">
                  When you're happy with your design, click "Export PDF" to generate a professional document.
                  Your PDF will be ready for printing, sharing, or any other use.
                </p>
              </div>
            )}

            {/* Navigation Buttons */}
            <div className="flex items-center justify-between mt-8 pt-6 border-t border-gray-200">
              <button
                onClick={currentIndex === 0 ? onBack : handlePrevious}
                className="flex items-center space-x-2 px-6 py-3 text-gray-600 hover:text-gray-900 font-medium transition-colors"
              >
                <FiChevronLeft className="w-5 h-5" />
                <span>{currentIndex === 0 ? 'Choose Different Template' : 'Previous'}</span>
              </button>

              <div className="flex items-center space-x-2">
                <span className="text-sm text-gray-500">
                  Step {currentIndex + 1} of {steps.length}
                </span>
              </div>

              <button
                onClick={handleNext}
                className="flex items-center space-x-2 px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg transition-colors"
              >
                <span>{isLastStep ? 'Start Designing' : 'Next'}</span>
                <FiChevronRight className="w-5 h-5" />
              </button>
            </div>
          </motion.div>
        </AnimatePresence>
      </div>
    </div>
  );
};

export default OnboardingWizard;
