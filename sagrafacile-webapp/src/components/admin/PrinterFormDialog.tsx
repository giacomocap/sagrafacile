'use client';

import React, { useState, useEffect } from 'react';
import { zodResolver } from "@hookform/resolvers/zod"
import { useForm } from "react-hook-form"
import * as z from "zod"
import { Button } from "@/components/ui/button"
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogClose,
  DialogDescription,
} from '@/components/ui/dialog';
import { Input } from "@/components/ui/input"
import { Switch } from "@/components/ui/switch"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { PrinterDto, PrinterUpsertDto, PrinterType } from '@/types';
import printerService from '@/services/printerService';
import { v4 as uuidv4, validate } from 'uuid';
import { toast } from 'sonner';

// Schema di validazione basato su PrinterArchitecture.md e DTO
const formSchema = z.object({
  name: z.string().min(1, { message: "Il nome della stampante è obbligatorio." }),
  type: z.nativeEnum(PrinterType, { required_error: "Il tipo di stampante è obbligatorio." }),
  connectionString: z.string().min(1, { message: "La stringa di connessione o il GUID sono obbligatori." }),
  windowsPrinterName: z.string().optional(),
  isEnabled: z.boolean(),
}).refine(data => {
  if (data.type === PrinterType.WindowsUsb) {
    return data.windowsPrinterName && data.windowsPrinterName.trim().length > 0;
  }
  return true;
}, {
  message: "Il Nome Stampante Windows è obbligatorio quando il tipo è Windows USB.",
  path: ["windowsPrinterName"], // Assegna l'errore a questo campo
}).refine(data => {
  if (data.type === PrinterType.Network) {
    // Validazione di base: controlla il pattern qualcosa:numero
    const parts = data.connectionString.split(':');
    return parts.length === 2 && parts[0].length > 0 && !isNaN(parseInt(parts[1], 10));
  }
  return true;
}, {
  message: "La Stringa di Connessione di Rete deve essere nel formato Indirizzo_IP:Porta (es. 192.168.1.100:9100).",
  path: ["connectionString"],
}).refine(data => {
    if (data.type === PrinterType.WindowsUsb) {
        // Usa uuid.validate() per la validazione
        return validate(data.connectionString);
    }
    return true;
}, {
    message: "La Stringa di Connessione deve essere un GUID valido per le stampanti Windows USB.",
    path: ["connectionString"],
});

interface PrinterFormDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  printerToEdit?: PrinterDto | null;
  onSaveSuccess: () => void;
  orgId: number;
}

