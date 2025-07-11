'use client';

import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { CreditCard, ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';
import apiClient from '@/services/apiClient';
import { AreaDto, PrinterDto, CashierStationDto } from '@/types';
import { toast } from 'sonner';

interface StepCashierStationProps {
  createdArea?: AreaDto;
  createdPrinter?: PrinterDto;
  onNext: (stationData: CashierStationDto) => void;
  onBack: () => void;
  isLoading: boolean;
  setIsLoading: (loading: boolean) => void;
}

export default function StepCashierStation({ 
  createdArea, 
  createdPrinter, 
  onNext, 
  onBack, 
  isLoading, 
  setIsLoading 
}: StepCashierStationProps) {
  const { user } = useAuth();
  const [stationName, setStationName] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async () => {
    if (!stationName.trim()) {
      setError("Il nome della postazione è obbligatorio.");
      return;
    }

    if (!createdArea) {
      setError("Area non trovata. Riprova dal passo precedente.");
      return;
    }

    if (!createdPrinter) {
      setError("Stampante non trovata. Riprova dal passo precedente.");
      return;
    }

    if (!user?.organizationId) {
      setError("Informazioni organizzazione mancanti.");
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const stationData = {
        name: stationName.trim(),
        areaId: createdArea.id,
        receiptPrinterId: createdPrinter.id,
        printComandasAtThisStation: false, // Default to false for simplicity
        isEnabled: true,
      };

      const response = await apiClient.post<CashierStationDto>(
        `/CashierStations/organization/${user.organizationId}`, 
        stationData
      );
      
      toast.success(`Postazione "${response.data.name}" creata con successo!`);
      onNext(response.data);
    } catch (err: any) {
      console.error('Error creating cashier station:', err);
      const errorMessage = err.response?.data?.title || err.response?.data?.message || 'Creazione postazione fallita.';
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
          <div className="w-12 h-12 bg-purple-100 rounded-full flex items-center justify-center">
            <CreditCard className="w-6 h-6 text-purple-600" />
          </div>
        </div>
        <div>
          <h3 className="text-lg font-semibold">Imposta una Postazione Cassa</h3>
          <p className="text-gray-600 max-w-md mx-auto">
            Una Postazione Cassa è un punto vendita che collega l'area operativa alla stampante. 
            Colleghiamo quelli che hai appena creato.
          </p>
        </div>
      </div>

      <div className="max-w-md mx-auto space-y-4">
        <div className="space-y-2">
          <Label htmlFor="stationName">Nome Postazione *</Label>
          <Input
            id="stationName"
            placeholder="es. Cassa 1, Cassa Principale"
            value={stationName}
            onChange={(e) => setStationName(e.target.value)}
            disabled={isLoading}
            className="text-center"
          />
        </div>

        {/* Display the linked area and printer */}
        <div className="bg-gray-50 rounded-lg p-4 space-y-3">
          <h4 className="text-sm font-medium text-gray-700">Configurazione collegata:</h4>
          <div className="space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-600">Area:</span>
              <span className="font-medium">{createdArea?.name || 'Non trovata'}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-600">Stampante:</span>
              <span className="font-medium">{createdPrinter?.name || 'Non trovata'}</span>
            </div>
          </div>
        </div>

        <p className="text-xs text-gray-500 text-center">
          Potrai configurare altre postazioni e impostazioni avanzate dalla sezione amministrativa
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
          disabled={!stationName.trim() || isLoading || !createdArea || !createdPrinter}
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
