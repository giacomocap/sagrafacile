'use client';

import React, { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Printer, ChevronLeft, ChevronRight, Loader2, Network, Usb, HelpCircle, RefreshCw } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { useAuth } from '@/contexts/AuthContext';
import printerService from '@/services/printerService';
import { PrinterDto, PrinterUpsertDto, PrinterType, DocumentType, PrintMode } from '@/types';
import { toast } from 'sonner';

interface StepPrinterProps {
  onNext: (printerData: PrinterDto) => void;
  onBack: () => void;
  isLoading: boolean;
  setIsLoading: (loading: boolean) => void;
}

export default function StepPrinter({ onNext, onBack, isLoading, setIsLoading }: StepPrinterProps) {
  const { user } = useAuth();
  const [printerName, setPrinterName] = useState('');
  const [printerType, setPrinterType] = useState<PrinterType | null>(null);
  const [connectionString, setConnectionString] = useState('');
  const [error, setError] = useState<string | null>(null);

  const generateGuid = () => {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
      const r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }

  useEffect(() => {
    if (printerType === PrinterType.WindowsUsb && !connectionString) {
      setConnectionString(generateGuid());
    } else if (printerType === PrinterType.Network) {
      setConnectionString('');
    }
  }, [connectionString, printerType]);

  const handleSubmit = async () => {
    if (!printerName.trim()) {
      setError("Il nome della stampante è obbligatorio.");
      return;
    }

    if (!printerType) {
      setError("Seleziona il tipo di stampante.");
      return;
    }

    if (!connectionString.trim()) {
      setError("Le informazioni di connessione sono obbligatorie.");
      return;
    }

    if (!user?.organizationId) {
      setError("Informazioni organizzazione mancanti.");
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const printerData: PrinterUpsertDto = {
        name: printerName.trim(),
        type: printerType,
        connectionString: connectionString.trim(),
        organizationId: user.organizationId,
        documentType: DocumentType.EscPos, // Default to ESC/POS for simplicity
        printMode: PrintMode.Immediate, // Default to immediate printing
        paperSize: null, // Default to null for ESC/POS
        isEnabled: true,
      };

      const response = await printerService.createPrinter(printerData);
      toast.success(`Stampante "${response.name}" configurata con successo!`);
      onNext(response);
    } catch (err: any) {
      console.error('Error creating printer:', err);
      const errorMessage = err.response?.data?.title || err.response?.data?.message || 'Configurazione stampante fallita.';
      setError(errorMessage);
      toast.error(errorMessage);
    } finally {
      setIsLoading(false);
    }
  };

  const getConnectionPlaceholder = () => {
    switch (printerType) {
      case PrinterType.Network:
        return "es. 192.168.1.100:9100";
      case PrinterType.WindowsUsb:
        return "es. 12345678-1234-1234-1234-123456789abc";
      default:
        return "Informazioni di connessione";
    }
  };

  const getConnectionHelp = () => {
    switch (printerType) {
      case PrinterType.Network:
        return "Inserisci l'indirizzo IP della stampante seguito dalla porta (solitamente 9100)";
      case PrinterType.WindowsUsb:
        return "Inserisci il GUID univoco per questa stampante USB (verrà utilizzato dall'app Windows)";
      default:
        return "";
    }
  };

  return (
    <div className="space-y-6">
      <div className="text-center space-y-4">
        <div className="flex justify-center">
          <div className="w-12 h-12 bg-green-100 rounded-full flex items-center justify-center">
            <Printer className="w-6 h-6 text-green-600" />
          </div>
        </div>
        <div>
          <h3 className="text-lg font-semibold">Configura una Stampante</h3>
          <p className="text-gray-600 max-w-md mx-auto">
            Aggiungiamo una stampante per scontrini o comande. Può essere una stampante di rete 
            o una stampante USB collegata a un PC Windows.
          </p>
        </div>
      </div>

      <div className="max-w-md mx-auto space-y-4">
        <div className="space-y-2">
          <Label htmlFor="printerName">Nome Stampante *</Label>
          <Input
            id="printerName"
            placeholder="es. Stampante Cassa, Stampante Cucina"
            value={printerName}
            onChange={(e) => setPrinterName(e.target.value)}
            disabled={isLoading}
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="printerType">Tipo Stampante *</Label>
          <Select value={printerType?.toString() || ''} onValueChange={(value) => {
            const newType = parseInt(value) as PrinterType;
            setPrinterType(newType);
            if (newType === PrinterType.WindowsUsb) {
              setConnectionString(generateGuid());
            } else {
              setConnectionString('');
            }
          }}>
            <SelectTrigger>
              <SelectValue placeholder="Seleziona il tipo di stampante" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={PrinterType.Network.toString()}>
                <div className="flex items-center">
                  <Network className="w-4 h-4 mr-2" />
                  Stampante di Rete
                </div>
              </SelectItem>
              <SelectItem value={PrinterType.WindowsUsb.toString()}>
                <div className="flex items-center">
                  <Usb className="w-4 h-4 mr-2" />
                  Stampante USB Windows
                </div>
              </SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <div className="flex items-center space-x-2">
            <Label htmlFor="connectionString">Connessione *</Label>
            {printerType && (
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-6 w-6">
                      <HelpCircle className="w-4 h-4 text-gray-400" />
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent>
                    <p className="max-w-xs">{getConnectionHelp()}</p>
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            )}
          </div>
          <div className="relative">
            <Input
              id="connectionString"
              placeholder={getConnectionPlaceholder()}
              value={connectionString}
              onChange={(e) => setConnectionString(e.target.value)}
              disabled={isLoading || !printerType || printerType === PrinterType.WindowsUsb}
              className={printerType === PrinterType.WindowsUsb ? "pr-10" : ""}
            />
            {printerType === PrinterType.WindowsUsb && (
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="absolute inset-y-0 right-0 h-full px-3"
                onClick={() => setConnectionString(generateGuid())}
                disabled={isLoading}
              >
                <RefreshCw className="w-4 h-4" />
              </Button>
            )}
          </div>
          {printerType === PrinterType.WindowsUsb && (
            <p className="text-xs text-gray-500">
              Dovrai installare l'app Windows Printer Service sul PC collegato alla stampante
            </p>
          )}
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
          disabled={!printerName.trim() || !printerType || !connectionString.trim() || isLoading}
        >
          {isLoading ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Configurazione...
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
