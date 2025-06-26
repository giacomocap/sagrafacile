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
      <h1 className="text-2xl font-bold">Gestione Prodotti Menu</h1>

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
              <CardTitle>Prodotti Menu</CardTitle>
              <CardDescription>
                {categories.length > 0 ? `Gestisci i prodotti per l'area selezionata.` : `Nessuna categoria di menu trovata per quest'area. Aggiungi prima le categorie.`}
              </CardDescription>
            </div>
            <div className="flex space-x-2">
              {/* Refresh Items Button */}
              <Button
                variant="outline"
                size="sm"
                onClick={async () => {
                  if (selectedAreaId && categories) {
                    toast.info("Aggiornamento prodotti...");
                    await fetchAllItemsForArea(selectedAreaId, categories);
                    toast.success("Prodotti aggiornati.");
                  }
                }}
                disabled={!selectedAreaId || isLoadingItems || isLoadingCategories}
              >
                Aggiorna Prodotti
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
                    Reimposta Tutte le Scorte dell'Area
                  </Button>
                </AlertDialogTrigger>
                <AlertDialogContent>
                  <AlertDialogHeader>
                    <AlertDialogTitle>Sei sicuro?</AlertDialogTitle>
                    <AlertDialogDescription>
                      Questo reimposterà la scorta per TUTTI i prodotti del menu nell'area selezionata a "Illimitata". Questa azione non può essere annullata.
                    </AlertDialogDescription>
                  </AlertDialogHeader>
                  {resetAllStockAreaError && <p className="text-red-500 text-sm">{resetAllStockAreaError}</p>}
                  <AlertDialogFooter>
                    <AlertDialogCancel>Annulla</AlertDialogCancel>
                    <AlertDialogAction onClick={async () => {
                      if (!selectedAreaId) return;
                      setResetAllStockAreaError(null);
                      try {
                        await apiClient.post(`/areas/${selectedAreaId}/stock/reset-all`);
                        toast.success("Scorta per tutti i prodotti nell'area è stata reimpostata.");
                        if (selectedAreaId) {
                          await fetchAllItemsForArea(selectedAreaId, categories); // Refresh list
                        }
                        setIsResetAllStockAreaDialogOpen(false);
                      } catch (err: unknown) {
                        console.error('Error resetting all area stock:', err);
                        const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
                        const errorMessage = errorResponse?.title || errorResponse?.message || 'Impossibile reimpostare tutte le scorte dell\'area.';
                        setResetAllStockAreaError(errorMessage);
                        toast.error(errorMessage);
                      }
                    }}>Conferma Reimposta Tutto</AlertDialogAction>
                  </AlertDialogFooter>
                </AlertDialogContent>
              </AlertDialog>

              {/* Add Item Dialog Trigger */}
              <Dialog open={isAddDialogOpen} onOpenChange={setIsAddDialogOpen}>
                <DialogTrigger asChild>
                  <Button size="sm" onClick={handleOpenAddDialog} disabled={!selectedAreaId || categories.length === 0}>Aggiungi Nuovo Prodotto</Button>
                </DialogTrigger>
                <DialogContent className="sm:max-w-md overflow-y-scroll max-h-screen">
                  <DialogHeader><DialogTitle>Aggiungi Nuovo Prodotto Menu</DialogTitle></DialogHeader>
                  {/* Add Item Form */}
                  <div className="grid gap-4 py-4">
                    {/* Category */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-category" className="text-right">Categoria*</Label>
                      <Select
                        value={newItemData.menuCategoryId?.toString()}
                        onValueChange={(value) => setNewItemData({ ...newItemData, menuCategoryId: parseInt(value, 10) })}
                      >
                        <SelectTrigger className="col-span-3">
                          <SelectValue placeholder="Seleziona una categoria" />
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
                      <Label htmlFor="add-name" className="text-right">Nome*</Label>
                      <Input id="add-name" value={newItemData.name || ''} onChange={(e) => setNewItemData({ ...newItemData, name: e.target.value })} className="col-span-3" />
                    </div>
                    {/* Description */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-desc" className="text-right">Descrizione</Label>
                      <Textarea id="add-desc" value={newItemData.description || ''} onChange={(e) => setNewItemData({ ...newItemData, description: e.target.value })} className="col-span-3" />
                    </div>
                    {/* Price */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-price" className="text-right">Prezzo* (€)</Label>
                      <Input id="add-price" type="number" step="0.01" value={newItemData.price || ''} onChange={(e) => setNewItemData({ ...newItemData, price: parseFloat(e.target.value) || 0 })} className="col-span-3" />
                    </div>
                    {/* Note Required */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-note-req" className="text-right">Nota Obbligatoria?</Label>
                      <Checkbox id="add-note-req" checked={newItemData.isNoteRequired} onCheckedChange={(checked) => setNewItemData({ ...newItemData, isNoteRequired: Boolean(checked) })} className="col-span-3 justify-self-start" />
                    </div>
                    {/* Note Suggestion (conditional) */}
                    {newItemData.isNoteRequired && (
                      <div className="grid grid-cols-4 items-center gap-4">
                        <Label htmlFor="add-note-sug" className="text-right">Suggerimento Nota</Label>
                        <Input id="add-note-sug" value={newItemData.noteSuggestion || ''} onChange={(e) => setNewItemData({ ...newItemData, noteSuggestion: e.target.value })} className="col-span-3" />
                      </div>
                    )}
                    {/* Scorta */}
                    <div className="grid grid-cols-4 items-center gap-4">
                      <Label htmlFor="add-scorta" className="text-right">Scorta</Label>
                      <Input
                        id="add-scorta"
                        type="number"
                        step="1"
                        placeholder="Illimitata"
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
                    <DialogClose asChild><Button type="button" variant="outline">Annulla</Button></DialogClose>
                    <Button type="submit" onClick={handleAddItem} disabled={!newItemData.name || newItemData.price == null}>Salva Prodotto</Button>
                  </DialogFooter>
                </DialogContent>
              </Dialog>
            </div>
          </CardHeader>
          <CardContent>
            {isLoadingItems ? <p>Caricamento prodotti...</p> : error ? <p className="text-red-500">{error}</p> : items.length > 0 ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>ID</TableHead>
                    <TableHead>Nome</TableHead>
                    <TableHead>Categoria</TableHead>
                    <TableHead>Prezzo</TableHead>
                    <TableHead>Scorta</TableHead>
                    <TableHead>Nota Obbl.</TableHead>
                    <TableHead className="text-right">Azioni</TableHead>
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
                        {item.scorta === null || item.scorta === undefined ? 'Illimitata' : item.scorta === 0 ? <span className="text-red-500">Esaurito</span> : item.scorta}
                      </TableCell>
                      <TableCell>{item.isNoteRequired ? 'Sì' : 'No'}</TableCell>
                      <TableCell className="text-right space-x-2">
                        {/* Edit Button Trigger */}
                        <Button variant="outline" size="sm" onClick={() => handleOpenEditDialog(item)}>Modifica</Button>
                        {/* Reset Stock Button Trigger */}
                        <AlertDialog open={isResetStockDialogOpen && itemToResetStock?.id === item.id} onOpenChange={(open) => { if (!open) setItemToResetStock(null); setIsResetStockDialogOpen(open); }}>
                          <AlertDialogTrigger asChild>
                            <Button variant="secondary" size="sm" onClick={() => { setItemToResetStock(item); setResetStockError(null); setIsResetStockDialogOpen(true); }}>Reimposta Scorta</Button>
                          </AlertDialogTrigger>
                          <AlertDialogContent>
                            <AlertDialogHeader>
                              <AlertDialogTitle>Sei sicuro?</AlertDialogTitle>
                              <AlertDialogDescription>
                                Reimpostare la scorta per il prodotto "{itemToResetStock?.name}" a illimitata?
                              </AlertDialogDescription>
                            </AlertDialogHeader>
                            {resetStockError && <p className="text-red-500 text-sm">{resetStockError}</p>}
                            <AlertDialogFooter>
                              <AlertDialogCancel onClick={() => setItemToResetStock(null)}>Annulla</AlertDialogCancel>
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
                                  const errorMessage = errorResponse?.title || errorResponse?.message || 'Impossibile reimpostare la scorta.';
                                  setResetStockError(errorMessage);
                                }
                              }}>Conferma Reimposta</AlertDialogAction>
                            </AlertDialogFooter>
                          </AlertDialogContent>
                        </AlertDialog>
                        {/* Delete Button Trigger */}
                        <AlertDialog open={isDeleteDialogOpen && itemToDelete?.id === item.id} onOpenChange={(open) => { if (!open) setItemToDelete(null); setIsDeleteDialogOpen(open); }}>
                          <AlertDialogTrigger asChild>
                            <Button variant="destructive" size="sm" onClick={() => handleOpenDeleteDialog(item)}>Elimina</Button>
                          </AlertDialogTrigger>
                          <AlertDialogContent>
                            <AlertDialogHeader>
                              <AlertDialogTitle>Sei sicuro?</AlertDialogTitle>
                              <AlertDialogDescription>
                                Eliminare il prodotto "{itemToDelete?.name}"? Questa azione non può essere annullata.
                              </AlertDialogDescription>
                            </AlertDialogHeader>
                            {deleteError && <p className="text-red-500 text-sm">{deleteError}</p>}
                            <AlertDialogFooter>
                              <AlertDialogCancel onClick={() => setItemToDelete(null)}>Annulla</AlertDialogCancel>
                              <AlertDialogAction onClick={handleDeleteItem}>Continua</AlertDialogAction>
                            </AlertDialogFooter>
                          </AlertDialogContent>
                        </AlertDialog>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : <p>Nessun prodotto menu trovato per quest'area.</p>}
          </CardContent>
        </Card>
      )}

      {/* Edit Item Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-md overflow-y-scroll max-h-screen">
          <DialogHeader><DialogTitle>Modifica Prodotto Menu</DialogTitle></DialogHeader>
          {/* Edit Item Form */}
          <div className="grid gap-4 py-4">
            {/* Category */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-category" className="text-right">Categoria*</Label>
              <Select
                value={editItemData.menuCategoryId?.toString() || editingItem?.menuCategoryId.toString()}
                onValueChange={(value) => setEditItemData({ ...editItemData, menuCategoryId: parseInt(value, 10) })}
              >
                <SelectTrigger className="col-span-3">
                  <SelectValue placeholder="Seleziona una categoria" />
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
              <Label htmlFor="edit-name" className="text-right">Nome*</Label>
              <Input id="edit-name" value={editItemData.name || ''} onChange={(e) => setEditItemData({ ...editItemData, name: e.target.value })} className="col-span-3" />
            </div>
            {/* Description */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-desc" className="text-right">Descrizione</Label>
              <Textarea id="edit-desc" value={editItemData.description || ''} onChange={(e) => setEditItemData({ ...editItemData, description: e.target.value })} className="col-span-3" />
            </div>
            {/* Price */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-price" className="text-right">Prezzo* (€)</Label>
              <Input id="edit-price" type="number" step="0.01" value={editItemData.price || ''} onChange={(e) => setEditItemData({ ...editItemData, price: parseFloat(e.target.value) || 0 })} className="col-span-3" />
            </div>
            {/* Note Required */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-note-req" className="text-right">Nota Obbligatoria?</Label>
              <Checkbox id="edit-note-req" checked={editItemData.isNoteRequired} onCheckedChange={(checked) => setEditItemData({ ...editItemData, isNoteRequired: Boolean(checked) })} className="col-span-3 justify-self-start" />
            </div>
            {/* Note Suggestion (conditional) */}
            {editItemData.isNoteRequired && (
              <div className="grid grid-cols-4 items-center gap-4">
                <Label htmlFor="edit-note-sug" className="text-right">Suggerimento Nota</Label>
                <Input id="edit-note-sug" value={editItemData.noteSuggestion || ''} onChange={(e) => setEditItemData({ ...editItemData, noteSuggestion: e.target.value })} className="col-span-3" />
              </div>
            )}
            {/* Scorta */}
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-scorta" className="text-right">Scorta</Label>
              <Input
                id="edit-scorta"
                type="number"
                step="1"
                        placeholder="Illimitata"
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
            <DialogClose asChild><Button type="button" variant="outline">Annulla</Button></DialogClose>
            <Button type="submit" onClick={handleEditItem} disabled={!editItemData.name || editItemData.price == null || !editItemData.menuCategoryId}>Salva Modifiche</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

    </div>
  );
}
