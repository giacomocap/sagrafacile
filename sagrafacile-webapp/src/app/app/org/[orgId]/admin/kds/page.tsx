'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link'; // Import Link
import { KdsStationDto } from '@/types';
import apiClient from '@/services/apiClient';
import { toast } from 'sonner';
import AdminAreaSelector from '@/components/shared/AdminAreaSelector';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PlusCircle, Edit, Trash2, ListChecks, ExternalLink } from 'lucide-react'; // Added ExternalLink
import { Skeleton } from '@/components/ui/skeleton';
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
    AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { KdsStationFormDialog } from '@/components/admin/KdsStationFormDialog'; // Import the form dialog
import { KdsCategoryAssignmentDialog } from '@/components/admin/KdsCategoryAssignmentDialog'; // Import the assignment dialog

// Renamed component for clarity
const OrgKdsStationsAdminPage = () => {
    const params = useParams();
    const orgId = params.orgId as string;

    // State for Stations (specific to selected area)
    const [stations, setStations] = useState<KdsStationDto[]>([]);
    const [isLoadingStations, setIsLoadingStations] = useState(false); // Start false, load on area select
    const [stationsError, setStationsError] = useState<string | null>(null);

    const [selectedAreaId, setSelectedAreaId] = useState<string | undefined>(undefined);


    // State for Delete Dialog
    const [stationToDelete, setStationToDelete] = useState<KdsStationDto | null>(null);
    const [deleteError, setDeleteError] = useState<string | null>(null);

    // State for Create/Edit Dialog
    const [isFormDialogOpen, setIsFormDialogOpen] = useState(false);
    const [stationToEdit, setStationToEdit] = useState<KdsStationDto | null>(null);

    // State for Category Assignment Dialog
    const [isAssignmentDialogOpen, setIsAssignmentDialogOpen] = useState(false);
    const [stationForAssignment, setStationForAssignment] = useState<KdsStationDto | null>(null);


    // Fetch Stations for the selected Area
    const fetchStationsForArea = useCallback(async (areaId: string) => {
        if (!areaId) {
            setStations([]); // Clear stations if no area selected
            return;
        }
        setIsLoadingStations(true);
        setStationsError(null);
        try {
            const response = await apiClient.get<KdsStationDto[]>(`/organizations/${orgId}/areas/${areaId}/kds-stations`);
            setStations(response.data || []);
        } catch (err) {
            console.error(`Errore nel recupero delle stazioni KDS per l'area ${areaId}:`, err);
            const message = `Caricamento stazioni KDS per l'area selezionata fallito: ${(err instanceof Error ? err.message : 'Errore sconosciuto')}`;
            setStationsError(message);
            setStations([]); // Clear stations on error
            toast.error(message);
        } finally {
            setIsLoadingStations(false);
        }
    }, [orgId]); // Depends on orgId

    // Effect to fetch stations when selectedAreaId changes
    useEffect(() => {
        if (selectedAreaId) {
            fetchStationsForArea(selectedAreaId);
        } else {
            setStations([]); // Clear stations if area is deselected
            setStationsError(null); // Clear any previous errors
        }
    }, [selectedAreaId, fetchStationsForArea]);


    const handleOpenDeleteDialog = (station: KdsStationDto) => {
        setStationToDelete(station);
        setDeleteError(null);
    };

    const handleDeleteStation = async () => {
        if (!stationToDelete) return;
        setDeleteError(null);
        toast.info(`Tentativo di eliminare la stazione KDS ${stationToDelete.id}...`);
        if (!selectedAreaId) {
            toast.error("Impossibile eliminare la stazione: Nessuna area selezionata.");
            setDeleteError("Nessuna area selezionata.");
            return;
        }
        try {
            // Use the correct, fully qualified endpoint for deletion
            await apiClient.delete(`/organizations/${orgId}/areas/${selectedAreaId}/kds-stations/${stationToDelete.id}`);
            toast.success(`Stazione KDS ${stationToDelete.name} eliminata con successo.`);
            // Refresh the list for the *current* area after deletion
            if (selectedAreaId) {
                fetchStationsForArea(selectedAreaId);
            }
            setStationToDelete(null);
        } catch (err) {
            console.error("Errore nell'eliminazione della stazione KDS:", err);
            const errorResponse = err as { response?: { data?: { title?: string } }, message?: string };
            const message = `Eliminazione della stazione KDS ${stationToDelete.name} fallita: ${errorResponse.response?.data?.title || errorResponse.message || 'Controlla la console per i dettagli.'}`;
            setDeleteError(message); // Show error within the dialog
            toast.error(message);
        }
    };

    // --- Dialog Handlers ---

    const handleOpenCreateDialog = () => {
        setStationToEdit(null); // Ensure edit mode is off
        setIsFormDialogOpen(true);
    };

    const handleOpenEditDialog = (station: KdsStationDto) => {
        setStationToEdit(station);
        setIsFormDialogOpen(true);
    };

    const handleFormSuccess = () => {
        // Refresh the list for the *current* area on successful create/edit
        if (selectedAreaId) {
            fetchStationsForArea(selectedAreaId);
        }
    };

    const handleOpenCategoryAssignmentDialog = (station: KdsStationDto) => {
        setStationForAssignment(station);
        setIsAssignmentDialogOpen(true);
    };


    return (
        <div className="space-y-6">
            <AdminAreaSelector
                selectedAreaId={selectedAreaId}
                onAreaChange={setSelectedAreaId}
                title="Seleziona Area"
                description="Scegli un'area per visualizzare o gestire le sue stazioni KDS."
            />

            {/* Only show station management card if an area is selected */}
            {selectedAreaId && (
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                        <div>
                            <CardTitle>Stazioni KDS per l'Area Selezionata</CardTitle>
                            <CardDescription>Configura le stazioni all'interno dell'area scelta.</CardDescription>
                        </div>
                        <Button onClick={handleOpenCreateDialog} size="sm" disabled={!selectedAreaId}>
                            <PlusCircle className="mr-2 h-4 w-4" /> Aggiungi Stazione all'Area
                        </Button>
                    </CardHeader>
                    <CardContent>
                        {stationsError && <p className="text-red-500 mb-4">{stationsError}</p>}
                        {isLoadingStations ? (
                            <div className="space-y-2">
                                <Skeleton className="h-10 w-full" />
                                <Skeleton className="h-10 w-full" />
                                <Skeleton className="h-10 w-full" />
                            </div>
                        ) : stations.length > 0 ? ( // Use stations state directly
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>ID</TableHead>
                                        <TableHead>Nome</TableHead>
                                        {/* Area ID column removed as it's implied by the selection */}
                                        <TableHead>Visualizza KDS</TableHead> {/* Added View KDS column */}
                                        <TableHead className="text-right">Azioni</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {stations.map((station) => ( // Use stations state directly
                                        <TableRow key={station.id}>
                                            <TableCell>{station.id}</TableCell>
                                            <TableCell className="font-medium">{station.name}</TableCell>
                                            {/* Added Cell for View KDS Link */}
                                            <TableCell>
                                                <Button variant="outline" size="sm" asChild>
                                                    <Link href={`/app/org/${orgId}/area/${selectedAreaId}/kds/${station.id}`} target="_blank">
                                                        <ExternalLink className="mr-1 h-4 w-4" /> Visualizza
                                                    </Link>
                                                </Button>
                                            </TableCell>
                                            <TableCell className="text-right space-x-2">
                                                <Button
                                                    variant="outline"
                                                    size="sm"
                                                    onClick={() => handleOpenCategoryAssignmentDialog(station)}
                                                >
                                                    <ListChecks className="mr-1 h-4 w-4" /> Assegna Categorie
                                                </Button>
                                                <Button variant="outline" size="sm" onClick={() => handleOpenEditDialog(station)}>
                                                    <Edit className="mr-1 h-4 w-4" /> Modifica
                                                </Button>
                                                <AlertDialog>
                                                    <AlertDialogTrigger asChild>
                                                        <Button
                                                            variant="destructive"
                                                            size="sm"
                                                            onClick={() => handleOpenDeleteDialog(station)}
                                                        >
                                                            <Trash2 className="mr-1 h-4 w-4" /> Elimina
                                                        </Button>
                                                    </AlertDialogTrigger>
                                                    {stationToDelete && stationToDelete.id === station.id && (
                                                        <AlertDialogContent>
                                                            <AlertDialogHeader>
                                                                <AlertDialogTitle>Sei sicuro?</AlertDialogTitle>
                                                                <AlertDialogDescription>
                                                                    Questa azione non può essere annullata. Questo eliminerà permanentemente la stazione KDS "{stationToDelete.name}".
                                                                </AlertDialogDescription>
                                                            </AlertDialogHeader>
                                                            {deleteError && (
                                                                <p className="text-sm font-medium text-destructive bg-destructive/10 p-3 rounded-md">
                                                                    {deleteError}
                                                                </p>
                                                            )}
                                                            <AlertDialogFooter>
                                                                <AlertDialogCancel onClick={() => setStationToDelete(null)}>Annulla</AlertDialogCancel>
                                                                <AlertDialogAction onClick={handleDeleteStation}>
                                                                    Sì, elimina
                                                                </AlertDialogAction>
                                                            </AlertDialogFooter>
                                                        </AlertDialogContent>
                                                    )}
                                                </AlertDialog>
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        ) : (
                            <p className="text-center py-4 text-muted-foreground">
                                Nessuna stazione KDS trovata per questa area. Aggiungi una stazione KDS per iniziare.
                            </p>
                        )}
                    </CardContent>
                </Card>
            )}

            {/* Render the Create/Edit Dialog */}
            {/* Pass selectedAreaId only when adding (stationToEdit is null) */}
            <KdsStationFormDialog
                isOpen={isFormDialogOpen}
                onOpenChange={setIsFormDialogOpen}
                orgId={orgId}
                areaId={stationToEdit ? stationToEdit.areaId.toString() : selectedAreaId!} // Non-null assertion as button is disabled otherwise
                stationToEdit={stationToEdit}
                onSuccess={handleFormSuccess}
            />

            {/* Render the Category Assignment Dialog */}
            {/* Pass areaId from the station object */}
            {stationForAssignment && ( // Ensure station is selected before rendering
                <KdsCategoryAssignmentDialog
                    station={stationForAssignment}
                    isOpen={isAssignmentDialogOpen}
                    onOpenChange={setIsAssignmentDialogOpen}
                    orgId={orgId}
                    areaId={selectedAreaId!} // Pass the selectedAreaId
                    onAssignmentSuccess={() => {
                        // Optionally refresh data or show a toast
                        toast.success(`Assegnazioni di categoria per ${stationForAssignment.name} aggiornate.`);
                    }}
                />
            )}
        </div> // Close the outer div
    );
};

export default OrgKdsStationsAdminPage;
