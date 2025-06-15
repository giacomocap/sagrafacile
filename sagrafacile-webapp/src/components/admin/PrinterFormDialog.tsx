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
import { PrinterDto, PrinterUpsertDto, PrinterType, PrintMode } from '@/types'; // Added PrintMode
import printerService from '@/services/printerService';
import { v4 as uuidv4, validate } from 'uuid';
import { toast } from 'sonner';

// Schema di validazione basato su PrinterArchitecture.md e DTO
const formSchema = z.object({
  name: z.string().min(1, { message: "Il nome della stampante è obbligatorio." }),
  type: z.nativeEnum(PrinterType, { required_error: "Il tipo di stampante è obbligatorio." }),
  connectionString: z.string().min(1, { message: "La stringa di connessione o il GUID sono obbligatori." }),
  isEnabled: z.boolean(),
  printMode: z.nativeEnum(PrintMode, { required_error: "La modalità di stampa è obbligatoria." }),
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
      isEnabled: true,
      printMode: PrintMode.Immediate, // Default to Immediate
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
          isEnabled: printerToEdit.isEnabled,
          printMode: printerToEdit.printMode, // Set printMode in edit mode
        });
      } else {
        form.reset({
          name: '',
          type: undefined,
          connectionString: '', // Mantiene vuoto inizialmente per l'aggiunta
          isEnabled: true,
          printMode: PrintMode.Immediate, // Default for new printers
        });
      }
    } else {
      // Opzionalmente, resetta alla chiusura se desiderato
      // form.reset();
    }
  }, [isOpen, printerToEdit, isEditMode, form]);

  // Effetto per generare un GUID quando il tipo è WindowsUsb e si è in modalità aggiunta
  // e per impostare la modalità di stampa per stampanti di rete
  useEffect(() => {
    if (isOpen) {
      if (!isEditMode) { // Solo in modalità aggiunta o quando il tipo cambia
        if (printerType === PrinterType.WindowsUsb) {
          if (!form.getValues('connectionString')) {
            form.setValue('connectionString', uuidv4());
          }
          // Non resettare printMode qui, lascia che l'utente scelga o mantenga il default
        } else if (printerType === PrinterType.Network) {
          if (form.getValues('connectionString') && validate(form.getValues('connectionString'))) { // Pulisce se era un GUID
            form.setValue('connectionString', '');
          }
          form.setValue('printMode', PrintMode.Immediate); // For Network printers, mode is always Immediate
        }
      } else { // In modalità modifica, se il tipo cambia a Network, imposta printMode
        if (printerType === PrinterType.Network) {
          form.setValue('printMode', PrintMode.Immediate);
        }
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
      isEnabled: values.isEnabled,
      organizationId: orgId,
      printMode: values.printMode, // Added printMode to dataToSend
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

            {/* Render condizionale per Modalità di Stampa */}
            {printerType === PrinterType.WindowsUsb && (
                <FormField
                  control={form.control}
                  name="printMode"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Modalità di Stampa*</FormLabel>
                      <Select
                        onValueChange={(value) => field.onChange(parseInt(value, 10))}
                        value={field.value !== undefined ? String(field.value) : undefined}
                        defaultValue={field.value !== undefined ? String(field.value) : undefined}
                      >
                        <FormControl>
                          <SelectTrigger>
                            <SelectValue placeholder="Seleziona modalità di stampa" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          <SelectItem value={String(PrintMode.Immediate)}>Immediata (stampa subito)</SelectItem>
                          <SelectItem value={String(PrintMode.OnDemandWindows)}>Su Richiesta (in coda sull'app Windows)</SelectItem>
                        </SelectContent>
                      </Select>
                      <FormDescription>
                        Seleziona "Su Richiesta" solo per stampanti collegate a un PC con l'app SagraFacile Windows Printer Service.
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
            )}

            {printerType === PrinterType.Network && (
                 <FormField
                 control={form.control}
                 name="printMode"
                 render={() => (
                   <FormItem>
                     <FormLabel>Modalità di Stampa*</FormLabel>
                     <Select
                       value={String(PrintMode.Immediate)} // Sempre Immediata per Network
                       disabled={true} // Disabilitato
                     >
                       <FormControl>
                         <SelectTrigger>
                           <SelectValue />
                         </SelectTrigger>
                       </FormControl>
                       <SelectContent>
                         <SelectItem value={String(PrintMode.Immediate)}>Immediata (stampa subito)</SelectItem>
                       </SelectContent>
                     </Select>
                     <FormDescription>
                       Per le stampanti di rete, la modalità è sempre "Immediata".
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
