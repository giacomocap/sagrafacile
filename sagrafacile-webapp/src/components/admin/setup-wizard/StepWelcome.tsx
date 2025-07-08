'use client';

import React from 'react';
import { Button } from '@/components/ui/button';
import { Rocket, Clock, CheckCircle } from 'lucide-react';

interface StepWelcomeProps {
  onNext: () => void;
  onSkip: () => void;
}

export default function StepWelcome({ onNext, onSkip }: StepWelcomeProps) {
  return (
    <div className="text-center space-y-6">
      <div className="flex justify-center">
        <div className="w-16 h-16 bg-primary/10 rounded-full flex items-center justify-center">
          <Rocket className="w-8 h-8 text-primary" />
        </div>
      </div>

      <div className="space-y-4">
        <h2 className="text-2xl font-semibold text-gray-900">
          Benvenuto in SagraFacile!
        </h2>
        <p className="text-lg text-gray-600 max-w-md mx-auto">
          Questo wizard ti aiuterà a configurare le basi per il tuo evento in pochi minuti.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 max-w-2xl mx-auto">
        <div className="flex flex-col items-center space-y-2 p-4 bg-gray-50 rounded-lg">
          <CheckCircle className="w-6 h-6 text-green-500" />
          <span className="text-sm font-medium">Area Operativa</span>
          <span className="text-xs text-gray-500 text-center">Crea la tua prima zona di lavoro</span>
        </div>
        <div className="flex flex-col items-center space-y-2 p-4 bg-gray-50 rounded-lg">
          <CheckCircle className="w-6 h-6 text-green-500" />
          <span className="text-sm font-medium">Stampante</span>
          <span className="text-xs text-gray-500 text-center">Configura la stampa di scontrini</span>
        </div>
        <div className="flex flex-col items-center space-y-2 p-4 bg-gray-50 rounded-lg">
          <CheckCircle className="w-6 h-6 text-green-500" />
          <span className="text-sm font-medium">Menu Base</span>
          <span className="text-xs text-gray-500 text-center">Aggiungi i primi piatti</span>
        </div>
      </div>

      <div className="flex items-center justify-center space-x-2 text-sm text-gray-500">
        <Clock className="w-4 h-4" />
        <span>Tempo stimato: 3-5 minuti</span>
      </div>

      <div className="flex justify-center space-x-4 pt-4">
        <Button variant="outline" onClick={onSkip}>
          Configurerò più tardi
        </Button>
        <Button onClick={onNext} className="px-8">
          Iniziamo!
        </Button>
      </div>
    </div>
  );
}
