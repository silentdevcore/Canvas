import { useState, useEffect, useCallback } from 'react';
import { DependencyContainer } from '../../di';
import { DesignerElement, ElementId } from '../../domain';

/**
 * View Model for Canvas component following Clean Architecture.
 * Handles presentation logic and coordinates with application layer.
 */
export class CanvasViewModel {
  private container = DependencyContainer.getInstance();

  // UI State
  private _elements: DesignerElement[] = [];
  private _selectedElementId: ElementId | null = null;
  private _isLoading = false;
  private _error: string | null = null;

  // Getters for UI state
  get elements(): DesignerElement[] {
    return this._elements;
  }

  get selectedElementId(): ElementId | null {
    return this._selectedElementId;
  }

  get selectedElement(): DesignerElement | null {
    return this._selectedElementId
      ? this._elements.find(el => el.id === this._selectedElementId) || null
      : null;
  }

  get isLoading(): boolean {
    return this._isLoading;
  }

  get error(): string | null {
    return this._error;
  }

  // Business operations using Clean Architecture use cases
  async addElement(type: string, props: any, x?: number, y?: number): Promise<void> {
    try {
      this._isLoading = true;
      this._error = null;

      const result = await this.container.addElementUseCase.execute({
        type: type as any,
        props,
        x,
        y
      });

      if (!result.success) {
        this._error = result.error || 'Failed to add element';
        return;
      }

      // Refresh elements from repository
      await this.loadElements();

      // Select the newly added element
      this._selectedElementId = result.elementId;

    } catch (error) {
      this._error = error instanceof Error ? error.message : 'Unknown error';
    } finally {
      this._isLoading = false;
    }
  }

  async updateElement(elementId: ElementId, updates: {
    props?: any;
    x?: number;
    y?: number;
    width?: number;
    height?: number;
  }): Promise<void> {
    try {
      this._isLoading = true;
      this._error = null;

      const result = await this.container.updateElementUseCase.execute({
        elementId,
        ...updates
      });

      if (!result.success) {
        this._error = result.error || 'Failed to update element';
        return;
      }

      // Refresh elements from repository
      await this.loadElements();

    } catch (error) {
      this._error = error instanceof Error ? error.message : 'Unknown error';
    } finally {
      this._isLoading = false;
    }
  }

  async deleteElement(elementId: ElementId): Promise<void> {
    try {
      this._isLoading = true;
      this._error = null;

      const result = await this.container.deleteElementUseCase.execute({
        elementId
      });

      if (!result.success) {
        this._error = result.error || 'Failed to delete element';
        return;
      }

      // Refresh elements from repository
      await this.loadElements();

      // Clear selection if deleted element was selected
      if (this._selectedElementId === elementId) {
        this._selectedElementId = null;
      }

    } catch (error) {
      this._error = error instanceof Error ? error.message : 'Unknown error';
    } finally {
      this._isLoading = false;
    }
  }

  selectElement(elementId: ElementId | null): void {
    this._selectedElementId = elementId;
  }

  // Data loading
  async loadElements(): Promise<void> {
    try {
      this._elements = await this.container.elementRepository.findAll();
    } catch (error) {
      this._error = error instanceof Error ? error.message : 'Failed to load elements';
    }
  }

  // Utility methods
  clearError(): void {
    this._error = null;
  }

  // Initialization
  async initialize(): Promise<void> {
    await this.loadElements();
  }
}

/**
 * React hook that provides CanvasViewModel instance.
 * Manages view model lifecycle and state synchronization.
 */
export const useCanvasViewModel = () => {
  const [viewModel] = useState(() => new CanvasViewModel());

  // Initialize on mount
  useEffect(() => {
    viewModel.initialize();
  }, [viewModel]);

  // State for React re-renders
  const [elements, setElements] = useState(viewModel.elements);
  const [selectedElementId, setSelectedElementId] = useState(viewModel.selectedElementId);
  const [isLoading, setIsLoading] = useState(viewModel.isLoading);
  const [error, setError] = useState(viewModel.error);

  // Sync state with view model
  useEffect(() => {
    const syncState = () => {
      setElements([...viewModel.elements]);
      setSelectedElementId(viewModel.selectedElementId);
      setIsLoading(viewModel.isLoading);
      setError(viewModel.error);
    };

    // Initial sync
    syncState();

    // Set up polling for state changes (in a real app, you'd use events or observables)
    const interval = setInterval(syncState, 100);

    return () => clearInterval(interval);
  }, [viewModel]);

  // Wrapped methods that trigger re-renders
  const addElement = useCallback(async (type: string, props: any, x?: number, y?: number) => {
    await viewModel.addElement(type, props, x, y);
    setElements([...viewModel.elements]);
    setSelectedElementId(viewModel.selectedElementId);
    setIsLoading(viewModel.isLoading);
    setError(viewModel.error);
  }, [viewModel]);

  const updateElement = useCallback(async (elementId: ElementId, updates: any) => {
    await viewModel.updateElement(elementId, updates);
    setElements([...viewModel.elements]);
    setIsLoading(viewModel.isLoading);
    setError(viewModel.error);
  }, [viewModel]);

  const deleteElement = useCallback(async (elementId: ElementId) => {
    await viewModel.deleteElement(elementId);
    setElements([...viewModel.elements]);
    setSelectedElementId(viewModel.selectedElementId);
    setIsLoading(viewModel.isLoading);
    setError(viewModel.error);
  }, [viewModel]);

  const selectElement = useCallback((elementId: ElementId | null) => {
    viewModel.selectElement(elementId);
    setSelectedElementId(elementId);
  }, [viewModel]);

  const clearError = useCallback(() => {
    viewModel.clearError();
    setError(null);
  }, [viewModel]);

  return {
    elements,
    selectedElementId,
    selectedElement: viewModel.selectedElement,
    isLoading,
    error,
    addElement,
    updateElement,
    deleteElement,
    selectElement,
    clearError
  };
};