import apiClient from './apiClient';
import { MenuCategoryDto, MenuItemDto } from '@/types';

// TODO: Define Upsert DTOs if needed for creating/updating categories/items
// interface MenuCategoryUpsertDto { ... }
// interface MenuItemUpsertDto { ... }

const menuService = {
  /**
   * Fetches all menu categories for the user's organization.
   * Assumes the backend filters by organization context automatically.
   */
  getCategories: async (areaId: number): Promise<MenuCategoryDto[]> => {
    const url = areaId ? `/MenuCategories?areaId=${areaId}` : '/MenuCategories';
    const response = await apiClient.get<MenuCategoryDto[]>(url);
    return response.data;
  },

  /**
   * Fetches menu items, optionally filtering by category ID.
   */
  getItems: async (categoryId?: number): Promise<MenuItemDto[]> => {
    const url = categoryId ? `/MenuItems?categoryId=${categoryId}` : '/MenuItems';
    const response = await apiClient.get<MenuItemDto[]>(url);
    return response.data;
  },

  // TODO: Add functions for creating, updating, deleting categories
  // createCategory: async (data: MenuCategoryUpsertDto): Promise<MenuCategoryDto> => { ... }
  // updateCategory: async (id: number, data: MenuCategoryUpsertDto): Promise<void> => { ... }
  // deleteCategory: async (id: number): Promise<void> => { ... }

  // TODO: Add functions for creating, updating, deleting items
  // createItem: async (data: MenuItemUpsertDto): Promise<MenuItemDto> => { ... }
  // updateItem: async (id: number, data: MenuItemUpsertDto): Promise<void> => { ... }
  // deleteItem: async (id: number): Promise<void> => { ... }

  /**
   * Updates the stock (Scorta) for a specific menu item.
   * @param menuItemId The ID of the menu item.
   * @param newScorta The new stock quantity, or null for unlimited.
   */
  updateMenuItemStock: async (menuItemId: number, newScorta: number | null): Promise<void> => {
    const response = await apiClient.put(`/menuitems/${menuItemId}/stock`, { newScorta });
    // Assuming backend returns 200/204 on success, no specific data needed back.
    // If backend returns the updated item, change Promise<MenuItemDto> and return response.data
    return response.data; // Or just return; if no data is expected/needed
  },

  /**
   * Resets the stock (Scorta) for a specific menu item to unlimited (null).
   * @param menuItemId The ID of the menu item.
   */
  resetMenuItemStock: async (menuItemId: number): Promise<void> => {
    const response = await apiClient.post(`/menuitems/${menuItemId}/stock/reset`);
    // Assuming backend returns 200/204 on success.
    return response.data; // Or just return;
  },

  /**
   * Resets the stock for ALL menu items within a specific area to unlimited.
   * @param areaId The ID of the area.
   */
  resetAllStockForArea: async (areaId: number): Promise<void> => {
    const response = await apiClient.post(`/areas/${areaId}/stock/reset-all`);
    // Assuming backend returns 200/204 on success.
    return response.data; // Or just return;
  },
};

export default menuService;
