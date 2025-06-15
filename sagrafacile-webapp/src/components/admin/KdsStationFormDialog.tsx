'use client';

import React, { useState, useEffect } from 'react';
import { KdsStationDto } from '@/types'; // Assuming KdsStationUpsertDto is not needed client-side yet
import apiClient from '@/services/apiClient';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogFooter,
    DialogClose,
    DialogDescription,
} from '@/components/ui/dialog';
import { Loader2 } from 'lucide-react';

interface KdsStationFormDialogProps {
    isOpen: boolean;
    onOpenChange: (isOpen: boolean) => void;
    stationToEdit?: KdsStationDto | null; // Pass station data for editing
    areaId: string | number; // Area context is needed for API call
    orgId: string | number; // Org context is needed for API call
    onSuccess: () => void; // Callback to refresh the list on the parent page
}

// Define the shape of the data needed for upserting
interface KdsStationUpsertData {
    name: string;
    // areaId and orgId are part of the URL, not the body for the backend endpoint
}

export const KdsStationFormDialog: React.FC<KdsStationFormDialogProps> = ({
    isOpen,
    onOpenChange,
    stationToEdit,
    areaId,
    orgId,
    onSuccess,
}) => {
    const [formData, setFormData] = useState<KdsStationUpsertData>({ name: '' });
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const isEditMode = !!stationToEdit;

    // Reset form when dialog opens or stationToEdit changes
    useEffect(() => {
        if (isOpen) {
            setFormData({ name: stationToEdit?.name || '' });
            setError(null); // Clear previous errors
            setIsLoading(false);
        }
    }, [isOpen, stationToEdit]);

    const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const { name, value } = e.target;
        setFormData(prev => ({ ...prev, [name]: value }));
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!formData.name.trim()) {
            setError("Il nome della stazione è obbligatorio.");
            return;
        }
        if (isEditMode && formData.name.trim() === stationToEdit?.name) {
            setError("Nessuna modifica rilevata.");
            return;
        }

        setIsLoading(true);
        setError(null);

        const apiUrl = isEditMode
            ? `/organizations/${orgId}/areas/${areaId}/kds-stations/${stationToEdit?.id}`
            : `/organizations/${orgId}/areas/${areaId}/kds-stations`;
        const apiMethod = isEditMode ? apiClient.put : apiClient.post;
        const successMessage = isEditMode ? 'Stazione KDS aggiornata con successo.' : 'Stazione KDS creata con successo.';
        const failureMessage = isEditMode ? 'Impossibile aggiornare la stazione KDS.' : 'Impossibile creare la stazione KDS.';

        try {
            // Backend expects only the name in the body for Upsert Dto
            const dataToSend: { name: string } = { name: formData.name.trim() };
            await apiMethod(apiUrl, dataToSend);
            toast.success(successMessage);
            onSuccess(); // Call the success callback (e.g., refetch list)
            onOpenChange(false); // Close the dialog
        } catch (err: any) {
            console.error(failureMessage, err);
            const message = `${failureMessage} ${err.response?.data?.title || err.message || 'Controlla la console.'}`;
            setError(message);
            toast.error(message);
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <Dialog open={isOpen} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-md">
                <DialogHeader>
                    <DialogTitle>{isEditMode ? 'Modifica Stazione KDS' : 'Aggiungi Nuova Stazione KDS'}</DialogTitle>
                    {!isEditMode && (
                        <DialogDescription>Inserisci il nome per la nuova stazione KDS.</DialogDescription>
                    )}
                </DialogHeader>
                <form onSubmit={handleSubmit}>
                    <div className="grid gap-4 py-4">
                        <div className="grid grid-cols-4 items-center gap-4">
                            <Label htmlFor="name" className="text-right">
                                Nome*
                            </Label>
                            <Input
                                id="name"
                                name="name" // Make sure name attribute matches state key
                                value={formData.name}
                                onChange={handleInputChange}
                                className="col-span-3"
                                placeholder="Es. Cucina Caldi, Bar, Pizzeria"
                                disabled={isLoading}
                            />
                        </div>
                        {error && (
                            <p className="col-span-4 text-red-500 text-sm text-center px-4">{error}</p>
                        )}
                    </div>
                    <DialogFooter>
                        <DialogClose asChild>
                            <Button type="button" variant="outline" disabled={isLoading}>
                                Annulla
                            </Button>
                        </DialogClose>
                        <Button type="submit" disabled={isLoading || !formData.name.trim() || (isEditMode && formData.name.trim() === stationToEdit?.name)}>
                            {isLoading ? (
                                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                            ) : isEditMode ? (
                                'Salva Modifiche'
                            ) : (
                                'Crea Stazione'
                            )}
                        </Button>
                    </DialogFooter>
                </form>
            </DialogContent>
        </Dialog>
    );
};
