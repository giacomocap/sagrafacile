'use client';

import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { MenuIcon, ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';
import apiClient from '@/services/apiClient';
import { AreaDto, MenuCategoryDto, MenuItemDto } from '@/types';
import { toast } from 'sonner';

interface StepMenuProps {
  createdArea?: AreaDto;
  onNext: (menuData: { createdMenuCategory: MenuCategoryDto; createdMenuItem: MenuItemDto }) => void;
  onBack: () => void;
  isLoading: boolean;
  setIsLoading: (loading: boolean) => void;
}

export default function StepMenu({ 
  createdArea, 
  onNext, 
  onBack, 
  isLoading, 
  setIsLoading 
}: StepMenuProps) {
  const [categoryName, setCategoryName] = useState('');
  const [itemName, setItemName] = useState('');
  const [itemPrice, setItemPrice] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async () => {
    if (!categoryName.trim()) {
      setError("Il nome della categoria è obbligatorio.");
      return;
    }

    if (!itemName.trim()) {
      setError("Il nome dell'articolo è obbligatorio.");
      return;
    }

    if (!itemPrice.trim() || isNaN(parseFloat(itemPrice)) || parseFloat(itemPrice) <= 0) {
      setError("Inserisci un prezzo valido.");
      return;
    }

    if (!createdArea) {
      setError("Area non trovata. Riprova dal passo precedente.");
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      // First, create the menu category
      const categoryData = {
        name: categoryName.trim(),
        areaId: createdArea.id,
      };

      const categoryResponse = await apiClient.post<MenuCategoryDto>('/MenuCategories', categoryData);
      const createdCategory = categoryResponse.data;

      // Then, create the menu item
      const itemData = {
        name: itemName.trim(),
        description: null,
        price: parseFloat(itemPrice),
        menuCategoryId: createdCategory.id,
        isNoteRequired: false,
        noteSuggestion: null,
        scorta: null, // Unlimited stock by default
      };

      const itemResponse = await apiClient.post<MenuItemDto>('/MenuItems', itemData);
      const createdItem = itemResponse.data;

      toast.success(`Categoria "${createdCategory.name}" e articolo "${createdItem.name}" creati con successo!`);
      
      onNext({
        createdMenuCategory: createdCategory,
        createdMenuItem: createdItem,
      });
    } catch (err: any) {
      console.error('Error creating menu:', err);
      const errorMessage = err.response?.data?.title || err.response?.data?.message || 'Creazione menu fallita.';
      setError(errorMessage);
      toast.error(errorMessage);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="text-center space-y-4">
        <div className="flex justify-center">
          <div className="w-12 h-12 bg-orange-100 rounded-full flex items-center justify-center">
            <MenuIcon className="w-6 h-6 text-orange-600" />
          </div>
        </div>
        <div>
          <h3 className="text-lg font-semibold">Crea il tuo primo Menu</h3>
          <p className="text-gray-600 max-w-md mx-auto">
            Creiamo la prima categoria del menu e un articolo di esempio. 
            Questo ti darà una base da cui partire per il tuo evento.
          </p>
        </div>
      </div>

      <div className="max-w-md mx-auto space-y-4">
        <div className="space-y-2">
          <Label htmlFor="categoryName">Nome Categoria *</Label>
          <Input
            id="categoryName"
            placeholder="es. Primi Piatti, Bevande, Dolci"
            value={categoryName}
            onChange={(e) => setCategoryName(e.target.value)}
            disabled={isLoading}
          />
        </div>

        <div className="border-t pt-4 space-y-4">
          <h4 className="text-sm font-medium text-gray-700">Primo articolo della categoria:</h4>
          
          <div className="space-y-2">
            <Label htmlFor="itemName">Nome Articolo *</Label>
            <Input
              id="itemName"
              placeholder="es. Spaghetti al Ragù, Coca Cola, Tiramisù"
              value={itemName}
              onChange={(e) => setItemName(e.target.value)}
              disabled={isLoading}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="itemPrice">Prezzo (€) *</Label>
            <Input
              id="itemPrice"
              type="number"
              step="0.01"
              min="0"
              placeholder="es. 8.50"
              value={itemPrice}
              onChange={(e) => setItemPrice(e.target.value)}
              disabled={isLoading}
            />
          </div>
        </div>

        <div className="bg-gray-50 rounded-lg p-4">
          <div className="flex justify-between text-sm">
            <span className="text-gray-600">Area collegata:</span>
            <span className="font-medium">{createdArea?.name || 'Non trovata'}</span>
          </div>
        </div>

        <p className="text-xs text-gray-500 text-center">
          Potrai aggiungere altre categorie e articoli dalla sezione "Gestione Menu"
        </p>

        {error && (
          <div className="p-3 bg-red-50 border border-red-200 rounded-md">
            <p className="text-sm text-red-600">{error}</p>
          </div>
        )}
      </div>

      <div className="flex justify-between pt-6">
        <Button variant="outline" onClick={onBack} disabled={isLoading}>
          <ChevronLeft className="w-4 h-4 mr-2" />
          Indietro
        </Button>
        <Button 
          onClick={handleSubmit} 
          disabled={!categoryName.trim() || !itemName.trim() || !itemPrice.trim() || isLoading || !createdArea}
        >
          {isLoading ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Creazione...
            </>
          ) : (
            <>
              Continua
              <ChevronRight className="w-4 h-4 ml-2" />
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
