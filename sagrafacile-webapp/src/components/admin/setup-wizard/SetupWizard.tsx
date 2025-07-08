'use client';

import React, { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Progress } from '@/components/ui/progress';
import StepWelcome from './StepWelcome';
import StepArea from './StepArea';
import StepPrinter from './StepPrinter';
import StepCashierStation from './StepCashierStation';
import StepMenu from './StepMenu';
import StepComplete from './StepComplete';
import { AreaDto, PrinterDto, CashierStationDto, MenuCategoryDto, MenuItemDto } from '@/types';

interface SetupWizardProps {
  onFinish: () => void;
}

interface WizardState {
  createdArea?: AreaDto;
  createdPrinter?: PrinterDto;
  createdCashierStation?: CashierStationDto;
  createdMenuCategory?: MenuCategoryDto;
  createdMenuItem?: MenuItemDto;
}

const TOTAL_STEPS = 6;

export default function SetupWizard({ onFinish }: SetupWizardProps) {
  const [isOpen, setIsOpen] = useState(true);
  const [currentStep, setCurrentStep] = useState(1);
  const [wizardState, setWizardState] = useState<WizardState>({});
  const [isLoading, setIsLoading] = useState(false);

  const handleNext = () => {
    if (currentStep < TOTAL_STEPS) {
      setCurrentStep(currentStep + 1);
    }
  };

  const handleBack = () => {
    if (currentStep > 1) {
      setCurrentStep(currentStep - 1);
    }
  };

  const handleSkip = () => {
    // Set localStorage flag to prevent auto-showing
    const orgId = window.location.pathname.split('/')[3]; // Extract orgId from URL
    localStorage.setItem(`setupWizardSkipped_org_${orgId}`, 'true');
    setIsOpen(false);
    onFinish();
  };

  const handleStepComplete = (stepData: Partial<WizardState>) => {
    setWizardState(prev => ({ ...prev, ...stepData }));
    handleNext();
  };

  const handleFinish = () => {
    onFinish();
    setIsOpen(false);
  };

  const getStepTitle = () => {
    switch (currentStep) {
      case 1: return 'Benvenuto in SagraFacile';
      case 2: return 'Crea la tua prima Area';
      case 3: return 'Configura una Stampante';
      case 4: return 'Imposta una Postazione Cassa';
      case 5: return 'Crea il tuo primo Menu';
      case 6: return 'Configurazione Completata!';
      default: return 'Setup Wizard';
    }
  };

  const renderCurrentStep = () => {
    switch (currentStep) {
      case 1:
        return <StepWelcome onNext={handleNext} onSkip={handleSkip} />;
      case 2:
        return (
          <StepArea
            onNext={(areaData: AreaDto) => handleStepComplete({ createdArea: areaData })}
            onBack={handleBack}
            isLoading={isLoading}
            setIsLoading={setIsLoading}
          />
        );
      case 3:
        return (
          <StepPrinter
            onNext={(printerData: PrinterDto) => handleStepComplete({ createdPrinter: printerData })}
            onBack={handleBack}
            isLoading={isLoading}
            setIsLoading={setIsLoading}
          />
        );
      case 4:
        return (
          <StepCashierStation
            createdArea={wizardState.createdArea}
            createdPrinter={wizardState.createdPrinter}
            onNext={(stationData: CashierStationDto) => handleStepComplete({ createdCashierStation: stationData })}
            onBack={handleBack}
            isLoading={isLoading}
            setIsLoading={setIsLoading}
          />
        );
      case 5:
        return (
          <StepMenu
            createdArea={wizardState.createdArea}
            onNext={(menuData: { createdMenuCategory: MenuCategoryDto; createdMenuItem: MenuItemDto }) => handleStepComplete(menuData)}
            onBack={handleBack}
            isLoading={isLoading}
            setIsLoading={setIsLoading}
          />
        );
      case 6:
        return (
          <StepComplete
            wizardState={wizardState}
            onFinish={handleFinish}
          />
        );
      default:
        return null;
    }
  };

  const progressPercentage = (currentStep / TOTAL_STEPS) * 100;

  return (
    <Dialog open={isOpen} onOpenChange={setIsOpen}>
      <DialogContent className="sm:max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="text-xl font-semibold">{getStepTitle()}</DialogTitle>
          <div className="mt-4">
            <div className="flex justify-between text-sm text-muted-foreground mb-2">
              <span>Passo {currentStep} di {TOTAL_STEPS}</span>
              <span>{Math.round(progressPercentage)}% completato</span>
            </div>
            <Progress value={progressPercentage} className="w-full" />
          </div>
        </DialogHeader>

        <div className="py-6">
          {renderCurrentStep()}
        </div>

        {/* Navigation buttons are handled by individual step components */}
      </DialogContent>
    </Dialog>
  );
}