export default function PrinterFormDialog({ isOpen, onOpenChange, printerToEdit, onSaveSuccess,orgId }: PrinterFormDialogProps) {
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const isEditMode = !!printerToEdit;

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      name: '',
      type: undefined,
      connectionString: '',
      windowsPrinterName: '',
      isEnabled: true,
    },
  });

  const printerType = form.watch("type");

  // Effetto per resettare il form quando il dialog si apre o printerToEdit cambia
  useEffect(() => {
    if (isOpen) {
      setError(null);
      if (isEditMode && printerToEdit) {
        form.reset({
          name: printerToEdit.name,
          type: printerToEdit.type,
          connectionString: printerToEdit.connectionString,
          windowsPrinterName: printerToEdit.windowsPrinterName || '',
          isEnabled: printerToEdit.isEnabled,
        });
      } else {
        form.reset({
          name: '',
          type: undefined,
          connectionString: '', // Mantiene vuoto inizialmente per l'aggiunta
          windowsPrinterName: '',
          isEnabled: true,
        });
      }
    } else {
      // Opzionalmente, resetta alla chiusura se desiderato
      // form.reset();
    }
  }, [isOpen, printerToEdit, isEditMode, form]);

  // Effetto per generare un GUID quando il tipo è WindowsUsb e si è in modalità aggiunta
  useEffect(() => {
    if (isOpen && !isEditMode && printerType === PrinterType.WindowsUsb) {
        // Imposta solo se la stringa di connessione è attualmente vuota
        if (!form.getValues('connectionString')) {
            form.setValue('connectionString', uuidv4());
        }
    } else if (isOpen && !isEditMode && printerType === PrinterType.Network) {
        // Pulisce il GUID se si torna a Rete in modalità aggiunta
        if (form.getValues('connectionString') && form.getValues('connectionString').length === 36) { // Controllo base sulla lunghezza dell'UUID
            form.setValue('connectionString', '');
        }
    }
  }, [isOpen, isEditMode, printerType, form]);


  async function onSubmit(values: z.infer<typeof formSchema>) {
    setIsLoading(true);
    setError(null);

    const dataToSend: PrinterUpsertDto = {
      name: values.name,
      type: values.type,
      connectionString: values.connectionString,
      windowsPrinterName: values.type === PrinterType.WindowsUsb ? values.windowsPrinterName : null,
      isEnabled: values.isEnabled,
      organizationId: orgId,
    };

    try {
      if (isEditMode && printerToEdit) {
        await printerService.updatePrinter(printerToEdit.id, dataToSend);
        toast.success("Stampante aggiornata con successo.");
      } else {
        await printerService.createPrinter(dataToSend);
        toast.success("Stampante creata con successo.");
      }
      onSaveSuccess(); // Chiama la callback per ricaricare la lista genitore
      onOpenChange(false); // Chiude il dialog
    } catch (err: any) {
      console.error('Errore nel salvataggio della stampante:', err);
      const errorMessage = err.response?.data?.title || err.response?.data || 'Salvataggio stampante fallito.';
      setError(errorMessage);
      toast.error("Errore nel salvataggio della stampante", { description: errorMessage });
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEditMode ? 'Modifica Stampante' : 'Aggiungi Nuova Stampante'}</DialogTitle>
          <DialogDescription>
            Configura i dettagli della stampante. Assicurati che la stringa di connessione/GUID e il nome Windows (se applicabile) siano corretti.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6 py-4">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nome Stampante*</FormLabel>
                  <FormControl>
                    <Input placeholder="es. Scontrini Cassa 1, Comande Bar" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="type"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Tipo Stampante*</FormLabel>
                  <Select
                    onValueChange={(value) => field.onChange(parseInt(value, 10))}
                    value={field.value !== undefined ? String(field.value) : undefined}
                    defaultValue={field.value !== undefined ? String(field.value) : undefined}
                  >
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Seleziona tipo stampante" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      <SelectItem value={String(PrinterType.Network)}>Rete (ESC/POS tramite IP:Porta)</SelectItem>
                      <SelectItem value={String(PrinterType.WindowsUsb)}>Windows USB (tramite Companion App)</SelectItem>
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="connectionString"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>
                    {printerType === PrinterType.WindowsUsb ? 'GUID Companion App*' : 'Stringa di Connessione di Rete*'}
                  </FormLabel>
                  <FormControl>
                    <Input
                      placeholder={printerType === PrinterType.WindowsUsb ? 'ID univoco auto-generato' : 'es. 192.168.1.100:9100'}
                      {...field}
                      readOnly={printerType === PrinterType.WindowsUsb && !isEditMode} // Sola lettura per GUID in modalità aggiunta
                    />
                  </FormControl>
                  <FormDescription>
                    {printerType === PrinterType.WindowsUsb
                      ? 'ID univoco per l\'istanza della companion app. Copialo nella configurazione dell\'app.'
                      : 'Indirizzo IP e porta per la stampa di rete diretta.'}
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/* Render condizionale per Nome Stampante Windows */}
            {printerType === PrinterType.WindowsUsb && (
              <FormField
                control={form.control}
                name="windowsPrinterName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Nome Stampante Windows*</FormLabel>
                    <FormControl>
                      <Input placeholder="es. ZDesigner ZD420-203dpi ZPL" {...field} value={field.value ?? ''} />
                    </FormControl>
                    <FormDescription>
                      Il nome esatto della stampante come appare nel Pannello di Controllo di Windows.
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

            <FormField
              control={form.control}
              name="isEnabled"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3 shadow-sm">
                  <div className="space-y-0.5">
                    <FormLabel>Abilitata</FormLabel>
                    <FormDescription>
                      Consenti a questa stampante di essere utilizzata per la stampa.
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                    />
                  </FormControl>
                </FormItem>
              )}
            />

            {error && <p className="text-sm font-medium text-destructive">{error}</p>}

            <DialogFooter>
                <DialogClose asChild>
                    <Button type="button" variant="outline">Annulla</Button>
                </DialogClose>
                <Button type="submit" disabled={isLoading}>
                    {isLoading ? "Salvataggio..." : "Salva Stampante"}
                </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
