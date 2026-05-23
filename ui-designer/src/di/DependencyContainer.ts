import {
  IElementRepository,
  IPageRepository,
  ITemplateRepository,
  ElementValidationService
} from '../domain';

import {
  IAddElementUseCase,
  IUpdateElementUseCase,
  IDeleteElementUseCase,
  AddElementUseCase,
  UpdateElementUseCase,
  DeleteElementUseCase
} from '../application';

import {
  ZustandElementRepository,
  ZustandPageRepository,
  LocalStorageTemplateRepository
} from '../infrastructure';

/**
 * Dependency Injection Container for Clean Architecture.
 * Manages the creation and wiring of all dependencies.
 */
export class DependencyContainer {
  private static instance: DependencyContainer;

  // Domain Services
  private _elementValidationService!: ElementValidationService;

  // Repositories
  private _elementRepository!: IElementRepository;
  private _pageRepository!: IPageRepository;
  private _templateRepository!: ITemplateRepository;

  // Use Cases
  private _addElementUseCase!: IAddElementUseCase;
  private _updateElementUseCase!: IUpdateElementUseCase;
  private _deleteElementUseCase!: IDeleteElementUseCase;

  private constructor() {
    this.initializeDependencies();
  }

  public static getInstance(): DependencyContainer {
    if (!DependencyContainer.instance) {
      DependencyContainer.instance = new DependencyContainer();
    }
    return DependencyContainer.instance;
  }

  private initializeDependencies(): void {
    // Initialize Domain Services
    this._elementValidationService = new ElementValidationService();

    // Initialize Repositories (Infrastructure Layer)
    this._elementRepository = new ZustandElementRepository();
    this._pageRepository = new ZustandPageRepository();
    this._templateRepository = new LocalStorageTemplateRepository();

    // Initialize Use Cases (Application Layer)
    this._addElementUseCase = new AddElementUseCase(
      this._elementRepository,
      this._elementValidationService
    );

    this._updateElementUseCase = new UpdateElementUseCase(
      this._elementRepository,
      this._elementValidationService
    );

    this._deleteElementUseCase = new DeleteElementUseCase(
      this._elementRepository
    );
  }

  // Getters for Domain Services
  public get elementValidationService(): ElementValidationService {
    return this._elementValidationService;
  }

  // Getters for Repositories
  public get elementRepository(): IElementRepository {
    return this._elementRepository;
  }

  public get pageRepository(): IPageRepository {
    return this._pageRepository;
  }

  public get templateRepository(): ITemplateRepository {
    return this._templateRepository;
  }

  // Getters for Use Cases
  public get addElementUseCase(): IAddElementUseCase {
    return this._addElementUseCase;
  }

  public get updateElementUseCase(): IUpdateElementUseCase {
    return this._updateElementUseCase;
  }

  public get deleteElementUseCase(): IDeleteElementUseCase {
    return this._deleteElementUseCase;
  }

  /**
   * Reset the container (useful for testing)
   */
  public static reset(): void {
    DependencyContainer.instance = null as any;
  }
}