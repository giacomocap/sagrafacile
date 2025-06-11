'use client';

import React, { useState, useEffect } from 'react'; // Import useState, useEffect
import { toast } from 'sonner';
import {
    DialogHeader,
    DialogTitle,
    DialogDescription,
    DialogFooter,
} from "@/components/ui/dialog";
import { ResponsiveDialog } from "./shared/ResponsiveDialog";
import { Button } from "@/components/ui/button";
import { ScrollArea } from "@/components/ui/scroll-area";
import { OrderDto, MenuCategoryDto, MenuItemDto, PrinterDto, ReprintType } from '@/types'; // Assuming OrderDto is defined in types, Added MenuCategoryDto, MenuItemDto, PrinterDto
import { Printer, X, Loader2, ShoppingBag } from 'lucide-react';
import Image from 'next/image'; // Added Image import
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from "@/components/ui/alert-dialog"; // Added AlertDialog imports
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "@/components/ui/select"; // Import Select components

interface ReceiptDialogProps {
    order: OrderDto | null; // This will initially be the preview order
    isOpen: boolean;
    onClose: (success?: boolean) => void;
    onSubmitOrder: () => Promise<OrderDto | null>; // Expecting the final OrderDto or null on failure
    menuItems?: (MenuItemDto & { categoryName?: string })[]; // Made optional for reprint scenario
    menuCategories?: MenuCategoryDto[]; // Made optional for reprint scenario
    isReprintMode?: boolean; // Added to differentiate behavior
    onInitiateReprint?: (orderId: string, reprintJobType: ReprintType, printerId?: number) => Promise<{ success: boolean; message?: string }>; // Added for reprint, printerId is optional
    availablePrinters?: PrinterDto[]; // Optional: For admin reprint to select a printer
    orgId?: string; // Optional: Needed if we were to fetch printers here, but passed for consistency for now
}


