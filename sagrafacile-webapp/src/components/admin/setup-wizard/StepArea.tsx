'use client';

import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { MapPin, ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';
import apiClient from '@/services/apiClient';
import { AreaDto, AreaUpsertDto } from '@/types';
import { toast } from 'sonner';

interface StepAreaProps {
  onNext: (areaData: AreaDto) => void;
  onBack: () => void;
  isLoading: boolean;
  setIsLoading: (loading: boolean) => void;
}

export default function StepArea({ onNext, onBack, isLoading, setIsLoading }: StepAreaProps) {
  const { user } = useAuth();
  const [areaName, setAreaName] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async () => {
    if (!areaName.trim()) {
      setError("Il nome dell'area è obbligatorio.");
      return;
    }

    if (!user?.organizationId) {
      setError("Informazioni organizzazione mancanti.");
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const areaData: AreaUpsertDto = {
        name: areaName.trim(),
        organizationId: user.organizationId,
        enableWaiterConfirmation: false,
        enableKds: false,
        enableCompletionConfirmation: false,
        receiptPrinterId: null,
        printComandasAtCashier: false,
        enableQueueSystem: false,
        guestCharge: 0,
        takeawayCharge: 0,
      };

      const response = await apiClient.post<AreaDto>('/Areas', areaData);
      toast.success(`Area "${response.data.name}" creata con successo!`);
      onNext(response.data);
    } catch (err: any) {
      console.error('Error creating area:', err);
      const errorMessage = err.response?.data?.title || err.response?.data?.message || 'Creazione area fallita.';
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
          <div className="w-12 h-12 bg-blue-100 rounded-full flex items-center justify-center">
            <MapPin className="w-6 h-6 text-blue-600" />
          </div>
        </div>
        <div>
          <h3 className="text-lg font-semibold">Crea la tua prima Area</h3>
          <p className="text-gray-600 max-w-md mx-auto">
            Un'Area è una zona operativa del tuo evento, come la cucina principale, il bar, o uno stand specifico. 
            Ogni area ha il proprio menu e può avere diverse configurazioni operative.
          </p>
        </div>
      </div>

      {/* Workflow explanation */}
      <div className="bg-blue-50 rounded-lg p-4 max-w-2xl mx-auto">
        <h4 className="text-sm font-medium text-blue-900 mb-3">💡 Come funziona il flusso degli ordini?</h4>
        <div className="text-sm text-blue-800 space-y-2">
          <p><strong>Flusso Base:</strong> Cliente ordina → Pagamento → Preparazione → Consegna</p>
          <p><strong>Con Cameriere:</strong> Cliente ordina → Pagamento → Cameriere conferma → Preparazione → Consegna</p>
          <p><strong>Con Cucina Digitale (KDS):</strong> Gli chef confermano ogni piatto su schermo prima della consegna</p>
          <p><strong>Con Sistema Code:</strong> I clienti ricevono un numero e vengono chiamati quando è il loro turno</p>
        </div>
      </div>

      <div className="max-w-md mx-auto space-y-4">
        <div className="space-y-2">
          <Label htmlFor="areaName">Nome Area *</Label>
          <Input
            id="areaName"
            placeholder="es. Cucina Principale, Bar, Stand Pizza"
            value={areaName}
            onChange={(e) => setAreaName(e.target.value)}
            disabled={isLoading}
            className="text-center"
          />
          <p className="text-xs text-gray-500 text-center">
            Per ora creiamo un'area semplice. Potrai configurare il flusso operativo e aggiungere altre aree dalla sezione amministrativa.
          </p>
        </div>

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
          disabled={!areaName.trim() || isLoading}
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
