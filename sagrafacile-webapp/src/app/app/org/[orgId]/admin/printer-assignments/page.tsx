'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { PrinterDto, MenuCategoryDto } from '@/types';
import printerService from '@/services/printerService';
import menuService from '@/services/menuService';
import printerAssignmentService from '@/services/printerAssignmentService';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import AdminAreaSelector from '@/components/shared/AdminAreaSelector';
import { Checkbox } from "@/components/ui/checkbox"
import { ScrollArea } from "@/components/ui/scroll-area"
import { toast } from 'sonner';
import { Separator } from '@/components/ui/separator';

export default function PrinterAssignmentsPage() {
  const { user } = useAuth();
  const [selectedAreaId, setSelectedAreaId] = useState<string | undefined>(undefined);
  const [printers, setPrinters] = useState<PrinterDto[]>([]);
  const [categories, setCategories] = useState<MenuCategoryDto[]>([]);
  const [selectedPrinterId, setSelectedPrinterId] = useState<string>(''); // Memorizza come stringa per il componente Select
  const [assignedCategoryIds, setAssignedCategoryIds] = useState<Set<number>>(new Set());

  const [isLoadingPrinters, setIsLoadingPrinters] = useState(false);
  const [isLoadingCategories, setIsLoadingCategories] = useState(false);
  const [isLoadingAssignments, setIsLoadingAssignments] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const [fetchError, setFetchError] = useState<string | null>(null);


  const fetchPrintersAndCategories = useCallback(async () => {
    if (!selectedAreaId) {
      setCategories([]);
      setSelectedPrinterId(''); // Resetta la selezione della stampante
      setAssignedCategoryIds(new Set()); // Pulisce le assegnazioni
      return;
    }
    setIsLoadingPrinters(true);
    setIsLoadingCategories(true);
    setFetchError(null);
    try {
      const [printersResponse, categoriesResponse] = await Promise.all([
        printerService.getPrinters(),
        menuService.getCategories(+(selectedAreaId || 0)),
      ]);
      setPrinters(printersResponse.filter(p => p.isEnabled)); // Mostra solo le stampanti abilitate
      setCategories(categoriesResponse);
    } catch (err) {
      console.error('Errore nel recupero di stampanti o categorie:', err);
      const errorMsg = 'Caricamento dati iniziali fallito.';
      setFetchError(errorMsg);
      toast.error("Errore", { description: errorMsg });
    } finally {
      setIsLoadingPrinters(false);
      setIsLoadingCategories(false);
    }
  }, [selectedAreaId]);

  const fetchAssignments = useCallback(async (printerId: number) => {
    setIsLoadingAssignments(true);
    try {
      const assignedCategories = await printerAssignmentService.getAssignmentsForPrinter(printerId, +(selectedAreaId || 0));
      setAssignedCategoryIds(new Set(assignedCategories.map(cat => cat.menuCategoryId)));
    } catch (err) {
      console.error(`Errore nel recupero delle assegnazioni per la stampante ${printerId}:`, err);
      toast.error("Errore", { description: `Caricamento assegnazioni per la stampante selezionata fallito.` });
      setAssignedCategoryIds(new Set()); // Resetta in caso di errore
    } finally {
      setIsLoadingAssignments(false);
    }
  }, [selectedAreaId]);

  useEffect(() => {
    if (user) {
      fetchPrintersAndCategories();
    }
  }, [user, fetchPrintersAndCategories]);

  useEffect(() => {
    if (selectedPrinterId) {
      const numericId = parseInt(selectedPrinterId, 10);
      if (!isNaN(numericId)) {
        fetchAssignments(numericId);
      }
    } else {
      setAssignedCategoryIds(new Set()); // Pulisce le assegnazioni se non è selezionata alcuna stampante
    }
  }, [selectedPrinterId, fetchAssignments]);

  // --- Gestori Eventi ---
  const handleAreaChange = (areaId: string | undefined) => {
    setSelectedAreaId(areaId);
    // Resetta le selezioni a valle
    setSelectedPrinterId('');
    setAssignedCategoryIds(new Set());
  };

  const handlePrinterChange = (value: string) => {
    setSelectedPrinterId(value);
  };

  const handleCheckboxChange = (categoryId: number, checked: boolean) => {
    setAssignedCategoryIds(prev => {
      const newSet = new Set(prev);
      if (checked) {
        newSet.add(categoryId);
      } else {
        newSet.delete(categoryId);
      }
      return newSet;
    });
  };

  const handleSaveChanges = async () => {
    if (!selectedPrinterId) {
      toast.warning("Selezionare prima una stampante.");
      return;
    }
    const numericPrinterId = parseInt(selectedPrinterId, 10);
    if (isNaN(numericPrinterId)) return; // Non dovrebbe succedere

    setIsSaving(true);
    try {
      const categoryIdsToSave = Array.from(assignedCategoryIds);
      await printerAssignmentService.setAssignmentsForPrinter(numericPrinterId, +(selectedAreaId || 0), categoryIdsToSave);
      toast.success("Assegnazioni aggiornate con successo.");
      // Opzionalmente, si possono ri-recuperare le assegnazioni per conferma, anche se lo stato dovrebbe essere corretto
      // fetchAssignments(numericPrinterId);
    } catch (err: unknown) {
      console.error('Errore nel salvataggio delle assegnazioni:', err);
      const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
      const errorMsg = errorResponse?.title || errorResponse?.message || 'Salvataggio assegnazioni fallito.';
      toast.error("Errore", { description: errorMsg });
    } finally {
      setIsSaving(false);
    }
  };

  // --- Render ---
  const selectedPrinterName = printers.find(p => p.id === parseInt(selectedPrinterId, 10))?.name || 'Stampante Selezionata';

  return (
    <div className="space-y-6">
      <AdminAreaSelector
        selectedAreaId={selectedAreaId}
        onAreaChange={handleAreaChange}
        title="Passo 1: Seleziona Area"
      />

      {/* Selettore Categorie (condizionale) */}
      {selectedAreaId && (
        <Card>
          <CardHeader>
            <CardTitle>Assegnazione Categorie Stampante</CardTitle>
            <CardDescription>
              Seleziona una stampante e spunta le categorie di menu che dovrebbero stampare le comande su di essa.
              Vengono mostrate solo le stampanti abilitate.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            {fetchError && <p className="text-red-500 text-center py-4">{fetchError}</p>}

            {/* Selezione Stampante */}
            <div className="max-w-xs">
              <Label htmlFor="printer-select">Seleziona Stampante</Label>
              <Select
                value={selectedPrinterId}
                onValueChange={handlePrinterChange}
                disabled={isLoadingPrinters}
              >
                <SelectTrigger id="printer-select">
                  <SelectValue placeholder={isLoadingPrinters ? "Caricamento stampanti..." : "Seleziona una stampante..."} />
                </SelectTrigger>
                <SelectContent>
                  {printers.map((printer) => (
                    <SelectItem key={printer.id} value={printer.id.toString()}>
                      {printer.name} (Tipo: {printer.type})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {selectedPrinterId && (
              <>
                <Separator />
                <div>
                  <h3 className="text-lg font-medium mb-4">Assegna Categorie a: <span className="font-semibold">{selectedPrinterName}</span></h3>
                  {isLoadingCategories || isLoadingAssignments ? (
                    <p>Caricamento categorie/assegnazioni...</p>
                  ) : categories.length > 0 ? (
                    <ScrollArea className="h-72 w-full rounded-md border p-4">
                      <div className="space-y-2">
                        {categories.map((category) => (
                          <div key={category.id} className="flex items-center space-x-2">
                            <Checkbox
                              id={`category-${category.id}`}
                              checked={assignedCategoryIds.has(category.id)}
                              onCheckedChange={(checked) => handleCheckboxChange(category.id, !!checked)}
                            />
                            <Label htmlFor={`category-${category.id}`}>{category.name} (ID: {category.id})</Label>
                          </div>
                        ))}
                      </div>
                    </ScrollArea>
                  ) : (
                    <p className="text-center text-muted-foreground py-4">Nessuna categoria trovata per questa area.</p>
                  )}
                </div>
                <Separator />
                <div className="flex justify-end">
                  <Button onClick={handleSaveChanges} disabled={isSaving}>
                    {isSaving ? 'Salvataggio...' : 'Salva Modifiche'}
                  </Button>
                </div>
              </>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
