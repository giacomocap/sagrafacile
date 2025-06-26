'use client';

import React, { useState, useEffect } from 'react';
import apiClient from '@/services/apiClient';
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from '@/components/ui/card';
import AdminAreaSelector from '@/components/shared/AdminAreaSelector';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogFooter,
  DialogClose,
  DialogDescription,
} from '@/components/ui/dialog';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"; // Added AlertDialog imports
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

import { MenuCategoryDto as MenuCategory } from '@/types';

export default function MenuCategoriesPage() {
  const [selectedAreaId, setSelectedAreaId] = useState<string | undefined>(undefined);
  const [categories, setCategories] = useState<MenuCategory[]>([]);
  const [isLoadingCategories, setIsLoadingCategories] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isAddDialogOpen, setIsAddDialogOpen] = useState(false); // State for Add dialog
  const [newCategoryName, setNewCategoryName] = useState(''); // State for new category name input
  const [addError, setAddError] = useState<string | null>(null); // State for errors within the add dialog
  const [editingCategory, setEditingCategory] = useState<MenuCategory | null>(null); // State for category being edited
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false); // State for Edit dialog
  const [editCategoryName, setEditCategoryName] = useState(''); // State for edited category name input
  const [editError, setEditError] = useState<string | null>(null); // State for errors within the edit dialog
  const [categoryToDelete, setCategoryToDelete] = useState<MenuCategory | null>(null); // State for category marked for deletion
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false); // State for Delete dialog
  const [deleteError, setDeleteError] = useState<string | null>(null); // State for errors within the delete dialog


  // Function to refresh categories (used after add/edit/delete)
  const fetchCategories = async (areaId: string) => {
    setIsLoadingCategories(true);
    setError(null); // Clear main error
    setAddError(null); // Clear dialog error
    try {
      const response = await apiClient.get<MenuCategory[]>(`/MenuCategories`, {
        params: { areaId: areaId },
      });
      setCategories(response.data);
    } catch (err) {
      console.error('Error fetching categories:', err);
      setError(`Caricamento categorie per l'area selezionata fallito.`); // Set main error
    } finally {
      setIsLoadingCategories(false);
    }
  };


  // Fetch Categories when selectedAreaId changes
  useEffect(() => {
    if (selectedAreaId) {
      fetchCategories(selectedAreaId);
    } else {
      setCategories([]); // Clear if no area selected
    }
  }, [selectedAreaId]); // Removed fetchCategories from dependency array as it's defined outside now

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Gestione Categorie Menu</h1>

      <AdminAreaSelector
        selectedAreaId={selectedAreaId}
        onAreaChange={setSelectedAreaId}
        title="Seleziona Area"
        description="Scegli l'area di cui vuoi gestire le categorie."
      />

      {selectedAreaId && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between"> {/* Adjusted layout */}
            <div>
              <CardTitle>Categorie per l'Area Selezionata</CardTitle>
              <CardDescription>Visualizza e gestisci le categorie all'interno dell'area scelta.</CardDescription> {/* Added description */}
            </div>
            {/* Add Category Dialog Trigger */}
            <Dialog open={isAddDialogOpen} onOpenChange={setIsAddDialogOpen}>
              <DialogTrigger asChild>
                <Button size="sm" onClick={() => { setNewCategoryName(''); setAddError(null); }}> {/* Reset state on open */}
                  Aggiungi Nuova Categoria
                </Button>
              </DialogTrigger>
              <DialogContent className="sm:max-w-[425px] overflow-y-scroll max-h-screen">
                <DialogHeader>
                  <DialogTitle>Aggiungi Nuova Categoria Menu</DialogTitle>
                  <DialogDescription>
                    Inserisci il nome per la nuova categoria nell'area selezionata.
                  </DialogDescription>
                </DialogHeader>
                <div className="grid gap-4 py-4">
                  <div className="grid grid-cols-4 items-center gap-4">
                    <Label htmlFor="name" className="text-right">
                      Nome
                    </Label>
                    <Input
                      id="name"
                      value={newCategoryName}
                      onChange={(e) => setNewCategoryName(e.target.value)}
                      className="col-span-3"
                      placeholder="Es. Primi, Bevande"
                    />
                  </div>
                  {addError && <p className="col-span-4 text-red-500 text-sm text-center">{addError}</p>}
                </div>
                <DialogFooter>
                  <DialogClose asChild>
                    <Button type="button" variant="outline">Annulla</Button>
                  </DialogClose>
                  <Button type="submit" onClick={handleAddCategory} disabled={!newCategoryName.trim()}>Salva Categoria</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </CardHeader>
          <CardContent>
            {isLoadingCategories ? (
              <p>Caricamento categorie...</p>
            ) : error ? (
              <p className="text-red-500">{error}</p>
            ) : categories.length > 0 ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>ID</TableHead>
                    <TableHead>Nome</TableHead>
                    <TableHead className="text-right">Azioni</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {categories.map((category) => (
                    <TableRow key={category.id}>
                      <TableCell>{category.id}</TableCell>
                      <TableCell className="font-medium">{category.name}</TableCell>
                      <TableCell className="text-right space-x-2">
                        {/* Edit Button Trigger */}
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => handleOpenEditDialog(category)}
                        >
                          Modifica
                        </Button>
                        {/* Delete Button Trigger */}
                        <AlertDialog open={isDeleteDialogOpen && categoryToDelete?.id === category.id} onOpenChange={(open) => { if (!open) { setCategoryToDelete(null); setDeleteError(null); } setIsDeleteDialogOpen(open); }}>
                          <AlertDialogTrigger asChild>
                            <Button
                              variant="destructive"
                              size="sm"
                              onClick={() => { setCategoryToDelete(category); setDeleteError(null); }} // Set category to delete on click
                            >
                              Elimina
                            </Button>
                          </AlertDialogTrigger>
                          <AlertDialogContent>
                            <AlertDialogHeader>
                              <AlertDialogTitle>Sei assolutamente sicuro?</AlertDialogTitle>
                              <AlertDialogDescription>
                                Questa azione non può essere annullata. Questo eliminerà permanentemente la
                                categoria "{categoryToDelete?.name}" e potenzialmente i suoi articoli di menu associati.
                              </AlertDialogDescription>
                            </AlertDialogHeader>
                            {deleteError && <p className="text-red-500 text-sm">{deleteError}</p>}
                            <AlertDialogFooter>
                              <AlertDialogCancel onClick={() => setCategoryToDelete(null)}>Annulla</AlertDialogCancel>
                              <AlertDialogAction onClick={handleDeleteCategory}>Continua</AlertDialogAction>
                            </AlertDialogFooter>
                          </AlertDialogContent>
                        </AlertDialog>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : (
              <p>Nessuna categoria trovata per questa area. Aggiungine una per iniziare.</p>
            )}
          </CardContent>
        </Card>
      )}

      {/* Edit Category Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[425px] overflow-y-scroll max-h-screen">
          <DialogHeader>
            <DialogTitle>Modifica Categoria Menu</DialogTitle>
            <DialogDescription>
              Modifica il nome della categoria.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="edit-name" className="text-right">
                Nome
              </Label>
              <Input
                id="edit-name"
                value={editCategoryName}
                onChange={(e) => setEditCategoryName(e.target.value)}
                className="col-span-3"
              />
            </div>
            {editError && <p className="col-span-4 text-red-500 text-sm text-center">{editError}</p>}
          </div>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline">Annulla</Button>
            </DialogClose>
            <Button type="submit" onClick={handleEditCategory} disabled={!editCategoryName.trim() || editCategoryName === editingCategory?.name}>Salva Modifiche</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );

  async function handleAddCategory() {
    if (!newCategoryName.trim() || !selectedAreaId) {
      setAddError("Il nome della categoria è obbligatorio.");
      return;
    }
    setAddError(null); // Clear previous errors

    try {
      const newCategoryData = {
        name: newCategoryName.trim(),
        areaId: parseInt(selectedAreaId, 10), // Ensure areaId is a number
      };
      await apiClient.post<MenuCategory>('/MenuCategories', newCategoryData);

      // Option 1: Add to state directly (optimistic update might be better later)
      // setCategories([...categories, response.data]);

      // Option 2: Refetch categories for the current area
      await fetchCategories(selectedAreaId);

      setNewCategoryName(''); // Clear input
      setIsAddDialogOpen(false); // Close dialog
    } catch (err: unknown) {
      console.error('Error adding category:', err);
      // Provide more specific error feedback if possible
      const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
      const errorMessage = errorResponse?.title || errorResponse?.message || 'Aggiunta categoria fallita.';
      setAddError(errorMessage);
    }
  }

  function handleOpenEditDialog(category: MenuCategory) {
    setEditingCategory(category);
    setEditCategoryName(category.name);
    setEditError(null);
    setIsEditDialogOpen(true);
  }

  async function handleEditCategory() {
    if (!editCategoryName.trim() || !editingCategory) {
      setEditError("Il nome della categoria è obbligatorio.");
      return;
    }
    if (editCategoryName.trim() === editingCategory.name) {
      setEditError("Il nome è identico a quello attuale.");
      return;
    }
    setEditError(null);

    try {
      const updatedCategoryData = {
        id: editingCategory.id, // Include ID for PUT request
        name: editCategoryName.trim(),
        areaId: editingCategory.areaId, // AreaId is needed by DTO/Model
      };
      // Note: API endpoint expects PUT /api/MenuCategories/{id}
      await apiClient.put(`/MenuCategories/${editingCategory.id}`, updatedCategoryData);

      // Refetch categories for the current area to show update
      if (selectedAreaId) {
        await fetchCategories(selectedAreaId);
      }

      setIsEditDialogOpen(false); // Close dialog
      setEditingCategory(null); // Clear editing state
    } catch (err: unknown) {
      console.error('Error updating category:', err);
      const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
      const errorMessage = errorResponse?.title || errorResponse?.message || 'Aggiornamento categoria fallito.';
      setEditError(errorMessage);
    }
  }

  async function handleDeleteCategory() {
    if (!categoryToDelete) return;
    setDeleteError(null); // Clear previous errors

    try {
      // API endpoint expects DELETE /api/MenuCategories/{id}
      await apiClient.delete(`/MenuCategories/${categoryToDelete.id}`);

      // Refetch categories for the current area to show update
      if (selectedAreaId) {
        await fetchCategories(selectedAreaId);
      }

      setIsDeleteDialogOpen(false); // Close dialog
      setCategoryToDelete(null); // Clear delete state
    } catch (err: unknown) {
      console.error('Error deleting category:', err);
      const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
      const errorMessage = errorResponse?.title || errorResponse?.message || 'Eliminazione categoria fallita. Potrebbe avere articoli associati.';
      setDeleteError(errorMessage);
      // Keep the dialog open by not setting setIsDeleteDialogOpen(false) here
    }
  }
}
