'use client';

import React, { useState, useEffect } from 'react'; // Removed useCallback
import apiClient from '@/services/apiClient';
import { useAuth } from '@/contexts/AuthContext';
// import { useParams } from 'next/navigation'; // Removed useParams
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
    Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter, DialogClose // Removed DialogDescription
} from '@/components/ui/dialog';
import {
    AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger
} from "@/components/ui/alert-dialog";
import { Switch } from '@/components/ui/switch';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { Info } from 'lucide-react'; // Import Info icon
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { toast } from 'sonner';
import { PrinterDto, AreaDto, AreaUpsertDto } from '@/types';
import printerService from '@/services/printerService';

// Special value to represent "None" selection for printers
const NONE_PRINTER_VALUE = "__NONE__";

export default function AreasPage() {
    const { user } = useAuth();

    const [areas, setAreas] = useState<AreaDto[]>([]);
    const [printers, setPrinters] = useState<PrinterDto[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [isLoadingPrinters, setIsLoadingPrinters] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // --- Dialog States ---
    const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
    const [newAreaData, setNewAreaData] = useState<Partial<AreaUpsertDto>>({
        name: '',
        enableWaiterConfirmation: false,
        enableKds: false,
        enableCompletionConfirmation: false,
        receiptPrinterId: null,
        printComandasAtCashier: false,
        enableQueueSystem: false,
        guestCharge: 0,
        takeawayCharge: 0,
    });
    const [addError, setAddError] = useState<string | null>(null);

    const [editingArea, setEditingArea] = useState<AreaDto | null>(null);
    const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
    const [editAreaData, setEditAreaData] = useState<Partial<AreaUpsertDto>>({});
    const [editError, setEditError] = useState<string | null>(null);

    const [areaToDelete, setAreaToDelete] = useState<AreaDto | null>(null);
    const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
    const [deleteError, setDeleteError] = useState<string | null>(null);

    // --- Data Fetching ---
    const fetchAreas = async () => {
        setIsLoading(true);
        setError(null);
        try {
            // Assuming /api/Areas returns areas accessible by the logged-in user
            const response = await apiClient.get<AreaDto[]>('/Areas');
            setAreas(response.data);
        } catch (err) {
            console.error('Error fetching areas:', err);
            setError('Caricamento aree fallito.');
            toast.error('Caricamento aree fallito.');
        } finally {
            setIsLoading(false);
        }
    }

    const fetchPrinters = async () => {
        setIsLoadingPrinters(true);
        try {
            const response = await printerService.getPrinters();
            setPrinters(response || []);
        } catch (err) {
            console.error('Error fetching printers:', err);
            setError('Caricamento stampanti fallito.');
            toast.error('Caricamento stampanti fallito.');
        } finally {
            setIsLoadingPrinters(false);
        }
    }

    useEffect(() => {
        if (user) {
            fetchAreas();
            fetchPrinters();
        }
    }, [user]);

    // --- Add Area ---
    const handleOpenAddDialog = () => {
        setNewAreaData({
            name: '',
            enableWaiterConfirmation: false,
            enableKds: false,
            enableCompletionConfirmation: false,
            receiptPrinterId: null,
            printComandasAtCashier: false,
            enableQueueSystem: false,
            guestCharge: 0,
            takeawayCharge: 0,
        });
        setAddError(null);
        setIsAddDialogOpen(true);
    };

    const handleAddArea = async () => {
        if (!user?.organizationId) {
            setAddError("Le informazioni sull'organizzazione dell'utente sono mancanti. Impossibile aggiungere l'area.");
            return;
        }
        if (!newAreaData.name?.trim()) {
            setAddError("Il nome dell'area è obbligatorio.");
            return;
        }
        setAddError(null);

        const dataToSend: AreaUpsertDto = {
            name: newAreaData.name.trim(),
            organizationId: user.organizationId,
            enableWaiterConfirmation: newAreaData.enableWaiterConfirmation || false,
            enableKds: newAreaData.enableKds || false,
            enableCompletionConfirmation: newAreaData.enableCompletionConfirmation || false,
            receiptPrinterId: newAreaData.receiptPrinterId === undefined ? null : newAreaData.receiptPrinterId,
            printComandasAtCashier: newAreaData.printComandasAtCashier || false,
            enableQueueSystem: newAreaData.enableQueueSystem || false,
            guestCharge: newAreaData.guestCharge || 0,
            takeawayCharge: newAreaData.takeawayCharge || 0,
        };

        if (!(dataToSend.organizationId?.length > 0)) {
            setAddError("ID organizzazione non valido.");
            return;
        }

        try {
            await apiClient.post<AreaDto>('/Areas', dataToSend);
            toast.success('Area aggiunta con successo!');
            await fetchAreas();
            setIsAddDialogOpen(false);
        } catch (err: unknown) {
            console.error('Error adding area:', err);
            let errorMessage = 'Aggiunta area fallita.';
            if (typeof err === 'object' && err !== null) {
                const errorResponse = err as { response?: { data?: { title?: string } }, message?: string };
                if (errorResponse.response?.data?.title) {
                    errorMessage = errorResponse.response.data.title;
                } else if (typeof errorResponse.response?.data === 'string') { // Handle plain string response
                    errorMessage = errorResponse.response.data;
                } else if (errorResponse.message) {
                    errorMessage = errorResponse.message;
                }
            }
            setAddError(errorMessage);
            toast.error(errorMessage);
        }
    };

    // --- Edit Area ---
    const handleOpenEditDialog = (area: AreaDto) => {
        setEditingArea(area);
        setEditAreaData({
            name: area.name,
            enableWaiterConfirmation: area.enableWaiterConfirmation,
            enableKds: area.enableKds,
            enableCompletionConfirmation: area.enableCompletionConfirmation,
            receiptPrinterId: area.receiptPrinterId === undefined ? null : area.receiptPrinterId,
            printComandasAtCashier: area.printComandasAtCashier,
            enableQueueSystem: area.enableQueueSystem,
            guestCharge: area.guestCharge,
            takeawayCharge: area.takeawayCharge,
        });
        setEditError(null);
        setIsEditDialogOpen(true);
    };

    const handleEditArea = async () => {
        if (!editingArea || !editAreaData.name?.trim()) {
            setEditError("Il nome dell'area è obbligatorio.");
            return;
        }

        const hasChanged =
            editAreaData.name.trim() !== editingArea.name ||
            editAreaData.enableWaiterConfirmation !== editingArea.enableWaiterConfirmation ||
            editAreaData.enableKds !== editingArea.enableKds ||
            editAreaData.enableCompletionConfirmation !== editingArea.enableCompletionConfirmation ||
            (editAreaData.receiptPrinterId === undefined ? null : editAreaData.receiptPrinterId) !== (editingArea.receiptPrinterId === undefined ? null : editingArea.receiptPrinterId) ||
            editAreaData.printComandasAtCashier !== editingArea.printComandasAtCashier ||
            editAreaData.enableQueueSystem !== editingArea.enableQueueSystem ||
            editAreaData.guestCharge !== editingArea.guestCharge ||
            editAreaData.takeawayCharge !== editingArea.takeawayCharge;

        if (!hasChanged) {
            setEditError("Nessuna modifica rilevata.");
            toast.info("Nessuna modifica rilevata.");
            return;
        }
        setEditError(null);

        const dataToSend: Partial<AreaUpsertDto> = {
            name: editAreaData.name.trim(),
            enableWaiterConfirmation: editAreaData.enableWaiterConfirmation || false,
            enableKds: editAreaData.enableKds || false,
            enableCompletionConfirmation: editAreaData.enableCompletionConfirmation || false,
            receiptPrinterId: editAreaData.receiptPrinterId === undefined ? null : editAreaData.receiptPrinterId,
            printComandasAtCashier: editAreaData.printComandasAtCashier || false,
            enableQueueSystem: editAreaData.enableQueueSystem || false,
            guestCharge: editAreaData.guestCharge || 0,
            takeawayCharge: editAreaData.takeawayCharge || 0,
        };

        try {
            await apiClient.put(`/Areas/${editingArea.id}`, dataToSend);
            toast.success('Area aggiornata con successo!');
            await fetchAreas();
            setIsEditDialogOpen(false);
            setEditingArea(null);
        } catch (err: unknown) {
            console.error('Error updating area:', err);
            let errorMessage = "Aggiornamento dell'area fallito.";
            if (typeof err === 'object' && err !== null) {
                const errorResponse = err as { response?: { data?: { title?: string } }, message?: string };
                if (errorResponse.response?.data?.title) {
                    errorMessage = errorResponse.response.data.title;
                } else if (typeof errorResponse.response?.data === 'string') {
                    errorMessage = errorResponse.response.data;
                } else if (errorResponse.message) {
                    errorMessage = errorResponse.message;
                }
            }
            setEditError(errorMessage);
            toast.error(errorMessage);
        }
    };

    // --- Delete Area ---
    const handleOpenDeleteDialog = (area: AreaDto) => {
        setAreaToDelete(area);
        setDeleteError(null);
        setIsDeleteDialogOpen(true);
        setDeleteError(null);
    };

    const handleDeleteArea = async () => {
        if (!areaToDelete) return;
        setDeleteError(null);

        try {
            await apiClient.delete(`/Areas/${areaToDelete.id}`);
            toast.success(`L'area "${areaToDelete.name}" è stata eliminata con successo.`);
            await fetchAreas();
            setIsDeleteDialogOpen(false);
            setAreaToDelete(null);
        } catch (err: unknown) {
            console.error('Error deleting area:', err);
            let errorMessage = "Eliminazione dell'area fallita.";
            if (typeof err === 'object' && err !== null) {
                const errorResponse = err as { response?: { data?: { title?: string } }, message?: string };
                if (errorResponse.response?.data?.title) {
                    errorMessage = errorResponse.response.data.title;
                } else if (typeof errorResponse.response?.data === 'string') {
                    errorMessage = errorResponse.response.data;
                } else if (errorResponse.message) {
                    errorMessage = errorResponse.message;
                }
            }
            setDeleteError(errorMessage);
            toast.error(errorMessage);
        }
    };

    // --- Render ---
    return (
        <div className="space-y-6">
            <Card>
                <CardHeader className="flex flex-row items-center justify-between">
                    <div>
                        <CardTitle>Gestisci Aree</CardTitle>
                        <CardDescription>Aggiungi, modifica o elimina le aree operative per la tua organizzazione.</CardDescription>
                    </div>
                    {/* Add Area Dialog Trigger */}
                    <Dialog open={isAddDialogOpen} onOpenChange={setIsAddDialogOpen}>
                        <DialogTrigger asChild>
                            <Button size="sm" onClick={handleOpenAddDialog}>Aggiungi Nuova Area</Button>
                        </DialogTrigger>
                        <DialogContent className="sm:max-w-md overflow-y-scroll max-h-screen">
                            <DialogHeader><DialogTitle>Aggiungi Nuova Area</DialogTitle></DialogHeader>
                            <div className="grid gap-4 py-4">
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="add-name" className="text-left">Nome*</Label>
                                    <Input
                                        id="add-name"
                                        value={newAreaData.name || ''}
                                        onChange={(e) => setNewAreaData(prev => ({ ...prev, name: e.target.value }))}
                                        className="col-span-3"
                                        placeholder="e.g., Cucina, Bar, Cassa Principale"
                                    />
                                </div>
                                {/* Added Printer Fields for Add Dialog */}
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="add-receiptPrinterId" className="text-left">Stampante Predefinita</Label>
                                    <Select
                                        value={newAreaData.receiptPrinterId?.toString() || NONE_PRINTER_VALUE}
                                        onValueChange={(value) => setNewAreaData(prev => ({ ...prev, receiptPrinterId: value === NONE_PRINTER_VALUE ? null : parseInt(value) }))}
                                        disabled={isLoadingPrinters}
                                    >
                                        <SelectTrigger className="col-span-3">
                                            <SelectValue placeholder="Seleziona una stampante predefinita (opzionale)" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value={NONE_PRINTER_VALUE}>Nessuna</SelectItem>
                                            {printers.map((printer) => (
                                                <SelectItem key={printer.id} value={printer.id.toString()}>
                                                    {printer.name}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                </div>
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="add-printComandasAtCashier" className="text-left">
                                        <TooltipProvider delayDuration={100}>
                                            <Tooltip>
                                                <TooltipTrigger asChild>
                                                    <span className="cursor-help border-b border-dashed border-gray-400">Stampa Comande su Predefinita</span>
                                                </TooltipTrigger>
                                                <TooltipContent side="right">
                                                    <p className="max-w-xs">Se ATTIVO, tutte le comande per quest'area (non legate a una specifica Postazione Cassa con le proprie impostazioni) verranno stampate sulla Stampante Predefinita selezionata sopra. Se DISATTIVO, le comande verranno stampate in base alle assegnazioni categoria-stampante.</p>
                                                </TooltipContent>
                                            </Tooltip>
                                        </TooltipProvider>
                                    </Label>
                                    <Switch
                                        id="add-printComandasAtCashier"
                                        checked={newAreaData.printComandasAtCashier || false}
                                        onCheckedChange={(checked) => setNewAreaData(prev => ({ ...prev, printComandasAtCashier: checked }))}
                                        className="col-span-3 justify-self-start"
                                    />
                                </div>
                                {addError && (
                                    <div className="py-2 px-3 text-red-500 text-sm bg-red-50 border border-red-200 rounded whitespace-pre-wrap max-h-32 overflow-y-auto">
                                        {addError}
                                    </div>
                                )}
                            </div>
                            <DialogFooter>
                                <DialogClose asChild><Button type="button" variant="outline">Annulla</Button></DialogClose>
                                <Button type="submit" onClick={handleAddArea} disabled={!newAreaData.name?.trim()}>Salva Area</Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>
                </CardHeader>
                <CardContent>
                    {isLoading ? <p>Caricamento aree...</p> : error ? <p className="text-red-500">{error}</p> : areas.length > 0 ? (
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>ID</TableHead>
                                    <TableHead>Nome</TableHead>
                                    <TableHead>
                                        <div className="flex items-center">
                                            Slug
                                            <TooltipProvider delayDuration={100}>
                                                <Tooltip>
                                                    <TooltipTrigger asChild>
                                                        <Info className="h-3 w-3 ml-1.5 text-muted-foreground cursor-help" />
                                                    </TooltipTrigger>
                                                    <TooltipContent side="top">
                                                        <p className="max-w-xs">
                                                            Lo Slug dell'Area viene utilizzato per generare il prefisso per i Numeri Ordine Display.
                                                            <br />
                                                            (es., "cucina-1" potrebbe diventare "CUC-").
                                                        </p>
                                                    </TooltipContent>
                                                </Tooltip>
                                            </TooltipProvider>
                                        </div>
                                    </TableHead>
                                    <TableHead>Stampante Predefinita</TableHead>
                                    <TableHead>Stampa Comande</TableHead>
                                    <TableHead>Conferma Cameriere</TableHead>
                                    <TableHead>Usa KDS</TableHead>
                                    <TableHead>Conferma Ritiro</TableHead>
                                    <TableHead>Sistema Coda</TableHead>
                                    <TableHead>Costo Coperto</TableHead>
                                    <TableHead>Costo Asporto</TableHead>
                                    <TableHead className="text-right">Azioni</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {areas.map((area) => (
                                    <TableRow key={area.id}>
                                        <TableCell>{area.id}</TableCell>
                                        <TableCell className="font-medium">{area.name}</TableCell>
                                        <TableCell>{area.slug}</TableCell>
                                        <TableCell>{printers.find(p => p.id === area.receiptPrinterId)?.name || 'Nessuna'}</TableCell>
                                        <TableCell>{area.printComandasAtCashier ? 'Sì' : 'No'}</TableCell>
                                        <TableCell>{area.enableWaiterConfirmation ? 'Sì' : 'No'}</TableCell>
                                        <TableCell>{area.enableKds ? 'Sì' : 'No'}</TableCell>
                                        <TableCell>{area.enableCompletionConfirmation ? 'Sì' : 'No'}</TableCell>
                                        <TableCell>{area.enableQueueSystem ? 'Sì' : 'No'}</TableCell>
                                        <TableCell>{area.guestCharge?.toFixed(2)}</TableCell>
                                        <TableCell>{area.takeawayCharge?.toFixed(2)}</TableCell>
                                        <TableCell className="text-right space-x-2">
                                            {/* Edit Button Trigger */}
                                            <Button variant="outline" size="sm" onClick={() => handleOpenEditDialog(area)}>Modifica</Button>
                                            {/* Delete Button Trigger */}
                                            <AlertDialog open={isDeleteDialogOpen && areaToDelete?.id === area.id} onOpenChange={(open) => { if (!open) setAreaToDelete(null); setIsDeleteDialogOpen(open); }}>
                                                <AlertDialogTrigger asChild>
                                                    <Button variant="destructive" size="sm" onClick={() => handleOpenDeleteDialog(area)}>Elimina</Button>
                                                </AlertDialogTrigger>
                                                <AlertDialogContent>
                                                    <AlertDialogHeader>
                                                        <AlertDialogTitle>Sei sicuro?</AlertDialogTitle>
                                                        <AlertDialogDescription>
                                                            Eliminare l'area "{areaToDelete?.name}"? Questa azione non può essere annullata e potrebbe influenzare categorie e articoli associati.
                                                        </AlertDialogDescription>
                                                    </AlertDialogHeader>
                                                    {deleteError && <p className="text-red-500 text-sm">{deleteError}</p>}
                                                    <AlertDialogFooter>
                                                        <AlertDialogCancel onClick={() => setAreaToDelete(null)}>Annulla</AlertDialogCancel>
                                                        <AlertDialogAction onClick={handleDeleteArea}>Continua</AlertDialogAction>
                                                    </AlertDialogFooter>
                                                </AlertDialogContent>
                                            </AlertDialog>
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    ) : <p>Nessuna area trovata. Aggiungine una per iniziare.</p>}
                </CardContent>
            </Card>

            {/* Edit Area Dialog */}
            <Dialog open={isEditDialogOpen} onOpenChange={(open) => { if (!open) setEditingArea(null); setIsEditDialogOpen(open); }}>
                <DialogContent className="sm:max-w-lg overflow-y-scroll max-h-screen">
                    <DialogHeader><DialogTitle>Modifica Area: {editingArea?.name}</DialogTitle></DialogHeader>
                    <div className="grid gap-6 py-4">
                        {/* Name Input */}
                        <div className="grid grid-cols-4 items-center gap-4">
                            <Label htmlFor="edit-name" className="text-left">Nome*</Label>
                            <Input
                                id="edit-name"
                                value={editAreaData.name || ''}
                                onChange={(e) => setEditAreaData(prev => ({ ...prev, name: e.target.value }))}
                                className="col-span-3"
                            />
                        </div>

                        {/* Printer Configuration Fields for Edit Dialog */}
                        <div className="grid grid-cols-4 items-center gap-4">
                            <Label htmlFor="edit-receiptPrinterId" className="text-left">Stampante Predefinita</Label>
                            <Select
                                value={editAreaData.receiptPrinterId?.toString() || NONE_PRINTER_VALUE}
                                onValueChange={(value) => setEditAreaData(prev => ({ ...prev, receiptPrinterId: value === NONE_PRINTER_VALUE ? null : parseInt(value) }))}
                                disabled={isLoadingPrinters}
                            >
                                <SelectTrigger className="col-span-3">
                                    <SelectValue placeholder="Seleziona una stampante predefinita (opzionale)" />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value={NONE_PRINTER_VALUE}>Nessuna</SelectItem>
                                    {printers.map((printer) => (
                                        <SelectItem key={printer.id} value={printer.id.toString()}>
                                            {printer.name}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                        <div className="grid grid-cols-4 items-center gap-4">
                            <Label htmlFor="edit-printComandasAtCashier" className="text-left">
                                <TooltipProvider delayDuration={100}>
                                    <Tooltip>
                                        <TooltipTrigger asChild>
                                            <span className="cursor-help border-b border-dashed border-gray-400">Stampa Comande su Predefinita</span>
                                        </TooltipTrigger>
                                        <TooltipContent side="right">
                                            <p className="max-w-xs">Se ATTIVO, tutte le comande per quest'area (non legate a una specifica Postazione Cassa con le proprie impostazioni) verranno stampate sulla Stampante Predefinita selezionata sopra. Se DISATTIVO, le comande verranno stampate in base alle assegnazioni categoria-stampante.</p>
                                        </TooltipContent>
                                    </Tooltip>
                                </TooltipProvider>
                            </Label>
                            <Switch
                                id="edit-printComandasAtCashier"
                                checked={editAreaData.printComandasAtCashier || false}
                                onCheckedChange={(checked) => setEditAreaData(prev => ({ ...prev, printComandasAtCashier: checked }))}
                                className="col-span-3 justify-self-start"
                            />
                        </div>

                        <div className="grid grid-cols-4 items-center gap-4">
                            <Label htmlFor="edit-guestCharge" className="text-left">Costo Coperto</Label>
                            <Input
                                id="edit-guestCharge"
                                type="number"
                                value={editAreaData.guestCharge || 0}
                                onChange={(e) => setEditAreaData(prev => ({ ...prev, guestCharge: parseFloat(e.target.value) || 0 }))}
                                className="col-span-3"
                            />
                        </div>
                        <div className="grid grid-cols-4 items-center gap-4">
                            <Label htmlFor="edit-takeawayCharge" className="text-left">Costo Asporto</Label>
                            <Input
                                id="edit-takeawayCharge"
                                type="number"
                                value={editAreaData.takeawayCharge || 0}
                                onChange={(e) => setEditAreaData(prev => ({ ...prev, takeawayCharge: parseFloat(e.target.value) || 0 }))}
                                className="col-span-3"
                            />
                        </div>

                        {/* Workflow Configuration Switches with Tooltips */}
                        <TooltipProvider delayDuration={100}>
                            {/* Enable Waiter Confirmation */}
                            <div className="grid grid-cols-4 items-center gap-4">
                                <Label htmlFor="edit-waiter" className="text-left">
                                    <Tooltip>
                                        <TooltipTrigger asChild>
                                            <span className="cursor-help border-b border-dashed border-gray-400">Conferma Cameriere</span>
                                        </TooltipTrigger>
                                        <TooltipContent side="right">
                                            <p className="max-w-xs">Se ATTIVO, gli ordini passano a 'Pagato' e richiedono una scansione del Cameriere per diventare 'In Preparazione'. Le comande vengono stampate dopo la scansione. Se DISATTIVO, gli ordini passano direttamente a 'In Preparazione' (o stato successivo) e le comande vengono stampate immediatamente.</p>
                                        </TooltipContent>
                                    </Tooltip>
                                </Label>
                                <Switch
                                    id="edit-waiter"
                                    checked={editAreaData.enableWaiterConfirmation || false}
                                    onCheckedChange={(checked) => setEditAreaData(prev => ({ ...prev, enableWaiterConfirmation: checked }))}
                                    className="col-span-3 justify-self-start" // Align switch to the left
                                />
                            </div>

                            {/* Enable KDS */}
                            <div className="grid grid-cols-4 items-center gap-4">
                                <Label htmlFor="edit-kds" className="text-left">
                                    <Tooltip>
                                        <TooltipTrigger asChild>
                                            <span className="cursor-help border-b border-dashed border-gray-400">Usa KDS</span>
                                        </TooltipTrigger>
                                        <TooltipContent side="right">
                                            <p className="max-w-xs">Se ATTIVO, gli ordini passano da 'In Preparazione' a 'Pronto per il Ritiro' solo dopo che tutti gli articoli sono stati confermati sulle schermate KDS pertinenti. Se DISATTIVO, gli ordini saltano questo passaggio.</p>
                                        </TooltipContent>
                                    </Tooltip>
                                </Label>
                                <Switch
                                    id="edit-kds"
                                    checked={editAreaData.enableKds || false}
                                    onCheckedChange={(checked) => setEditAreaData(prev => ({ ...prev, enableKds: checked }))}
                                    className="col-span-3 justify-self-start"
                                />
                            </div>

                            {/* Enable Completion Confirmation */}
                            <div className="grid grid-cols-4 items-center gap-4">
                                <Label htmlFor="edit-completion" className="text-left">
                                    <Tooltip>
                                        <TooltipTrigger asChild>
                                            <span className="cursor-help border-b border-dashed border-gray-400">Conferma Ritiro</span>
                                        </TooltipTrigger>
                                        <TooltipContent side="right">
                                            <p className="max-w-xs">Se ATTIVO, gli ordini passano da 'Pronto per il Ritiro' a 'Completato' solo dopo un'azione di conferma esplicita (es. scansione/pulsante al punto di ritiro - richiede implementazione UI). Se DISATTIVO, gli ordini si completano automaticamente dopo KDS/preparazione.</p>
                                        </TooltipContent>
                                    </Tooltip>
                                </Label>
                                <Switch
                                    id="edit-completion"
                                    checked={editAreaData.enableCompletionConfirmation || false}
                                    onCheckedChange={(checked) => setEditAreaData(prev => ({ ...prev, enableCompletionConfirmation: checked }))}
                                    className="col-span-3 justify-self-start"
                                />
                            </div>

                            {/* Queue System Toggle */}
                            <div className="grid grid-cols-4 items-center gap-4">
                                <Label htmlFor="edit-queueSystem" className="text-left">
                                    <Tooltip>
                                        <TooltipTrigger asChild>
                                            <span className="cursor-help border-b border-dashed border-gray-400">Sistema Coda</span>
                                        </TooltipTrigger>
                                        <TooltipContent side="right">
                                            <p className="max-w-xs">Se ATTIVO, attiva il sistema di chiamata del numero di coda per quest'area.</p>
                                        </TooltipContent>
                                    </Tooltip>
                                </Label>
                                <Switch
                                    id="edit-queueSystem"
                                    checked={editAreaData.enableQueueSystem || false}
                                    onCheckedChange={(checked) => setEditAreaData(prev => ({ ...prev, enableQueueSystem: checked }))}
                                    className="col-span-3 justify-self-start"
                                />
                            </div>
                        </TooltipProvider>

                        {editError && (
                            <div className="py-2 px-3 text-red-500 text-sm bg-red-50 border border-red-200 rounded whitespace-pre-wrap max-h-32 overflow-y-auto">
                                {editError}
                            </div>
                        )}
                    </div>
                    <DialogFooter>
                        <DialogClose asChild><Button type="button" variant="outline" onClick={() => setEditingArea(null)}>Annulla</Button></DialogClose>
                        <Button type="submit" onClick={handleEditArea} disabled={!editAreaData.name?.trim()}>Salva Modifiche</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
