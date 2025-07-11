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
import { AdMediaItemDto } from '@/types';
import apiClient from '@/services/apiClient';
import { toast } from 'sonner';

const formSchema = z.object({
  name: z.string().min(1, "Il nome è obbligatorio.").max(100, "Il nome non può superare i 100 caratteri."),
  file: z.instanceof(File).optional(),
});

interface AdUpsertDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  adToEdit?: AdMediaItemDto | null;
  onSaveSuccess: () => void;
  organizationId: string;
}

export default function AdUpsertDialog({ isOpen, onOpenChange, adToEdit, onSaveSuccess, organizationId }: AdUpsertDialogProps) {
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const isEditMode = !!adToEdit;

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      name: '',
      file: undefined,
    },
  });

  useEffect(() => {
    if (isOpen) {
      setError(null);
      if (isEditMode && adToEdit) {
        form.reset({
          name: adToEdit.name,
          file: undefined,
        });
      } else {
        form.reset({
          name: '',
          file: undefined,
        });
      }
    }
  }, [isOpen, adToEdit, isEditMode, form]);

  async function onSubmit(values: z.infer<typeof formSchema>) {
    setIsLoading(true);
    setError(null);

    if (!isEditMode && !values.file) {
        toast.error("È necessario selezionare un file per creare un nuovo media.");
        setIsLoading(false);
        return;
    }

    const formData = new FormData();
    formData.append('name', values.name);
    if (values.file) {
        formData.append('file', values.file);
    }

    try {
        const config = {
            headers: { 'Content-Type': 'multipart/form-data' }
        };

        if (isEditMode && adToEdit) {
            await apiClient.put(`/admin/ads/${adToEdit.id}`, formData, config);
            toast.success("Media aggiornato con successo.");
        } else {
            await apiClient.post(`/admin/organizations/${organizationId}/ads`, formData, config);
            toast.success("Media creato con successo.");
        }
        onSaveSuccess();
        onOpenChange(false);
    } catch (err: any) {
        console.error('Errore nel salvataggio del media:', err);
        const errorMessage = err.response?.data?.message || 'Salvataggio fallito.';
        setError(errorMessage);
        toast.error("Errore nel salvataggio", { description: errorMessage });
    } finally {
        setIsLoading(false);
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg overflow-y-scroll max-h-screen">
        <DialogHeader>
          <DialogTitle>{isEditMode ? 'Modifica Media' : 'Aggiungi Nuovo Media'}</DialogTitle>
          <DialogDescription>
            Carica un'immagine o un video e dagli un nome.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6 py-4">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nome</FormLabel>
                  <FormControl>
                    <Input placeholder="Es. Sponsor ACME" {...field} />
                  </FormControl>
                  <FormDescription>Un nome per identificare facilmente questo media.</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
            
            {!isEditMode && (
              <FormField
                control={form.control}
                name="file"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>File*</FormLabel>
                    <FormControl>
                      <Input
                        type="file"
                        accept="image/*,video/*"
                        onChange={(e) => {
                            const file = e.target.files ? e.target.files[0] : null;
                            field.onChange(file);
                            if (file) {
                                form.setValue('name', file.name);
                            }
                        }}
                      />
                    </FormControl>
                    <FormDescription>Seleziona un file immagine (JPG, PNG, GIF) o video (MP4).</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

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
