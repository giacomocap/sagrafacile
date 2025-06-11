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
import { AdAreaAssignmentDto, AdMediaItemDto } from '@/types';
import apiClient from '@/services/apiClient';
import { toast } from 'sonner';

const formSchema = z.object({
  adMediaItemId: z.string().min(1, "È necessario selezionare un media."),
  displayOrder: z.coerce.number().min(0, "L'ordine non può essere negativo."),
  durationSeconds: z.coerce.number().optional().nullable(),
  isActive: z.boolean(),
});

interface AdAssignmentUpsertDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  assignmentToEdit?: AdAreaAssignmentDto | null;
  onSaveSuccess: () => void;
  areaId: number;
  availableAds: AdMediaItemDto[];
}

export default function AdAssignmentUpsertDialog({ isOpen, onOpenChange, assignmentToEdit, onSaveSuccess, areaId, availableAds }: AdAssignmentUpsertDialogProps) {
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const isEditMode = !!assignmentToEdit;

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      adMediaItemId: '',
      displayOrder: 0,
      durationSeconds: 10,
      isActive: true,
    },
  });

  const adMediaItemId = form.watch('adMediaItemId');
  const selectedAd = React.useMemo(
    () => availableAds.find((ad) => ad.id === adMediaItemId),
    [adMediaItemId, availableAds]
  );

  useEffect(() => {
    if (selectedAd) {
      if (selectedAd.mediaType === 'Video') {
        form.setValue('durationSeconds', null, { shouldValidate: true });
      } else {
        const currentDuration = form.getValues('durationSeconds');
        if (currentDuration === null || currentDuration === undefined) {
          form.setValue('durationSeconds', 10, { shouldValidate: true });
        }
      }
    }
  }, [selectedAd, form]);

  useEffect(() => {
    if (isOpen) {
      setError(null);
      if (isEditMode && assignmentToEdit) {
        form.reset({
          adMediaItemId: assignmentToEdit.adMediaItemId,
          displayOrder: assignmentToEdit.displayOrder,
          durationSeconds: assignmentToEdit.durationSeconds,
          isActive: assignmentToEdit.isActive,
        });
      } else {
        form.reset({
          adMediaItemId: '',
          displayOrder: 0,
          durationSeconds: 10,
          isActive: true,
        });
      }
    }
  }, [isOpen, assignmentToEdit, isEditMode, form]);

  async function onSubmit(values: z.infer<typeof formSchema>) {
    setIsLoading(true);
    setError(null);

    const payload = {
        ...values,
        areaId: areaId,
        adMediaItemId: values.adMediaItemId
    };

    try {
        if (isEditMode && assignmentToEdit) {
            await apiClient.put(`/admin/ad-assignments/${assignmentToEdit.id}`, payload);
            toast.success("Assegnazione aggiornata con successo.");
        } else {
            await apiClient.post(`/admin/ad-assignments`, payload);
            toast.success("Assegnazione creata con successo.");
        }
        onSaveSuccess();
        onOpenChange(false);
    } catch (err: any) {
        console.error('Errore nel salvataggio dell\'assegnazione:', err);
        const errorMessage = err.response?.data?.message || 'Salvataggio fallito.';
        setError(errorMessage);
        toast.error("Errore nel salvataggio", { description: errorMessage });
    } finally {
        setIsLoading(false);
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEditMode ? 'Modifica Assegnazione' : 'Assegna Media ad Area'}</DialogTitle>
          <DialogDescription>
            Scegli un media dalla libreria e configuralo per quest'area.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6 py-4">
            <FormField
              control={form.control}
              name="adMediaItemId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Media</FormLabel>
                  <Select onValueChange={field.onChange} defaultValue={field.value} disabled={isEditMode}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Seleziona un media dalla libreria" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {availableAds.map(ad => (
                        <SelectItem key={ad.id} value={ad.id}>{ad.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="displayOrder"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Ordine di Visualizzazione</FormLabel>
                  <FormControl>
                    <Input type="number" {...field} />
                  </FormControl>
                  <FormDescription>Numero più basso viene visualizzato prima.</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            {selectedAd?.mediaType !== 'Video' && (
              <FormField
                control={form.control}
                name="durationSeconds"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Durata (secondi)</FormLabel>
                    <FormControl>
                      <Input type="number" {...field} value={field.value ?? ''} />
                    </FormControl>
                    <FormDescription>
                      Per le immagini, indica per quanti secondi mostrarle. Lascia vuoto per usare la durata del video.
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

            <FormField
              control={form.control}
              name="isActive"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3 shadow-sm">
                  <div className="space-y-0.5">
                    <FormLabel>Attiva</FormLabel>
                    <FormDescription>
                      Mostra questa pubblicità nella rotazione per quest'area.
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
                {isLoading ? "Salvataggio..." : "Salva"}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
