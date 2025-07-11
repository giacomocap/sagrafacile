'use client';

import React, { useState, useEffect, useCallback } from 'react';
import apiClient from '@/services/apiClient';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
    DialogFooter,
    DialogClose,
    DialogDescription,
} from '@/components/ui/dialog';
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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { toast } from 'sonner'; // Assuming sonner for toasts
import { AreaDto, PrinterDto, CashierStationDto } from '@/types'; // Import shared types
import printerService from '@/services/printerService';

interface CashierStationFormData {
    areaId: string;
    name: string;
    receiptPrinterId: string;
    printComandasAtThisStation: boolean;
    isEnabled: boolean;
}

const initialFormData: CashierStationFormData = {
    areaId: '',
    name: '',
    receiptPrinterId: '',
    printComandasAtThisStation: false,
    isEnabled: true,
};

export default function CashierStationsPage() {
    // const { user: _user } = useAuth(); // Renamed to avoid conflict if 'user' is used elsewhere or to satisfy linter
    const params = useParams();
    const router = useRouter();
    const orgId = params.orgId as string;

    const [areas, setAreas] = useState<AreaDto[]>([]);
    const [printers, setPrinters] = useState<PrinterDto[]>([]);
    const [cashierStations, setCashierStations] = useState<CashierStationDto[]>([]);
    const [filteredStations, setFilteredStations] = useState<CashierStationDto[]>([]);

    const [selectedFilterAreaId, setSelectedFilterAreaId] = useState<string>('all'); // 'all' or areaId

    const [isLoadingAreas, setIsLoadingAreas] = useState(false);
    const [isLoadingPrinters, setIsLoadingPrinters] = useState(false);
    const [isLoadingStations, setIsLoadingStations] = useState(false);
    const [pageError, setPageError] = useState<string | null>(null);

    const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
    const [addFormData, setAddFormData] = useState<CashierStationFormData>(initialFormData);
    const [addError, setAddError] = useState<string | null>(null);

    const [editingStation, setEditingStation] = useState<CashierStationDto | null>(null);
    const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
    const [editFormData, setEditFormData] = useState<CashierStationFormData>(initialFormData);
    const [editError, setEditError] = useState<string | null>(null);

    const [stationToDelete, setStationToDelete] = useState<CashierStationDto | null>(null);
    const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
    const [deleteError, setDeleteError] = useState<string | null>(null);

    const fetchAreas = useCallback(async () => {
        if (!orgId) return;
        setIsLoadingAreas(true);
        try {
            const response = await apiClient.get<AreaDto[]>('/Areas');
            setAreas(response.data || []);
        } catch (err) {
            console.error('Errore nel recupero delle aree:', err);
            setPageError('Caricamento aree fallito.');
            toast.error('Caricamento aree fallito.');
        } finally {
            setIsLoadingAreas(false);
        }
    }, [orgId]);

    const fetchPrinters = useCallback(async () => {
        if (!orgId) return;
        setIsLoadingPrinters(true);
        try {
            // Assuming an endpoint like /api/Printers/organization/{orgId} exists
            const response = await printerService.getPrinters();
            setPrinters(response || []);
        } catch (err) {
            console.error('Errore nel recupero delle stampanti:', err);
            setPageError('Caricamento stampanti fallito.');
            toast.error('Caricamento stampanti fallito.');
        } finally {
            setIsLoadingPrinters(false);
        }
    }, [orgId]);

    const fetchCashierStations = useCallback(async () => {
        if (!orgId) return;
        setIsLoadingStations(true);
        setPageError(null);
        try {
            const response = await apiClient.get<CashierStationDto[]>(`/CashierStations/organization/${orgId}`);
            setCashierStations(response.data || []);
        } catch (err) {
            console.error('Errore nel recupero delle postazioni cassa:', err);
            setPageError('Caricamento postazioni cassa fallito.');
            toast.error('Caricamento postazioni cassa fallito.');
        } finally {
            setIsLoadingStations(false);
        }
    }, [orgId]);

    useEffect(() => {
        fetchAreas();
        fetchPrinters();
        fetchCashierStations();
    }, [fetchAreas, fetchPrinters, fetchCashierStations]);

    useEffect(() => {
        if (selectedFilterAreaId === 'all') {
            setFilteredStations(cashierStations);
        } else {
            setFilteredStations(
                cashierStations.filter(station => station.areaId.toString() === selectedFilterAreaId)
            );
        }
    }, [cashierStations, selectedFilterAreaId]);

    const handleAddFormChange = (field: keyof CashierStationFormData, value: string | boolean) => {
        setAddFormData(prev => ({ ...prev, [field]: value }));
    };

    const handleEditFormChange = (field: keyof CashierStationFormData, value: string | boolean) => {
        setEditFormData(prev => ({ ...prev, [field]: value }));
    };

    const handleOpenAddDialog = () => {
        setAddFormData(initialFormData);
        setAddError(null);
        if (areas.length > 0) { // Pre-select first area if available
            setAddFormData(prev => ({ ...prev, areaId: areas[0].id.toString() }))
        }
        setIsAddDialogOpen(true);
    };

    const handleAddStation = async () => {
        if (!addFormData.name.trim()) {
            setAddError("Il nome della postazione non può essere vuoto.");
            return;
        }
        if (!addFormData.areaId) {
            setAddError("Selezionare un'area.");
            return;
        }
        // Check if receipt printer is required based on backend validation
        if (!addFormData.receiptPrinterId) {
            setAddError("Selezionare una stampante per le ricevute. Questo campo è obbligatorio.");
            return;
        }
        setAddError(null);

        try {
            // Convert string IDs to numbers in the payload
            const payload = {
                name: addFormData.name.trim(),
                areaId: parseInt(addFormData.areaId, 10),
                receiptPrinterId: parseInt(addFormData.receiptPrinterId, 10), // Always a number now
                printComandasAtThisStation: addFormData.printComandasAtThisStation,
                isEnabled: addFormData.isEnabled
            };

            await apiClient.post(`/CashierStations/organization/${orgId}`, payload);
            toast.success("Postazione cassa aggiunta con successo.");
            setIsAddDialogOpen(false);
            fetchCashierStations(); // Refresh list
        } catch (err: unknown) { // Changed to unknown
            console.error('Errore nell\'aggiunta della postazione cassa:', err);
            const errorResponse = (err as { response?: { data?: { title?: string, errors?: Record<string, string[]> } } }).response?.data;
            let errorMsg = errorResponse?.title || 'Aggiunta postazione cassa fallita.';

            if (errorResponse?.errors) {
                const fieldErrors = Object.entries(errorResponse.errors)
                    .map(([field, messages]) => `${field}: ${messages.join(', ')}`)
                    .join('\n');

                if (fieldErrors) {
                    errorMsg = `${errorMsg}\n${fieldErrors}`;
                }
            }

            setAddError(errorMsg);
            toast.error(errorMsg);
        }
    };

    const handleOpenEditDialog = (station: CashierStationDto) => {
        setEditingStation(station);
        setEditFormData({
            name: station.name,
            areaId: station.areaId.toString(),
            receiptPrinterId: station.receiptPrinterId?.toString() || '',
            printComandasAtThisStation: station.printComandasAtThisStation,
            isEnabled: station.isEnabled,
        });
        setEditError(null);
        setIsEditDialogOpen(true);
    };

    const handleEditStation = async () => {
        if (!editingStation) return;
        if (!editFormData.name.trim()) {
            setEditError("Il nome della postazione non può essere vuoto.");
            return;
        }
        if (!editFormData.areaId) {
            setEditError("Selezionare un'area.");
            return;
        }
        // Check if receipt printer is required based on backend validation
        if (!editFormData.receiptPrinterId) {
            setEditError("Selezionare una stampante per le ricevute. Questo campo è obbligatorio.");
            return;
        }
        setEditError(null);

        try {
            // Convert string IDs to numbers in the payload
            const payload = {
                name: editFormData.name.trim(),
                areaId: parseInt(editFormData.areaId, 10),
                receiptPrinterId: parseInt(editFormData.receiptPrinterId, 10), // Always a number now
                printComandasAtThisStation: editFormData.printComandasAtThisStation,
                isEnabled: editFormData.isEnabled
            };

            await apiClient.put(`/CashierStations/${editingStation.id}`, payload);
            toast.success("Postazione cassa aggiornata con successo.");
            setIsEditDialogOpen(false);
            setEditingStation(null);
            fetchCashierStations(); // Refresh list
        } catch (err: unknown) {
            console.error('Errore nell\'aggiornamento della postazione cassa:', err);
            const errorResponse = (err as { response?: { data?: { title?: string, errors?: Record<string, string[]> } } }).response?.data;
            let errorMsg = errorResponse?.title || 'Aggiornamento postazione cassa fallito.';

            if (errorResponse?.errors) {
                const fieldErrors = Object.entries(errorResponse.errors)
                    .map(([field, messages]) => `${field}: ${messages.join(', ')}`)
                    .join('\n');

                if (fieldErrors) {
                    errorMsg = `${errorMsg}\n${fieldErrors}`;
                }
            }

            setEditError(errorMsg);
            toast.error(errorMsg);
        }
    };

    const handleOpenDeleteDialog = (station: CashierStationDto) => {
        setStationToDelete(station);
        setDeleteError(null);
        setIsDeleteDialogOpen(true);
    };

    const handleDeleteStation = async () => {
        if (!stationToDelete) return;
        setDeleteError(null);
        try {
            await apiClient.delete(`/CashierStations/${stationToDelete.id}`);
            toast.success("Postazione cassa eliminata con successo.");
            setIsDeleteDialogOpen(false);
            setStationToDelete(null);
            fetchCashierStations(); // Refresh list
        } catch (err) {
            console.error('Errore nell\'eliminazione della postazione cassa:', err);
            setDeleteError("Eliminazione della postazione cassa fallita. Potrebbe essere ancora associata a degli ordini.");
            toast.error("Eliminazione della postazione cassa fallita.");
        }
    };

    const renderFormFields = (formData: CashierStationFormData, handleChange: (field: keyof CashierStationFormData, value: string | boolean) => void) => (
        <div className="grid gap-4 py-4">
            <div className="grid grid-cols-4 items-center gap-4">
                <Label htmlFor="name" className="text-right">
                    Nome
                </Label>
                <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => handleChange('name', e.target.value)}
                    className="col-span-3"
                    placeholder="Es. Cassa 1, Cassa Bar"
                />
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
                <Label htmlFor="areaId" className="text-right">
                    Area
                </Label>
                <Select
                    value={formData.areaId}
                    onValueChange={(value) => handleChange('areaId', value)}
                >
                    <SelectTrigger className="col-span-3">
                        <SelectValue placeholder="Seleziona un'area" />
                    </SelectTrigger>
                    <SelectContent>
                        {areas.map(area => (
                            <SelectItem key={area.id} value={area.id.toString()}>
                                {area.name}
                            </SelectItem>
                        ))}
                    </SelectContent>
                </Select>
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
                <Label htmlFor="receiptPrinterId" className="text-right">
                    Stampante Scontrini
                </Label>
                <Select
                    value={formData.receiptPrinterId}
                    onValueChange={(value) => handleChange('receiptPrinterId', value)}
                    disabled={isLoadingPrinters}
                >
                    <SelectTrigger className="col-span-3">
                        <SelectValue placeholder="Seleziona una stampante per le ricevute" />
                    </SelectTrigger>
                    <SelectContent>
                        {printers.map(printer => (
                            <SelectItem key={printer.id} value={printer.id.toString()}>
                                {printer.name}
                            </SelectItem>
                        ))}
                    </SelectContent>
                </Select>
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
                <Label htmlFor="printComandasAtThisStation" className="text-right">
                    Stampa Comande Qui
                </Label>
                <div className="col-span-3 flex items-center">
                    <Switch
                        id="printComandasAtThisStation"
                        checked={formData.printComandasAtThisStation}
                        onCheckedChange={(checked) => handleChange('printComandasAtThisStation', checked)}
                    />
                    <span className="ml-2 text-sm text-muted-foreground">
                        Stampa una copia di tutte le comande su questa stampante.
                    </span>
                </div>
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
                <Label htmlFor="isEnabled" className="text-right">
                    Abilitata
                </Label>
                <div className="col-span-3 flex items-center">
                    <Switch
                        id="isEnabled"
                        checked={formData.isEnabled}
                        onCheckedChange={(checked) => handleChange('isEnabled', checked)}
                    />
                    <span className="ml-2 text-sm text-muted-foreground">
                        La postazione cassa è attiva e utilizzabile.
                    </span>
                </div>
            </div>
        </div>
    );

    if (!orgId) {
        return <p>ID Organizzazione non trovato.</p>;
    }

    if (isLoadingAreas || isLoadingPrinters && !cashierStations.length) { // Show loading only on initial data fetch phase
        return <p>Caricamento dati di configurazione...</p>;
    }

    return (
        <div className="space-y-6">
            <h1 className="text-2xl font-bold">Gestisci Postazioni Cassa</h1>

            <Card>
                <CardHeader>
                    <CardTitle>Filtra Postazioni</CardTitle>
                    <CardDescription>Visualizza le postazioni cassa per un'area specifica o per tutte le aree.</CardDescription>
                </CardHeader>
                <CardContent>
                    <Select onValueChange={setSelectedFilterAreaId} value={selectedFilterAreaId}>
                        <SelectTrigger className="w-[280px]">
                            <SelectValue placeholder="Seleziona un'area per filtrare" />
                        </SelectTrigger>
                        <SelectContent>
                            <SelectItem value="all">Tutte le Aree</SelectItem>
                            {areas.map((area) => (
                                <SelectItem key={area.id} value={area.id.toString()}>
                                    {area.name}
                                </SelectItem>
                            ))}
                        </SelectContent>
                    </Select>
                </CardContent>
            </Card>

            <Card>
                <CardHeader className="flex flex-row items-center justify-between">
                    <div>
                        <CardTitle>Elenco Postazioni Cassa</CardTitle>
                        <CardDescription>Visualizza e gestisci tutte le postazioni cassa nella tua organizzazione.</CardDescription>
                    </div>
                    <div className="flex flex-col items-end gap-2">
                        <Dialog open={isAddDialogOpen} onOpenChange={setIsAddDialogOpen}>
                            <DialogTrigger asChild>
                                <Button size="sm" onClick={handleOpenAddDialog} disabled={areas.length === 0 || printers.length === 0 && !isLoadingAreas && !isLoadingPrinters}>
                                    Aggiungi Nuova Postazione
                                </Button>
                            </DialogTrigger>
                            <DialogContent className="sm:max-w-md overflow-y-scroll max-h-screen">
                                <DialogHeader>
                                    <DialogTitle>Aggiungi Nuova Postazione Cassa</DialogTitle>
                                    <DialogDescription>Configura i dettagli per la nuova postazione.</DialogDescription>
                                </DialogHeader>
                                {renderFormFields(addFormData, handleAddFormChange)}
                                {addError && (
                                    <div className="py-2 px-3 text-red-500 text-sm bg-red-50 border border-red-200 rounded whitespace-pre-wrap max-h-32 overflow-y-auto">
                                        {addError}
                                    </div>
                                )}
                                <DialogFooter>
                                    <DialogClose asChild><Button type="button" variant="outline">Annulla</Button></DialogClose>
                                    <Button type="submit" onClick={handleAddStation} disabled={!addFormData.name.trim() || !addFormData.areaId}>Salva Postazione</Button>
                                </DialogFooter>
                            </DialogContent>
                        </Dialog>
                    </div>
                </CardHeader>
                <CardContent>
                    {isLoadingStations ? (
                        <p>Caricamento postazioni...</p>
                    ) : pageError && !filteredStations.length ? (
                        <p className="text-red-500">{pageError}</p>
                    ) : filteredStations.length > 0 ? (
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Nome</TableHead>
                                    <TableHead>Area</TableHead>
                                    <TableHead>Stampante Scontrini</TableHead>
                                    <TableHead>Stampa Comande</TableHead>
                                    <TableHead>Abilitata</TableHead>
                                    <TableHead className="text-right">Azioni</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {filteredStations.map((station) => (
                                    <TableRow key={station.id}>
                                        <TableCell className="font-medium">{station.name}</TableCell>
                                        <TableCell>{station.areaName || areas.find(a => a.id === station.areaId)?.name || 'N/D'}</TableCell>
                                        <TableCell>{station.receiptPrinterName || printers.find(p => p.id === station.receiptPrinterId)?.name || 'Nessuna'}</TableCell>
                                        <TableCell>{station.printComandasAtThisStation ? 'Sì' : 'No'}</TableCell>
                                        <TableCell>{station.isEnabled ? 'Sì' : 'No'}</TableCell>
                                        <TableCell className="text-right space-x-2">
                                            <Button variant="outline" size="sm" onClick={() => handleOpenEditDialog(station)} disabled={areas.length === 0 || printers.length === 0 && !isLoadingAreas && !isLoadingPrinters}>
                                                Modifica
                                            </Button>
                                            <AlertDialog open={isDeleteDialogOpen && stationToDelete?.id === station.id} onOpenChange={(open) => { if (!open) setStationToDelete(null); setIsDeleteDialogOpen(open); }}>
                                                <AlertDialogTrigger asChild>
                                                    <Button variant="destructive" size="sm" onClick={() => handleOpenDeleteDialog(station)}>
                                                        Elimina
                                                    </Button>
                                                </AlertDialogTrigger>
                                                <AlertDialogContent>
                                                    <AlertDialogHeader>
                                                        <AlertDialogTitle>Sei assolutamente sicuro?</AlertDialogTitle>
                                                        <AlertDialogDescription>
                                                            Questa azione non può essere annullata. Questo eliminerà permanentemente la postazione cassa "{stationToDelete?.name}".
                                                        </AlertDialogDescription>
                                                    </AlertDialogHeader>
                                                    {deleteError && <p className="text-red-500 text-sm">{deleteError}</p>}
                                                    <AlertDialogFooter>
                                                        <AlertDialogCancel onClick={() => { setStationToDelete(null); setDeleteError(null); }}>Annulla</AlertDialogCancel>
                                                        <AlertDialogAction onClick={handleDeleteStation}>Continua</AlertDialogAction>
                                                    </AlertDialogFooter>
                                                </AlertDialogContent>
                                            </AlertDialog>
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    ) : (
                        <div className="text-center py-8">
                            <p className="text-muted-foreground mb-4">
                                Nessuna postazione cassa trovata{selectedFilterAreaId !== 'all' ? ' per l\'area selezionata' : ' per questa organizzazione'}.
                            </p>
                            {(areas.length === 0 || printers.length === 0) && !isLoadingAreas && !isLoadingPrinters && (
                                <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4 text-sm">
                                    <p className="font-medium text-yellow-800 mb-2">Configurazione richiesta</p>
                                    <p className="text-yellow-700 mb-3">
                                        {areas.length === 0 && printers.length === 0 ? (
                                            <>Prima di poter creare postazioni cassa, devi configurare almeno un'<strong>area</strong> e una <strong>stampante</strong>.</>
                                        ) : areas.length === 0 ? (
                                            <>Prima di poter creare postazioni cassa, devi configurare almeno un'<strong>area</strong>.</>
                                        ) : (
                                            <>Prima di poter creare postazioni cassa, devi configurare almeno una <strong>stampante</strong>.</>
                                        )}
                                    </p>
                                    <div className="flex gap-2 justify-center">
                                        {areas.length === 0 && (
                                            <Button variant="outline" size="sm" onClick={() => router.push(`/app/org/${orgId}/admin/areas`)}>
                                                Configura Aree
                                            </Button>
                                        )}
                                        {printers.length === 0 && (
                                            <Button variant="outline" size="sm" onClick={() => router.push(`/app/org/${orgId}/admin/printers`)}>
                                                Configura Stampanti
                                            </Button>
                                        )}
                                    </div>
                                </div>
                            )}
                            {pageError && <p className="text-red-500 mt-2">{pageError}</p>}
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* Edit Dialog */}
            <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
                <DialogContent className="sm:max-w-md overflow-y-scroll max-h-screen">
                    <DialogHeader>
                        <DialogTitle>Modifica Postazione Cassa</DialogTitle>
                        <DialogDescription>Aggiorna i dettagli per "{editingStation?.name}".</DialogDescription>
                    </DialogHeader>
                    {editingStation && renderFormFields(editFormData, handleEditFormChange)}
                    {editError && (
                        <div className="py-2 px-3 text-red-500 text-sm bg-red-50 border border-red-200 rounded whitespace-pre-wrap max-h-32 overflow-y-auto">
                            {editError}
                        </div>
                    )}
                    <DialogFooter>
                        <DialogClose asChild><Button type="button" variant="outline">Annulla</Button></DialogClose>
                        <Button type="submit" onClick={handleEditStation} disabled={!editFormData.name.trim() || !editFormData.areaId}>Salva Modifiche</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