const ReceiptDialog: React.FC<ReceiptDialogProps> = ({
    order: initialOrder,
    isOpen,
    onClose,
    onSubmitOrder,
    menuItems,
    menuCategories,
    isReprintMode = false,
    onInitiateReprint, // Added prop
    availablePrinters,
}) => {
    // const { connectionStatus, sendPrintCommand } = usePrinterWebSocket(); 
    const [isProcessing, setIsProcessing] = useState(false);
    const [finalOrderData, setFinalOrderData] = useState<OrderDto | null>(null);
    const [showReprintChoiceDialog, setShowReprintChoiceDialog] = useState(false); // State for reprint choice
    const [selectedPrinterId, setSelectedPrinterId] = useState<number | undefined>(undefined); // For admin printer selection

    const displayOrder = finalOrderData || initialOrder;

    // Reset selected printer when dialog opens/closes or availablePrinters change, if in admin reprint mode
    useEffect(() => {
        if (isOpen && isReprintMode && availablePrinters && availablePrinters.length > 0) {
            // Optionally, set a default printer if desired, e.g., the first one
            // setSelectedPrinterId(availablePrinters[0].id);
            setSelectedPrinterId(undefined); // Or force selection
        } else if (!isOpen) {
            setSelectedPrinterId(undefined);
        }
    }, [isOpen, isReprintMode, availablePrinters]);

    const handleActualReprint = async (reprintType: ReprintType) => {
        if (!displayOrder || !onInitiateReprint || isProcessing) return;

        // For admin reprint, ensure a printer is selected if printers are available
        if (isReprintMode && availablePrinters && availablePrinters.length > 0 && !selectedPrinterId) {
            toast.error("Seleziona una stampante per la ristampa.");
            return;
        }

        setShowReprintChoiceDialog(false); // Close choice dialog
        setIsProcessing(true);

        // Pass selectedPrinterId to onInitiateReprint
        const result = await onInitiateReprint(displayOrder.id, reprintType, selectedPrinterId);

        if (result.success) {
            toast.success(result.message || "Ristampa avviata con successo!");
            onClose(true); // Close main dialog on success
        } else {
            toast.error(result.message || "Errore durante la ristampa.");
            // Keep main dialog open on error, reset processing for another attempt if desired
        }
        // isProcessing will be reset by useEffect when main dialog closes or by explicit set if staying open
        setIsProcessing(false);
    };

    const handleOpenReprintChoices = () => {
        if (isReprintMode && availablePrinters && availablePrinters.length > 0 && !selectedPrinterId) {
            toast.error("Prima seleziona una stampante.");
            return;
        }
        setShowReprintChoiceDialog(true);
    };

    const handlePrintAction = async () => {
        if (!displayOrder || isProcessing) return;

        if (isReprintMode) {
            if (!onInitiateReprint) {
                toast.error("Funzione di ristampa non configurata correttamente.");
                return;
            }
            // If printers are available (admin mode), printer selection is handled before this.
            // Directly open reprint choices. If printer selection was needed, it's done via selectedPrinterId state.
            handleOpenReprintChoices();
            return;
        }

        // --- New Order Submission Logic (no client-side printing) ---
        setIsProcessing(true);
        const submittedOrder = await onSubmitOrder();
        let orderSuccessfullyProcessed = false;

        if (!submittedOrder) {
            toast.warning("Invio ordine fallito.");
        } else {
            setFinalOrderData(submittedOrder);
            toast.info("Ordine inviato con successo! La stampa sarà gestita dal server.");
            orderSuccessfullyProcessed = true;
        }

        if (orderSuccessfullyProcessed) {
            onClose(true);
        } else {
            setIsProcessing(false);
        }
    };

    // Reset finalOrderData when the dialog is closed or the initialOrder changes
    // This ensures that if the dialog is reopened, it shows the new preview data first.
    React.useEffect(() => {
        if (!isOpen) {
            setFinalOrderData(null);
            setIsProcessing(false); // Also reset processing state on close
            setSelectedPrinterId(undefined); // Reset selected printer on close
        }
    }, [isOpen]);


    if (!displayOrder) return null; // Use displayOrder which falls back to initialOrder

    // Format date and time nicely using displayOrder
    const formattedDateTime = new Date(displayOrder.orderDateTime).toLocaleString('it-IT', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
    });

    const itemsSubtotal = displayOrder.items.reduce((acc, item) => acc + item.quantity * item.unitPrice, 0);

    return (
        <ResponsiveDialog
            isOpen={isOpen}
            onOpenChange={onClose}
            className="max-w-lg p-0 print:max-w-full print:shadow-none print:border-none w-full"
        >
            <div className="flex flex-col h-full">
                <DialogHeader className="text-center p-6 pb-4 print:p-0 print:mb-2 flex-shrink-0">
                    {/* Display the ID from displayOrder (will update from PREVIEW to actual ID) */}
                    <DialogTitle className="text-xl font-bold print:text-lg">Ordine #{displayOrder.displayOrderNumber ?? displayOrder.id}</DialogTitle>
                    {displayOrder.isTakeaway && (
                        <p className="text-lg font-semibold text-orange-600 flex items-center justify-center print:text-base">
                            <ShoppingBag className="mr-2 h-5 w-5 print:h-4 print:w-4" /> ASPORTO / TAKEAWAY
                        </p>
                    )}
                    <DialogDescription className="print:text-xs">
                        {formattedDateTime} - Cassa: {displayOrder.cashierName} <br />
                        Area: {displayOrder.areaName}
                        {displayOrder.customerName && ` - Cliente: ${displayOrder.customerName}`}
                        {!displayOrder.isTakeaway && displayOrder.numberOfGuests > 0 && ` - Coperti: ${displayOrder.numberOfGuests}`}
                    </DialogDescription>
                </DialogHeader>

                <ScrollArea className="flex-1 min-h-0 px-6 print:px-0">
                    <div className="border-t border-b py-4 my-4 print:border-none print:my-2 print:py-1">
                        {/* Only group by category if menuItems and menuCategories are available */}
                        {menuItems && menuCategories && menuCategories.length > 0 ? (
                            <>
                                <table className="w-full text-sm print:text-xs">
                                    {/* Items will be grouped by category below */}
                                </table>
                                {
                                    menuCategories
                                        .filter(cat =>
                                            displayOrder.items.some(item =>
                                                menuItems.find(mi => mi.id === item.menuItemId)?.menuCategoryId === cat.id
                                            )
                                        )
                                        .map(category => (
                                            <div key={category.id} className="mb-2 print:mb-1">
                                                <h4 className="font-semibold text-muted-foreground border-b mt-2 mb-1 pb-0.5 print:text-xs print:mt-1 print:mb-0.5">{category.name}</h4>
                                                <table className="w-full text-sm print:text-xs">
                                                    <thead>
                                                        {/* Optional: Repeat headers per category if desired, or keep global ones */}
                                                        {/* For simplicity, let's assume global headers are fine and we just list items here */}
                                                    </thead>
                                                    <tbody>
                                                        {displayOrder.items
                                                            .filter(orderItem => menuItems.find(mi => mi.id === orderItem.menuItemId)?.menuCategoryId === category.id)
                                                            .map((item, index) => (
                                                                <React.Fragment key={`${item.menuItemId}-${index}-${category.id}`}>
                                                                    <tr>
                                                                        <td className="pt-1 w-2/4 print:w-auto">{item.menuItemName}</td>
                                                                        <td className="text-center pt-1 w-1/6 print:w-auto">{item.quantity}</td>
                                                                        <td className="text-right pt-1 w-1/6 print:w-auto">€{item.unitPrice.toFixed(2)}</td>
                                                                        <td className="text-right pt-1 w-1/6 print:w-auto">€{(item.quantity * item.unitPrice).toFixed(2)}</td>
                                                                    </tr>
                                                                    {item.note && (
                                                                        <tr>
                                                                            <td colSpan={4} className="text-xs italic text-muted-foreground pl-2 pb-1 print:text-xxs">
                                                                                &nbsp;&nbsp;↳ {item.note}
                                                                            </td>
                                                                        </tr>
                                                                    )}
                                                                </React.Fragment>
                                                            ))}
                                                    </tbody>
                                                </table>
                                            </div>
                                        ))
                                }
                                {/* Fallback if no categories matched (unlikely with the filter above) */}
                                {menuCategories.filter(cat => displayOrder.items.some(item => menuItems.find(mi => mi.id === item.menuItemId)?.menuCategoryId === cat.id)).length === 0 && (
                                    <table className="w-full text-sm print:text-xs">
                                        <thead>
                                            <tr className="border-b">
                                                <th className="text-left font-semibold pb-1">Prodotto</th>
                                                <th className="text-center font-semibold pb-1">Qtà</th>
                                                <th className="text-right font-semibold pb-1">Prezzo</th>
                                                <th className="text-right font-semibold pb-1">Tot.</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {displayOrder.items.map((item, index) => (
                                                <React.Fragment key={`${item.menuItemId}-${index}-fallback1`}>
                                                    <tr>
                                                        <td className="pt-1">{item.menuItemName}</td>
                                                        <td className="text-center pt-1">{item.quantity}</td>
                                                        <td className="text-right pt-1">€{item.unitPrice.toFixed(2)}</td>
                                                        <td className="text-right pt-1">€{(item.quantity * item.unitPrice).toFixed(2)}</td>
                                                    </tr>
                                                    {item.note && (
                                                        <tr>
                                                            <td colSpan={4} className="text-xs italic text-muted-foreground pl-2 pb-1">
                                                                &nbsp;&nbsp;↳ {item.note}
                                                            </td>
                                                        </tr>
                                                    )}
                                                </React.Fragment>
                                            ))}
                                        </tbody>
                                    </table>
                                )}
                            </>
                        ) : (
                            // Fallback to normal table for reprint or if menu data isn't available
                            <table className="w-full text-sm print:text-xs">
                                <thead>
                                    <tr className="border-b">
                                        <th className="text-left font-semibold pb-1">Prodotto</th>
                                        <th className="text-center font-semibold pb-1">Qtà</th>
                                        <th className="text-right font-semibold pb-1">Prezzo</th>
                                        <th className="text-right font-semibold pb-1">Tot.</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {displayOrder.items.map((item, index) => (
                                        <React.Fragment key={`${item.menuItemId}-${index}-fallback2`}>
                                            <tr>
                                                <td className="pt-1">{item.menuItemName}</td>
                                                <td className="text-center pt-1">{item.quantity}</td>
                                                <td className="text-right pt-1">€{item.unitPrice.toFixed(2)}</td>
                                                <td className="text-right pt-1">€{(item.quantity * item.unitPrice).toFixed(2)}</td>
                                            </tr>
                                            {item.note && (
                                                <tr>
                                                    <td colSpan={4} className="text-xs italic text-muted-foreground pl-2 pb-1">
                                                        &nbsp;&nbsp;↳ {item.note}
                                                    </td>
                                                </tr>
                                            )}
                                        </React.Fragment>
                                    ))}
                                </tbody>
                            </table>
                        )}
                    </div>
                </ScrollArea>

                {/* Totals section, outside and below the scroll area */}
                <div className="px-6 pt-4 print:pt-2 flex-shrink-0">
                    <div className="space-y-1 text-sm mb-2 print:text-xs">
                        <div className="flex justify-between">
                            <span>Subtotale</span>
                            <span>€{itemsSubtotal.toFixed(2)}</span>
                        </div>
                        {displayOrder.guestCharge > 0 && (
                            <div className="flex justify-between">
                                <span>Coperto ({displayOrder.numberOfGuests} x €{(displayOrder.guestCharge / displayOrder.numberOfGuests).toFixed(2)})</span>
                                <span>€{displayOrder.guestCharge.toFixed(2)}</span>
                            </div>
                        )}
                        {displayOrder.takeawayCharge > 0 && (
                            <div className="flex justify-between">
                                <span>Asporto</span>
                                <span>€{displayOrder.takeawayCharge.toFixed(2)}</span>
                            </div>
                        )}
                    </div>

                    <div className="flex justify-between font-bold text-lg mb-2 pt-2 border-t print:text-base">
                        <span>TOTALE:</span>
                        {/* Use displayOrder for total */}
                        <span>€{displayOrder.totalAmount.toFixed(2)}</span>
                    </div>
                    <div className="text-sm text-muted-foreground print:text-xs">
                        {/* Use displayOrder for payment details */}
                        Metodo: {displayOrder.paymentMethod}
                        {displayOrder.paymentMethod === 'Contanti' && displayOrder.amountPaid != null && (
                            <>
                                <br />Ricevuto: €{displayOrder.amountPaid.toFixed(2)}
                                {displayOrder.amountPaid > displayOrder.totalAmount && (
                                    <span className="font-semibold"> - Resto: €{(displayOrder.amountPaid - displayOrder.totalAmount).toFixed(2)}</span>
                                )}
                            </>
                        )}
                    </div>

                    {/* QR Code Display - Use displayOrder */}
                    {displayOrder.qrCodeBase64 && (
                        <div className="flex justify-center my-4 print:my-2">
                            <Image
                                src={`data:image/png;base64,${displayOrder.qrCodeBase64}`}
                                alt={`QR Code Ordine ${displayOrder.id}`}
                                width={128} // from w-32
                                height={128} // from h-32
                                className="print:w-24 print:h-24" // Keep print styles
                            />
                        </div>
                    )}

                    {/* Optional: Add footer text like Grazie! */}
                    <p className="text-center text-xs text-muted-foreground mt-4 print:mt-2">Grazie!</p>
                </div>
                {/* Buttons visible only on screen */}
                <DialogFooter className="p-4 border-t print:hidden flex flex-col sm:flex-row sm:justify-between items-center gap-2 flex-shrink-0">
                    {/* Printer Selector for Admin Reprint Mode */}
                    {isReprintMode && availablePrinters && availablePrinters.length > 0 && (
                        <div className="w-full sm:w-auto flex items-center gap-2">
                            <Select
                                value={selectedPrinterId?.toString() || ''}
                                onValueChange={(value) => setSelectedPrinterId(value ? parseInt(value) : undefined)}
                            >
                                <SelectTrigger id="admin-printer-select" className="w-full sm:w-[200px]">
                                    <SelectValue placeholder="Seleziona stampante" />
                                </SelectTrigger>
                                <SelectContent>
                                    {availablePrinters.map(printer => (
                                        <SelectItem key={printer.id} value={printer.id.toString()}>
                                            {printer.name}
                                        </SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </div>
                    )}
                    {/* Original Buttons - adjust layout for new element */}
                    <div className="flex gap-2 w-full sm:w-auto justify-end">
                        <Button id="receipt-close-button" type="button" variant="outline" onClick={() => onClose()} className="">
                            <X className="mr-2 h-4 w-4" /> Chiudi
                        </Button>
                        <Button
                            id={isReprintMode ? "receipt-reprint-button" : "receipt-print-submit-button"}
                            type="button"
                            onClick={handlePrintAction} // Use the unified handler
                            disabled={isProcessing} // Disable if not connected or processing // MODIFIED: Remove connectionStatus check
                        >
                            {isProcessing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Printer className="mr-2 h-4 w-4" />}
                            {isProcessing ? 'Elaborazione...' : (isReprintMode ? 'Ristampa' : 'Invia Ordine')}
                        </Button>
                    </div>
                </DialogFooter>

                {/* Reprint Choice Dialog */}
                {isReprintMode && displayOrder && (
                    <AlertDialog open={showReprintChoiceDialog} onOpenChange={setShowReprintChoiceDialog}>
                        <AlertDialogContent>
                            <AlertDialogHeader>
                                <AlertDialogTitle>Cosa vuoi ristampare per l'ordine #{displayOrder.displayOrderNumber ?? displayOrder.id}?</AlertDialogTitle>
                                <AlertDialogDescription>
                                    {selectedPrinterId && availablePrinters?.find(p => p.id === selectedPrinterId) ?
                                        `La ristampa avverrà sulla stampante selezionata: ${availablePrinters.find(p => p.id === selectedPrinterId)?.name}.` :
                                        "La ristampa avverrà sulla stampante della cassa o predefinita dell'area."
                                    }
                                </AlertDialogDescription>
                            </AlertDialogHeader>
                            <AlertDialogFooter className="gap-2 sm:justify-start">
                                <AlertDialogAction onClick={() => handleActualReprint(ReprintType.ReceiptOnly)}
                                    disabled={isProcessing}
                                >
                                    {isProcessing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                                    Solo Ricevuta
                                </AlertDialogAction>
                                <AlertDialogAction onClick={() => handleActualReprint(ReprintType.ReceiptAndComandas)}
                                    disabled={isProcessing}
                                >
                                    {isProcessing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                                    Ricevuta e Comande
                                </AlertDialogAction>
                                <AlertDialogCancel onClick={() => setShowReprintChoiceDialog(false)} disabled={isProcessing}>
                                    Annulla
                                </AlertDialogCancel>
                            </AlertDialogFooter>
                        </AlertDialogContent>
                    </AlertDialog>
                )}
            </div>
        </ResponsiveDialog>
    );
};

export default ReceiptDialog;
