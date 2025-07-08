'use client';

import React from 'react';
import { Button } from '@/components/ui/button';
import { CheckCircle, ArrowRight, Settings, Users, CreditCard } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { AreaDto, PrinterDto, CashierStationDto, MenuCategoryDto, MenuItemDto } from '@/types';

interface WizardState {
  createdArea?: AreaDto;
  createdPrinter?: PrinterDto;
  createdCashierStation?: CashierStationDto;
  createdMenuCategory?: MenuCategoryDto;
  createdMenuItem?: MenuItemDto;
}

interface StepCompleteProps {
  wizardState: WizardState;
  onFinish: () => void;
}

export default function StepComplete({ wizardState, onFinish }: StepCompleteProps) {
  const router = useRouter();

  const handleGoToCashier = () => {
    if (wizardState.createdArea) {
      const orgId = window.location.pathname.split('/')[3]; // Extract orgId from URL
      router.push(`/app/org/${orgId}/cashier/area/${wizardState.createdArea.id}`);
    }
    onFinish();
  };

  const handleGoToMenu = () => {
    const orgId = window.location.pathname.split('/')[3]; // Extract orgId from URL
    router.push(`/app/org/${orgId}/admin/menu/categories`);
    onFinish();
  };

  const handleGoToDashboard = () => {
    onFinish();
  };

  return (
    <div className="text-center space-y-6">
      <div className="flex justify-center">
        <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center">
          <CheckCircle className="w-10 h-10 text-green-600" />
        </div>
      </div>

      <div className="space-y-4">
        <h2 className="text-2xl font-semibold text-gray-900">
          Configurazione Completata! 🎉
        </h2>
        <p className="text-lg text-gray-600 max-w-md mx-auto">
          Perfetto! Hai configurato con successo le basi per il tuo evento. 
          Ecco cosa hai creato:
        </p>
      </div>

      {/* Summary of created items */}
      <div className="bg-gray-50 rounded-lg p-6 max-w-lg mx-auto">
        <h3 className="text-lg font-medium text-gray-900 mb-4">Riepilogo configurazione:</h3>
        <div className="space-y-3 text-left">
          {wizardState.createdArea && (
            <div className="flex items-center space-x-3">
              <div className="w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center">
                <CheckCircle className="w-4 h-4 text-blue-600" />
              </div>
              <div>
                <span className="font-medium">Area:</span> {wizardState.createdArea.name}
              </div>
            </div>
          )}
          
          {wizardState.createdPrinter && (
            <div className="flex items-center space-x-3">
              <div className="w-8 h-8 bg-green-100 rounded-full flex items-center justify-center">
                <CheckCircle className="w-4 h-4 text-green-600" />
              </div>
              <div>
                <span className="font-medium">Stampante:</span> {wizardState.createdPrinter.name}
              </div>
            </div>
          )}
          
          {wizardState.createdCashierStation && (
            <div className="flex items-center space-x-3">
              <div className="w-8 h-8 bg-purple-100 rounded-full flex items-center justify-center">
                <CheckCircle className="w-4 h-4 text-purple-600" />
              </div>
              <div>
                <span className="font-medium">Postazione:</span> {wizardState.createdCashierStation.name}
              </div>
            </div>
          )}
          
          {wizardState.createdMenuCategory && wizardState.createdMenuItem && (
            <div className="flex items-center space-x-3">
              <div className="w-8 h-8 bg-orange-100 rounded-full flex items-center justify-center">
                <CheckCircle className="w-4 h-4 text-orange-600" />
              </div>
              <div>
                <span className="font-medium">Menu:</span> {wizardState.createdMenuCategory.name} → {wizardState.createdMenuItem.name}
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Next steps */}
      <div className="space-y-4">
        <h3 className="text-lg font-medium text-gray-900">Cosa puoi fare ora:</h3>
        
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 max-w-2xl mx-auto">
          <Button 
            variant="outline" 
            className="h-auto p-4 flex flex-col items-center space-y-2"
            onClick={handleGoToCashier}
          >
            <CreditCard className="w-6 h-6 text-blue-600" />
            <span className="font-medium">Vai alla Cassa</span>
            <span className="text-xs text-gray-500 text-center">Inizia a prendere ordini</span>
          </Button>
          
          <Button 
            variant="outline" 
            className="h-auto p-4 flex flex-col items-center space-y-2"
            onClick={handleGoToMenu}
          >
            <Users className="w-6 h-6 text-orange-600" />
            <span className="font-medium">Gestisci Menu</span>
            <span className="text-xs text-gray-500 text-center">Aggiungi più piatti</span>
          </Button>
          
          <Button 
            variant="outline" 
            className="h-auto p-4 flex flex-col items-center space-y-2"
            onClick={handleGoToDashboard}
          >
            <Settings className="w-6 h-6 text-gray-600" />
            <span className="font-medium">Dashboard Admin</span>
            <span className="text-xs text-gray-500 text-center">Configura altro</span>
          </Button>
        </div>
      </div>

      <div className="pt-6">
        <Button onClick={handleGoToDashboard} size="lg" className="px-8">
          Vai alla Dashboard
          <ArrowRight className="w-4 h-4 ml-2" />
        </Button>
      </div>

      <div className="text-sm text-gray-500">
        <p>
          Hai bisogno di aiuto? Consulta la documentazione o contatta il supporto.
        </p>
      </div>
    </div>
  );
}
