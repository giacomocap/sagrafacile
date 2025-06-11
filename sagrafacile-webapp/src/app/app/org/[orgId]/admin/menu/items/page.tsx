'use client';

import React, { useState, useEffect } from 'react';
import apiClient from '@/services/apiClient';
import menuService from '@/services/menuService';
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { toast } from "sonner";// Added for reset all stock notification
import { Checkbox } from '@/components/ui/checkbox';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter, DialogClose
} from '@/components/ui/dialog';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger
} from "@/components/ui/alert-dialog";
import {  MenuCategoryDto as GlobalMenuCategoryDto, MenuItemDto as GlobalMenuItemDto } from '@/types';
import AdminAreaSelector from '@/components/shared/AdminAreaSelector';

// Interfaces based on DataStructures.md
// Local MenuCategory and MenuItem interfaces removed, will use GlobalMenuCategoryDto and GlobalMenuItemDto from @/types

// DTO for creating/updating menu items
interface MenuItemUpsertDto {
  name: string;
  description?: string;
  price: number;
  menuCategoryId: number;
  isNoteRequired: boolean;
  noteSuggestion?: string; // Changed to string | undefined
  scorta?: number | null; // Added for Stock Management
}


export default function MenuItemsPage() {
  const [selectedAreaId, setSelectedAreaId] = useState<string | undefined>(undefined);
  const [categories, setCategories] = useState<GlobalMenuCategoryDto[]>([]);
  // const [selectedCategoryId, setSelectedCategoryId] = useState<string | undefined>(undefined); // Removed
  const [items, setItems] = useState<(GlobalMenuItemDto & { categoryName?: string })[]>([]);
  const [isLoadingCategories, setIsLoadingCategories] = useState(false);
  const [isLoadingItems, setIsLoadingItems] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // --- Dialog States ---
  // Add
  const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
  const [newItemData, setNewItemData] = useState<Partial<MenuItemUpsertDto>>({});
  const [addError, setAddError] = useState<string | null>(null);
  // Edit
  const [editingItem, setEditingItem] = useState<GlobalMenuItemDto | null>(null);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [editItemData, setEditItemData] = useState<Partial<MenuItemUpsertDto>>({});
  const [editError, setEditError] = useState<string | null>(null);
  // Delete
  const [itemToDelete, setItemToDelete] = useState<GlobalMenuItemDto | null>(null);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  // Reset Stock
  const [itemToResetStock, setItemToResetStock] = useState<GlobalMenuItemDto | null>(null);
  const [isResetStockDialogOpen, setIsResetStockDialogOpen] = useState(false);
  const [resetStockError, setResetStockError] = useState<string | null>(null);

  // Reset All Stock for Area
  const [isResetAllStockAreaDialogOpen, setIsResetAllStockAreaDialogOpen] = useState(false);
  const [resetAllStockAreaError, setResetAllStockAreaError] = useState<string | null>(null);

  // Fetch Categories when Area changes
  // Fetch Categories and then all items for the area
  const fetchAllItemsForArea = async (areaId: string, areaCategories: GlobalMenuCategoryDto[]) => {
    setIsLoadingItems(true);
    setError(null);
    setItems([]); // Clear previous items

    if (areaCategories.length === 0) {
      // toast.info(`Nessuna categoria menu trovata per l'area selezionata.`);
      setIsLoadingItems(false);
      return;
    }

    try {
      const categoryIds = areaCategories.map((cat) => cat.id);
      const itemPromises = categoryIds.map((catId) =>
        apiClient.get<GlobalMenuItemDto[]>(`/MenuItems`, { params: { categoryId: catId } })
      );
      const itemResponses = await Promise.all(itemPromises);

      const allItemsRaw = itemResponses.flatMap((res) => res.data || []);

      const allItemsWithCategoryName = allItemsRaw.map((item) => {
        const category = areaCategories.find((cat) => cat.id === item.menuCategoryId);
        return { ...item, categoryName: category?.name || 'Senza Categoria' };
      });

      setItems(allItemsWithCategoryName);

      // if (allItemsWithCategoryName.length === 0) {
      //     toast.info(`Nessun prodotto menu trovato per l'area selezionata.`);
      // }
    } catch (error) {
      console.error(`Error fetching menu items for area ${areaId}:`, error);
      setError(`Impossibile caricare i prodotti del menu per l'area selezionata.`);
      setItems([]);
    } finally {
      setIsLoadingItems(false);
    }
  };

  useEffect(() => {
    const fetchCategoriesAndItems = async () => {
      if (!selectedAreaId) {
        setCategories([]);
        setItems([]);
        return;
      }
      setIsLoadingCategories(true);
      setError(null);
      try {
        const response = await apiClient.get<GlobalMenuCategoryDto[]>(`/MenuCategories`, {
          params: { areaId: selectedAreaId },
        });
        const fetchedCategories = response.data || [];
        setCategories(fetchedCategories);

        // After fetching categories, fetch all items for the area
        await fetchAllItemsForArea(selectedAreaId, fetchedCategories);

      } catch (err) {
        console.error('Error fetching categories:', err);
        setError(`Caricamento categorie per l'area selezionata fallito.`);
        setCategories([]);
        setItems([]);
      } finally {
        setIsLoadingCategories(false);
      }
    };
    fetchCategoriesAndItems();
  }, [selectedAreaId]);

  // --- Event Handlers ---

  const handleAreaChange = (value?: string) => {
    setSelectedAreaId(value);
    // Categories and items will be refetched by the useEffect hook
  };

  // handleCategoryChange is no longer needed

  // --- Add Item ---
  const handleOpenAddDialog = () => {
    if (categories.length === 0) {
      toast.error("Devi prima creare almeno una categoria di menu in quest'area.");
      return;
    }
    setNewItemData({ // Reset form
      name: '',
      description: '',
      price: 0,
      isNoteRequired: false,
      noteSuggestion: '',
      menuCategoryId: categories[0]?.id, // Pre-select first category if available
      scorta: null, // Default to unlimited
    });
    setAddError(null);
    setIsAddDialogOpen(true);
  };

  const handleAddItem = async () => {
    if (!newItemData.menuCategoryId || !newItemData.name || newItemData.price == null) {
      setAddError("Categoria, Nome e Prezzo sono obbligatori.");
      return;
    }
    setAddError(null);

    const dataToSend: MenuItemUpsertDto = {
      name: newItemData.name.trim(),
      description: newItemData.description?.trim() || undefined,
      price: Number(newItemData.price),
      menuCategoryId: Number(newItemData.menuCategoryId),
      isNoteRequired: newItemData.isNoteRequired ?? false,
      noteSuggestion: newItemData.noteSuggestion?.trim() || undefined,
      scorta: newItemData.scorta === undefined ? null : newItemData.scorta,
    };

    try {
      await apiClient.post<GlobalMenuItemDto>('/MenuItems', dataToSend);
      if (selectedAreaId) {
        await fetchAllItemsForArea(selectedAreaId, categories); // Refresh list
      }
      setIsAddDialogOpen(false);
    } catch (err: unknown) {
      console.error('Error adding menu item:', err);
      const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
      const errorMessage = errorResponse?.title || errorResponse?.message || 'Aggiunta prodotto menu fallita.';
      setAddError(errorMessage);
    }
  };

  // --- Edit Item ---
  const handleOpenEditDialog = (item: GlobalMenuItemDto) => {
    setEditingItem(item);
    setEditItemData({ // Pre-fill form
      name: item.name,
      description: item.description!,
      price: item.price,
      isNoteRequired: item.isNoteRequired,
      noteSuggestion: item.noteSuggestion === null ? undefined : item.noteSuggestion, // Handle null explicitly
      menuCategoryId: item.menuCategoryId, // Keep original category ID
      scorta: item.scorta === undefined ? null : item.scorta, // Pre-fill scorta
    });
    setEditError(null);
    setIsEditDialogOpen(true);
  };

  const handleEditItem = async () => {
    if (!editingItem || !editItemData.menuCategoryId || !editItemData.name || editItemData.price == null) {
      setEditError("Categoria, Nome e Prezzo sono obbligatori.");
      return;
    }
    setEditError(null);

    const dataToSend: MenuItemUpsertDto = {
      name: editItemData.name.trim(),
      description: editItemData.description?.trim() || undefined,
      price: Number(editItemData.price),
      menuCategoryId: Number(editItemData.menuCategoryId || editingItem.menuCategoryId), // Use new category ID from dialog
      isNoteRequired: editItemData.isNoteRequired ?? false,
      noteSuggestion: editItemData.noteSuggestion?.trim() || undefined,
      scorta: editItemData.scorta === undefined ? null : editItemData.scorta,
    };

    try {
      await apiClient.put(`/MenuItems/${editingItem.id}`, dataToSend);
      if (selectedAreaId) {
        await fetchAllItemsForArea(selectedAreaId, categories); // Refresh list
      }
      setIsEditDialogOpen(false);
      setEditingItem(null);
    } catch (err: unknown) {
      console.error('Error updating menu item:', err);
      const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
      const errorMessage = errorResponse?.title || errorResponse?.message || 'Aggiornamento prodotto menu fallito.';
      setEditError(errorMessage);
    }
  };

  // --- Delete Item ---
  const handleOpenDeleteDialog = (item: GlobalMenuItemDto) => {
    setItemToDelete(item);
    setDeleteError(null);
    setIsDeleteDialogOpen(true);
  };

  const handleDeleteItem = async () => {
    if (!itemToDelete) return;
    setDeleteError(null);

    try {
      await apiClient.delete(`/MenuItems/${itemToDelete.id}`);
      if (selectedAreaId) {
        await fetchAllItemsForArea(selectedAreaId, categories); // Refresh list
      }
      setIsDeleteDialogOpen(false);
      setItemToDelete(null);
    } catch (err: unknown) {
      console.error('Error deleting menu item:', err);
      const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
      const errorMessage = errorResponse?.title || errorResponse?.message || 'Eliminazione prodotto menu fallita.';
      setDeleteError(errorMessage);
    }
  };

  // --- Render ---
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Manage Menu Items</h1>

      <AdminAreaSelector
        selectedAreaId={selectedAreaId}
        onAreaChange={handleAreaChange}
        title="Seleziona Area"
        description="Scegli un'area per gestire i suoi prodotti del menu."
      />

      {/* Menu Items Table (conditional on selectedAreaId and categories loaded) */}
      {selectedAreaId && !isLoadingCategories && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <div>
              <CardTitle>Menu Items</CardTitle>
              <CardDescription>
                {categories.length > 0 ? `Manage items for the selected area.` : `No menu categories found for this area. Please add categories first.`}
              </CardDescription>
            </div>
            <div className="flex space-x-2">
              {/* Refresh Items Button */}
              <Button
                variant="outline"
                size="sm"
                onClick={async () => {
                  if (selectedAreaId && categories) {
                    toast.info("Refreshing items...");
                    await fetchAllItemsForArea(selectedAreaId, categories);
                    toast.success("Items refreshed.");
                  }
                }}
                disabled={!selectedAreaId || isLoadingItems || isLoadingCategories}
              >
                Refresh Items
              </Button>

              {/* Reset All Stock for Area Button Trigger */}
              <AlertDialog open={isResetAllStockAreaDialogOpen} onOpenChange={setIsResetAllStockAreaDialogOpen}>
                <AlertDialogTrigger asChild>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => { setResetAllStockAreaError(null); setIsResetAllStockAreaDialogOpen(true); }}
                    disabled={!selectedAreaId || items.length === 0}
                  >
                    Reset All Area Stock
                  </Button>
                </AlertDialogTrigger>
                <AlertDialogContent>
                  <AlertDialogHeader>
                    <AlertDialogTitle>Are you sure?</AlertDialogTitle>
                    <AlertDialogDescription>
                      This will reset the stock for ALL menu items in the selected area to "Unlimited". This action cannot be undone.
                    </AlertDialogDescription>
                  </AlertDialogHeader>
                  {resetAllStockAreaError && <p className="text-red-500 text-sm">{resetAllStockAreaError}</p>}
                  <AlertDialogFooter>
                    <AlertDialogCancel>Cancel</AlertDialogCancel>
                    <AlertDialogAction onClick={async () => {
                      if (!selectedAreaId) return;
                      setResetAllStockAreaError(null);
                      try {
                        await apiClient.post(`/areas/${selectedAreaId}/stock/reset-all`);
                        toast.success("Stock for all items in the area has been reset.");
                        if (selectedAreaId) {
                          await fetchAllItemsForArea(selectedAreaId, categories); // Refresh list
                        }
                        setIsResetAllStockAreaDialogOpen(false);
                      } catch (err: unknown) {
                        console.error('Error resetting all area stock:', err);
                        const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
                        const errorMessage = errorResponse?.title || errorResponse?.message || 'Failed to reset all area stock.';
                        setResetAllStockAreaError(errorMessage);
                        toast.error(errorMessage);
                      }
                    }}>Confirm Reset All</AlertDialogAction>
                  </AlertDialogFooter>
                </AlertDialogContent>
              </AlertDialog>

              {/* Add Item Dialog Trigger */}
              <Dialog open={isAddDialogOpen} onOpenChange={setIsAddDialogOpen}>
                <DialogTrigger asChild>
                  <Button size="sm" onClick={handleOpenAddDialog} disabled={!selectedAreaId || categories.length === 0}>Add New Item</Button>
                </DialogTrigger>
                <DialogContent className="sm:max-w-md">
                  <DialogHeader><DialogTitle>Add New Menu Item</DialogTitle></DialogHeader>
                  {/* Add Item Form */}
                  <div className="grid gap-4 py-4">
                    {/* Category */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-category" className="text-right">Category*</Label>
                      <Select
                        value={newItemData.menuCategoryId?.toString()}
                        onValueChange={(value) => setNewItemData({ ...newItemData, menuCategoryId: parseInt(value, 10) })}
                      >
                        <SelectTrigger className="col-span-3">
                          <SelectValue placeholder="Select a category" />
                        </SelectTrigger>
                        <SelectContent>
                          {categories.map((cat) => (
                            <SelectItem key={cat.id} value={cat.id.toString()}>
                              {cat.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    {/* Name */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-name" className="text-right">Name*</Label>
                      <Input id="add-name" value={newItemData.name || ''} onChange={(e) => setNewItemData({ ...newItemData, name: e.target.value })} className="col-span-3" />
                    </div>
                    {/* Description */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-desc" className="text-right">Description</Label>
                      <Textarea id="add-desc" value={newItemData.description || ''} onChange={(e) => setNewItemData({ ...newItemData, description: e.target.value })} className="col-span-3" />
                    </div>
                    {/* Price */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-price" className="text-right">Price* (€)</Label>
                      <Input id="add-price" type="number" step="0.01" value={newItemData.price || ''} onChange={(e) => setNewItemData({ ...newItemData, price: parseFloat(e.target.value) || 0 })} className="col-span-3" />
                    </div>
                    {/* Note Required */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-note-req" className="text-right">Note Required?</Label>
                      <Checkbox id="add-note-req" checked={newItemData.isNoteRequired} onCheckedChange={(checked) => setNewItemData({ ...newItemData, isNoteRequired: Boolean(checked) })} className="col-span-3 justify-self-start" />
                    </div>
                    {/* Note Suggestion (conditional) */}
                    {newItemData.isNoteRequired && (
                      <div className="grid grid-cols-4 items-center gap-4">
                        <Label htmlFor="add-note-sug" className="text-right">Note Suggestion</Label>
                        <Input id="add-note-sug" value={newItemData.noteSuggestion || ''} onChange={(e) => setNewItemData({ ...newItemData, noteSuggestion: e.target.value })} className="col-span-3" />
                      </div>
                    )}
                    {/* Scorta */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-scorta" className="text-right">Stock (Scorta)</Label>
                      <Input
                        id="add-scorta"
                        type="number"
                        step="1"
                        placeholder="Unlimited"
                        value={newItemData.scorta === null || newItemData.scorta === undefined ? '' : newItemData.scorta.toString()}
                        onChange={(e) => {
                          const val = e.target.value;
                          setNewItemData({ ...newItemData, scorta: val === '' ? null : parseInt(val, 10) });
                        }}
                        className="col-span-3"
                      />
                    </div>
                    {addError && <p className="col-span-4 text-red-500 text-sm text-center">{addError}</p>}
                  </div>
                  <DialogFooter>
                    <DialogClose asChild><Button type="button" variant="outline">Cancel</Button></DialogClose>
                    <Button type="submit" onClick={handleAddItem} disabled={!newItemData.name || newItemData.price == null}>Save Item</Button>
                  </DialogFooter>
                </DialogContent>
              </Dialog>
            </div>
          </CardHeader>
          <CardContent>
            {isLoadingItems ? <p>Loading items...</p> : error ? <p className="text-red-500">{error}</p> : items.length > 0 ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>ID</TableHead>
                    <TableHead>Name</TableHead>
                    <TableHead>Category</TableHead>
                    <TableHead>Price</TableHead>
                    <TableHead>Stock</TableHead>
                    <TableHead>Note Req.</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell>{item.id}</TableCell>
                      <TableCell className="font-medium">{item.name}</TableCell>
                      <TableCell>{item.categoryName}</TableCell>
                      <TableCell>€{item.price.toFixed(2)}</TableCell>
                      <TableCell>
                        {item.scorta === null || item.scorta === undefined ? 'Unlimited' : item.scorta === 0 ? <span className="text-red-500">Out of Stock</span> : item.scorta}
                      </TableCell>
                      <TableCell>{item.isNoteRequired ? 'Yes' : 'No'}</TableCell>
                      <TableCell className="text-right space-x-2">
                        {/* Edit Button Trigger */}
                        <Button variant="outline" size="sm" onClick={() => handleOpenEditDialog(item)}>Edit</Button>
                        {/* Reset Stock Button Trigger */}
                        <AlertDialog open={isResetStockDialogOpen && itemToResetStock?.id === item.id} onOpenChange={(open) => { if (!open) setItemToResetStock(null); setIsResetStockDialogOpen(open); }}>
                          <AlertDialogTrigger asChild>
                            <Button variant="secondary" size="sm" onClick={() => { setItemToResetStock(item); setResetStockError(null); setIsResetStockDialogOpen(true); }}>Reset Stock</Button>
                          </AlertDialogTrigger>
                          <AlertDialogContent>
                            <AlertDialogHeader>
                              <AlertDialogTitle>Are you sure?</AlertDialogTitle>
                              <AlertDialogDescription>
                                Reset stock for item "{itemToResetStock?.name}" to unlimited?
                              </AlertDialogDescription>
                            </AlertDialogHeader>
                            {resetStockError && <p className="text-red-500 text-sm">{resetStockError}</p>}
                            <AlertDialogFooter>
                              <AlertDialogCancel onClick={() => setItemToResetStock(null)}>Cancel</AlertDialogCancel>
                              <AlertDialogAction onClick={async () => {
                                if (!itemToResetStock) return;
                                setResetStockError(null);
                                try {
                                  await menuService.resetMenuItemStock(itemToResetStock.id);
                                  if (selectedAreaId) { // Refresh all items for the area
                                    await fetchAllItemsForArea(selectedAreaId, categories);
                                  }
                                  setIsResetStockDialogOpen(false);
                                  setItemToResetStock(null);
                                } catch (err: unknown) {
                                  console.error('Error resetting stock:', err);
                                  const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
                                  const errorMessage = errorResponse?.title || errorResponse?.message || 'Failed to reset stock.';
                                  setResetStockError(errorMessage);
                                }
                              }}>Confirm Reset</AlertDialogAction>
                            </AlertDialogFooter>
                          </AlertDialogContent>
                        </AlertDialog>
                        {/* Delete Button Trigger */}
                        <AlertDialog open={isDeleteDialogOpen && itemToDelete?.id === item.id} onOpenChange={(open) => { if (!open) setItemToDelete(null); setIsDeleteDialogOpen(open); }}>
                          <AlertDialogTrigger asChild>
                            <Button variant="destructive" size="sm" onClick={() => handleOpenDeleteDialog(item)}>Delete</Button>
                          </AlertDialogTrigger>
                          <AlertDialogContent>
                            <AlertDialogHeader>
                              <AlertDialogTitle>Are you sure?</AlertDialogTitle>
                              <AlertDialogDescription>
                                Delete item "{itemToDelete?.name}"? This cannot be undone.
                              </AlertDialogDescription>
                            </AlertDialogHeader>
                            {deleteError && <p className="text-red-500 text-sm">{deleteError}</p>}
                            <AlertDialogFooter>
                              <AlertDialogCancel onClick={() => setItemToDelete(null)}>Cancel</AlertDialogCancel>
                              <AlertDialogAction onClick={handleDeleteItem}>Continue</AlertDialogAction>
                            </AlertDialogFooter>
                          </AlertDialogContent>
                        </AlertDialog>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : <p>No menu items found for this category.</p>}
          </CardContent>
        </Card>
      )}

      {/* Edit Item Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader><DialogTitle>Edit Menu Item</DialogTitle></DialogHeader>
          {/* Edit Item Form */}
          <div className="grid gap-4 py-4">
            {/* Category */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-category" className="text-right">Category*</Label>
              <Select
                value={editItemData.menuCategoryId?.toString() || editingItem?.menuCategoryId.toString()}
                onValueChange={(value) => setEditItemData({ ...editItemData, menuCategoryId: parseInt(value, 10) })}
              >
                <SelectTrigger className="col-span-3">
                  <SelectValue placeholder="Select a category" />
                </SelectTrigger>
                <SelectContent>
                  {categories.map((cat) => (
                    <SelectItem key={cat.id} value={cat.id.toString()}>
                      {cat.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {/* Name */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-name" className="text-right">Name*</Label>
              <Input id="edit-name" value={editItemData.name || ''} onChange={(e) => setEditItemData({ ...editItemData, name: e.target.value })} className="col-span-3" />
            </div>
            {/* Description */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-desc" className="text-right">Description</Label>
              <Textarea id="edit-desc" value={editItemData.description || ''} onChange={(e) => setEditItemData({ ...editItemData, description: e.target.value })} className="col-span-3" />
            </div>
            {/* Price */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-price" className="text-right">Price* (€)</Label>
              <Input id="edit-price" type="number" step="0.01" value={editItemData.price || ''} onChange={(e) => setEditItemData({ ...editItemData, price: parseFloat(e.target.value) || 0 })} className="col-span-3" />
            </div>
            {/* Note Required */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-note-req" className="text-right">Note Required?</Label>
              <Checkbox id="edit-note-req" checked={editItemData.isNoteRequired} onCheckedChange={(checked) => setEditItemData({ ...editItemData, isNoteRequired: Boolean(checked) })} className="col-span-3 justify-self-start" />
            </div>
            {/* Note Suggestion (conditional) */}
            {editItemData.isNoteRequired && (
              <div className="grid grid-cols-4 items-center gap-4">
                <Label htmlFor="edit-note-sug" className="text-right">Note Suggestion</Label>
                <Input id="edit-note-sug" value={editItemData.noteSuggestion || ''} onChange={(e) => setEditItemData({ ...editItemData, noteSuggestion: e.target.value })} className="col-span-3" />
              </div>
            )}
            {/* Scorta */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-scorta" className="text-right">Stock (Scorta)</Label>
              <Input
                id="edit-scorta"
                type="number"
                step="1"
                placeholder="Unlimited"
                value={editItemData.scorta === null || editItemData.scorta === undefined ? '' : editItemData.scorta.toString()}
                onChange={(e) => {
                  const val = e.target.value;
                  setEditItemData({ ...editItemData, scorta: val === '' ? null : parseInt(val, 10) });
                }}
                className="col-span-3"
              />
            </div>
            {editError && <p className="col-span-4 text-red-500 text-sm text-center">{editError}</p>}
          </div>
          <DialogFooter>
            <DialogClose asChild><Button type="button" variant="outline">Cancel</Button></DialogClose>
            <Button type="submit" onClick={handleEditItem} disabled={!editItemData.name || editItemData.price == null || !editItemData.menuCategoryId}>Save Changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

    </div>
  );
}
