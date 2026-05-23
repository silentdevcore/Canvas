import { useDesignerStore } from '../../store';
import { DesignerElement, ElementId } from '../../domain';
import { PageSettings, PageMargins } from '../../domain';

/**
 * Adapter to bridge domain layer with Zustand store.
 * This allows the domain layer to remain pure while still using the existing store.
 */
export class ZustandStoreAdapter {
  // Element operations
  static async saveElement(element: DesignerElement): Promise<void> {
    useDesignerStore.getState().updateElementProps(element.id, element.props);
    // Update positioning and sizing if provided
    if (element.x !== undefined && element.y !== undefined) {
      useDesignerStore.getState().updateElementPosition(element.id, element.x, element.y);
    }
    if (element.width !== undefined && element.height !== undefined) {
      useDesignerStore.getState().updateElementSize(element.id, element.width, element.height);
    }
  }

  static async saveAllElements(elements: DesignerElement[]): Promise<void> {
    const store = useDesignerStore.getState();
    for (const element of elements) {
      await this.saveElement(element);
    }
  }

  static async findElementById(id: ElementId): Promise<DesignerElement | null> {
    const store = useDesignerStore.getState();
    const element = store.elements[id];
    if (!element) return null;

    return new DesignerElement({
      id: element.id,
      type: element.type,
      props: element.props,
      children: element.children,
      x: element.x,
      y: element.y,
      width: element.width,
      height: element.height,
      isGroup: element.isGroup,
      groupId: element.groupId,
      locked: element.locked
    });
  }

  static async findAllElements(): Promise<DesignerElement[]> {
    const store = useDesignerStore.getState();
    return Object.values(store.elements).map(element =>
      new DesignerElement({
        id: element.id,
        type: element.type,
        props: element.props,
        children: element.children,
        x: element.x,
        y: element.y,
        width: element.width,
        height: element.height,
        isGroup: element.isGroup,
        groupId: element.groupId,
        locked: element.locked
      })
    );
  }

  static async findElementsByParentId(parentId: ElementId): Promise<DesignerElement[]> {
    const store = useDesignerStore.getState();
    return Object.values(store.elements)
      .filter(element => element.children?.includes(parentId))
      .map(element => new DesignerElement({
        id: element.id,
        type: element.type,
        props: element.props,
        children: element.children,
        x: element.x,
        y: element.y,
        width: element.width,
        height: element.height,
        isGroup: element.isGroup,
        groupId: element.groupId,
        locked: element.locked
      }));
  }

  static async findRootElements(): Promise<DesignerElement[]> {
    const store = useDesignerStore.getState();
    return store.rootIds
      .map(id => store.elements[id])
      .filter(element => element !== undefined)
      .map(element => new DesignerElement({
        id: element.id,
        type: element.type,
        props: element.props,
        children: element.children,
        x: element.x,
        y: element.y,
        width: element.width,
        height: element.height,
        isGroup: element.isGroup,
        groupId: element.groupId,
        locked: element.locked
      }));
  }

  static async findElementsByType(type: string): Promise<DesignerElement[]> {
    const store = useDesignerStore.getState();
    return Object.values(store.elements)
      .filter(element => element.type === type)
      .map(element => new DesignerElement({
        id: element.id,
        type: element.type,
        props: element.props,
        children: element.children,
        x: element.x,
        y: element.y,
        width: element.width,
        height: element.height,
        isGroup: element.isGroup,
        groupId: element.groupId,
        locked: element.locked
      }));
  }

  static async deleteElementById(id: ElementId): Promise<void> {
    useDesignerStore.getState().deleteElement(id);
  }

  static async deleteAllElements(ids: ElementId[]): Promise<void> {
    const store = useDesignerStore.getState();
    for (const id of ids) {
      store.deleteElement(id);
    }
  }

  static async elementExists(id: ElementId): Promise<boolean> {
    const store = useDesignerStore.getState();
    return id in store.elements;
  }

  static async countElements(): Promise<number> {
    const store = useDesignerStore.getState();
    return Object.keys(store.elements).length;
  }

  static async clearAllElements(): Promise<void> {
    const store = useDesignerStore.getState();
    const elementIds = Object.keys(store.elements);
    for (const id of elementIds) {
      store.deleteElement(id);
    }
  }

  // Page settings operations
  static async savePageSettings(settings: PageSettings): Promise<void> {
    const store = useDesignerStore.getState();
    store.updatePageSettings({
      size: settings.size,
      orientation: settings.orientation,
      width: settings.width,
      height: settings.height,
      backgroundColor: settings.backgroundColor,
      margins: {
        top: settings.margins.top,
        right: settings.margins.right,
        bottom: settings.margins.bottom,
        left: settings.margins.left
      },
      title: settings.title,
      description: settings.description
    });
  }

  static async getPageSettings(): Promise<PageSettings | null> {
    const store = useDesignerStore.getState();
    const pageSettings = store.pageSettings;

    return new PageSettings(
      pageSettings.size,
      pageSettings.orientation,
      pageSettings.width,
      pageSettings.height,
      pageSettings.backgroundColor,
      new PageMargins(
        pageSettings.margins.top,
        pageSettings.margins.right,
        pageSettings.margins.bottom,
        pageSettings.margins.left
      ),
      pageSettings.title,
      pageSettings.description
    );
  }

  static async updatePageSettings(settings: PageSettings): Promise<void> {
    await this.savePageSettings(settings);
  }

  static async resetPageSettingsToDefaults(): Promise<void> {
    const store = useDesignerStore.getState();
    store.updatePageSettings({
      size: 'A4',
      orientation: 'Portrait',
      width: 794,
      height: 1123,
      backgroundColor: '#ffffff',
      margins: { top: 20, right: 20, bottom: 20, left: 20 },
      title: 'Untitled Document',
      description: ''
    });
  }

  static async pageSettingsExist(): Promise<boolean> {
    const settings = await this.getPageSettings();
    return settings !== null;
  }

  // Store state access for advanced operations
  static getStoreState() {
    return useDesignerStore.getState();
  }

  static subscribeToStore(callback: () => void) {
    return useDesignerStore.subscribe(callback);
  }
}