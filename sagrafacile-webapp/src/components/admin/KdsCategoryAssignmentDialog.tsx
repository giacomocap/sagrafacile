'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { KdsStationDto, MenuCategoryDto } from '@/types';
import apiClient from '@/services/apiClient';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Skeleton } from '@/components/ui/skeleton';
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

interface KdsCategoryAssignmentDialogProps {
    isOpen: boolean;
    onOpenChange: (isOpen: boolean) => void;
    station: KdsStationDto | null; // Station being configured
    areaId: string | number;
    orgId: string | number;
    onAssignmentSuccess?: () => void; // Optional: Callback if needed after save
}

export const KdsCategoryAssignmentDialog: React.FC<KdsCategoryAssignmentDialogProps> = ({
    isOpen,
    onOpenChange,
    station,
    areaId,
    orgId,
    onAssignmentSuccess,
}) => {
    const [allCategories, setAllCategories] = useState<MenuCategoryDto[]>([]);
    const [assignedCategoryIds, setAssignedCategoryIds] = useState<Set<number>>(new Set());
    const [initialAssignedIds, setInitialAssignedIds] = useState<Set<number>>(new Set()); // To track changes
    const [isLoading, setIsLoading] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const fetchAllCategories = useCallback(async () => {
        if (!orgId || !areaId) return;
        try {
            // Corrected API endpoint: Use /api/MenuCategories with areaId query parameter
            const response = await apiClient.get<MenuCategoryDto[]>(`/MenuCategories?areaId=${areaId}`);
            setAllCategories(response.data);
        } catch (err: any) {
            console.error("Error fetching all categories:", err);
            setError(`Caricamento categorie menu fallito: ${err.message || 'Errore sconosciuto'}`);
            toast.error("Caricamento categorie menu fallito.");
        }
    }, [orgId, areaId]);

    const fetchAssignedCategories = useCallback(async () => {
        if (!station || !orgId || !areaId) return;
        try {
            // Endpoint from KdsArchitecture.md
            const response = await apiClient.get<MenuCategoryDto[]>(`/organizations/${orgId}/areas/${areaId}/kds-stations/${station.id}/categories`);
            const ids = new Set(response.data.map(cat => cat.id));
            setAssignedCategoryIds(ids);
            setInitialAssignedIds(new Set(ids)); // Store initial state for comparison on save
        } catch (err: any) {
            console.error("Error fetching assigned categories:", err);
            setError(`Caricamento categorie assegnate fallito: ${err.message || 'Errore sconosciuto'}`);
            toast.error("Caricamento categorie assegnate fallito.");
        }
    }, [station, orgId, areaId]);

    // Fetch data when dialog opens or station changes
    useEffect(() => {
        if (isOpen && station) {
            setIsLoading(true);
            setError(null);
            Promise.all([fetchAllCategories(), fetchAssignedCategories()])
                .finally(() => setIsLoading(false));
        } else {
            // Reset state when closed
            setAllCategories([]);
            setAssignedCategoryIds(new Set());
            setInitialAssignedIds(new Set());
            setError(null);
            setIsLoading(false);
            setIsSaving(false);
        }
    }, [isOpen, station, fetchAllCategories, fetchAssignedCategories]);

    const handleCheckboxChange = (categoryId: number, checked: boolean) => {
        setAssignedCategoryIds(prev => {
            const newSet = new Set(prev);
            if (checked) {
                newSet.add(categoryId);
            } else {
                newSet.delete(categoryId);
            }
            return newSet;
        });
        setError(null); // Clear error on interaction
    };

    const handleSaveChanges = async () => {
        if (!station) return;
        setIsSaving(true);
        setError(null);

        const categoriesToAdd = [...assignedCategoryIds].filter(id => !initialAssignedIds.has(id));
        const categoriesToRemove = [...initialAssignedIds].filter(id => !assignedCategoryIds.has(id));

        const promises: Promise<any>[] = [];

        // API calls for adding assignments
        categoriesToAdd.forEach(categoryId => {
            promises.push(
                apiClient.post(`/organizations/${orgId}/areas/${areaId}/kds-stations/${station.id}/categories/${categoryId}`)
            );
        });

        // API calls for removing assignments
        categoriesToRemove.forEach(categoryId => {
            promises.push(
                apiClient.delete(`/organizations/${orgId}/areas/${areaId}/kds-stations/${station.id}/categories/${categoryId}`)
            );
        });

        try {
            await Promise.all(promises);
            toast.success(`Assegnazioni categorie per ${station.name} aggiornate con successo.`);
            // Update initial state to reflect saved changes
            setInitialAssignedIds(new Set(assignedCategoryIds));
            onAssignmentSuccess?.(); // Call optional success callback
            onOpenChange(false); // Close dialog on success
        } catch (err: any) {
            console.error("Error updating category assignments:", err);
            const message = `Aggiornamento assegnazioni fallito: ${err.response?.data?.title || err.message || 'Controlla la console.'}`;
            setError(message);
            toast.error(message);
            // Optionally refetch assigned categories to revert UI state on error?
            // fetchAssignedCategories();
        } finally {
            setIsSaving(false);
        }
    };

    const hasChanges = initialAssignedIds.size !== assignedCategoryIds.size ||
                       ![...initialAssignedIds].every(id => assignedCategoryIds.has(id));

    return (
        <Dialog open={isOpen} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-lg overflow-y-scroll max-h-screen"> {/* Increased width */}
                <DialogHeader>
                    <DialogTitle>Assegna Categorie a "{station?.name}"</DialogTitle>
                    <DialogDescription>
                        Seleziona le categorie di menu i cui articoli dovrebbero apparire su questa stazione KDS.
                    </DialogDescription>
                </DialogHeader>
                <div className="py-4">
                    {error && !isLoading && (
                        <p className="col-span-4 text-red-500 text-sm text-center px-4 mb-4">{error}</p>
                    )}
                    {isLoading ? (
                        <div className="space-y-2 pr-6"> {/* Added padding right for scrollbar */}
                            <Skeleton className="h-6 w-full" />
                            <Skeleton className="h-6 w-full" />
                            <Skeleton className="h-6 w-2/3" />
                            <Skeleton className="h-6 w-full" />
                        </div>
                    ) : allCategories.length > 0 ? (
                        <ScrollArea className="h-72 w-full rounded-md border p-4"> {/* Scrollable area */}
                            <div className="space-y-2">
                                {allCategories.map(category => (
                                    <div key={category.id} className="flex items-center space-x-2">
                                        <Checkbox
                                            id={`category-${category.id}`}
                                            checked={assignedCategoryIds.has(category.id)}
                                            onCheckedChange={(checked) => handleCheckboxChange(category.id, !!checked)}
                                            disabled={isSaving}
                                        />
                                        <Label
                                            htmlFor={`category-${category.id}`}
                                            className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
                                        >
                                            {category.name}
                                        </Label>
                                    </div>
                                ))}
                            </div>
                        </ScrollArea>
                    ) : (
                        <p className="text-sm text-muted-foreground">Nessuna categoria di menu trovata per questa area.</p>
                    )}
                </div>
                <DialogFooter>
                    <DialogClose asChild>
                        <Button type="button" variant="outline" disabled={isSaving}>
                            Annulla
                        </Button>
                    </DialogClose>
                    <Button
                        type="button"
                        onClick={handleSaveChanges}
                        disabled={isSaving || isLoading || !hasChanges}
                    >
                        {isSaving ? (
                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                            'Salva Modifiche'
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
};
