'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { PrinterDto, PrinterType } from '@/types';
import printerService from '@/services/printerService';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { MoreHorizontal, CheckCircle, XCircle, Network, Usb } from 'lucide-react';
import PrinterFormDialog from '@/components/admin/PrinterFormDialog';
import { toast } from 'sonner';

export default function PrintersPage() {
  const { user } = useAuth();
  const [printers, setPrinters] = useState<PrinterDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // --- Stati dei Dialog ---
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingPrinter, setEditingPrinter] = useState<PrinterDto | null>(null);

  const [printerToDelete, setPrinterToDelete] = useState<PrinterDto | null>(null);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  // --- Recupero Dati ---
  const fetchPrinters = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await printerService.getPrinters();
      setPrinters(response);
    } catch (err) {
      console.error('Errore nel recupero delle stampanti:', err);
      setError('Caricamento stampanti fallito.');
      toast.error("Errore", { description: 'Caricamento stampanti fallito.' });
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (user) { // Recupera solo quando il contesto utente è disponibile
        fetchPrinters();
    }
  }, [user, fetchPrinters]);

  // --- Gestori Eventi ---
  const handleOpenAddDialog = () => {
    setEditingPrinter(null); // Assicura di essere in modalità aggiunta
    setIsFormOpen(true);
  };

  const handleOpenEditDialog = (printer: PrinterDto) => {
    setEditingPrinter(printer);
    setIsFormOpen(true);
  };

  const handleSaveSuccess = () => {
    fetchPrinters(); // Ricarica la lista dopo il salvataggio
    setIsFormOpen(false); // Chiude il dialog
  };

  const handleOpenDeleteDialog = (printer: PrinterDto) => {
    setPrinterToDelete(printer);
    setDeleteError(null);
    setIsDeleteDialogOpen(true);
  };

  const handleDeletePrinter = async () => {
    if (!printerToDelete) return;
    setDeleteError(null);

    try {
        await printerService.deletePrinter(printerToDelete.id);
        toast.success("Stampante eliminata con successo.");
        await fetchPrinters(); // Ricarica la lista
        setIsDeleteDialogOpen(false);
        setPrinterToDelete(null);
    } catch (err: unknown) {
        console.error('Errore nell\'eliminazione della stampante:', err);
        const errorResponse = (err as { response?: { data?: { title?: string, message?: string } } }).response?.data;
        const errorMessage = errorResponse?.title
                            || errorResponse?.message
                            || 'Eliminazione stampante fallita. Potrebbe essere assegnata come Stampante Scontrini in un\'Area.';
        setDeleteError(errorMessage);
        toast.error("Errore eliminazione stampante", { description: errorMessage });
    }
  };

  // --- Funzione di Render Ausiliaria ---
  const renderPrinterType = (type: PrinterType) => {
    switch (type) {
      case PrinterType.Network:
        return <Badge variant="outline"><Network className="mr-1 h-3 w-3" /> Rete</Badge>;
      case PrinterType.WindowsUsb:
        return <Badge variant="secondary"><Usb className="mr-1 h-3 w-3" /> Windows USB</Badge>;
      default:
        return <Badge variant="destructive">Sconosciuto</Badge>;
    }
  };

  // --- Render ---
  return (
    <div className="space-y-6">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Gestione Stampanti</CardTitle>
            <CardDescription>
              Aggiungi, modifica o elimina stampanti per la stampa di scontrini e comande.
            </CardDescription>
          </div>
          <Button size="sm" onClick={handleOpenAddDialog}>Aggiungi Nuova Stampante</Button>
        </CardHeader>
        <CardContent>
          {isLoading ? <p className="text-center py-4">Caricamento stampanti...</p> : error ? <p className="text-red-500 text-center py-4">{error}</p> : printers.length > 0 ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>ID</TableHead>
                  <TableHead>Nome</TableHead>
                  <TableHead>Tipo</TableHead>
                  <TableHead>Connessione</TableHead>
                  <TableHead>Nome Windows</TableHead>
                  <TableHead>Abilitata</TableHead>
                  <TableHead className="text-right">Azioni</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {printers.map((printer) => (
                  <TableRow key={printer.id}>
                    <TableCell className="font-medium">{printer.id}</TableCell>
                    <TableCell>{printer.name}</TableCell>
                    <TableCell>{renderPrinterType(printer.type)}</TableCell>
                    <TableCell className="max-w-[200px] truncate" title={printer.connectionString}>{printer.connectionString}</TableCell>
                    <TableCell>{printer.windowsPrinterName || '-'}</TableCell>
                    <TableCell>
                      {printer.isEnabled ? (
                        <CheckCircle className="h-5 w-5 text-green-500" />
                      ) : (
                        <XCircle className="h-5 w-5 text-red-500" />
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                        <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                                <Button variant="ghost" className="h-8 w-8 p-0">
                                <span className="sr-only">Apri menu</span>
                                <MoreHorizontal className="h-4 w-4" />
                                </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                                <DropdownMenuLabel>Azioni</DropdownMenuLabel>
                                <DropdownMenuItem onClick={() => handleOpenEditDialog(printer)}>
                                Modifica
                                </DropdownMenuItem>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                    className="text-red-600 focus:text-red-700 focus:bg-red-50"
                                    onClick={() => handleOpenDeleteDialog(printer)}
                                >
                                Elimina
                                </DropdownMenuItem>
                            </DropdownMenuContent>
                        </DropdownMenu>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : (
            <p className="text-center text-muted-foreground py-4">Nessuna stampante ancora configurata.</p>
          )}
        </CardContent>
      </Card>

      {/* Dialog Aggiungi/Modifica */}
      <PrinterFormDialog
        isOpen={isFormOpen}
        onOpenChange={setIsFormOpen}
        printerToEdit={editingPrinter}
        onSaveSuccess={handleSaveSuccess}
        orgId={+(user?.organizationId || 0)}
      />

      {/* Dialog Conferma Eliminazione */}
        <AlertDialog open={isDeleteDialogOpen} onOpenChange={setIsDeleteDialogOpen}>
            <AlertDialogContent>
                <AlertDialogHeader>
                <AlertDialogTitle>Sei assolutamente sicuro?</AlertDialogTitle>
                <AlertDialogDescription>
                    Questa azione non può essere annullata. Questo eliminerà permanentemente la stampante
                    <span className="font-semibold"> {printerToDelete?.name}</span>.
                    {deleteError && <p className="text-red-500 text-sm mt-2">{deleteError}</p>}
                </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                <AlertDialogCancel onClick={() => setPrinterToDelete(null)}>Annulla</AlertDialogCancel>
                <AlertDialogAction
                    className="bg-red-600 hover:bg-red-700 text-white"
                    onClick={handleDeletePrinter}
                >
                    Elimina Stampante
                </AlertDialogAction>
                </AlertDialogFooter>
            </AlertDialogContent>
        </AlertDialog>
    </div>
  );
}
