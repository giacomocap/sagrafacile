import React, { useState, useEffect, useMemo } from 'react';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogDescription,
    DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { KdsOrderDto, KdsOrderItemDto, KdsStatus } from '@/types';
import apiClient from '@/services/apiClient';
import { toast } from 'sonner';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Check, Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';
import { KdsOrderDetailDialogCloseResult } from '@/types'; // Import the type

interface KdsOrderDetailDialogProps {
    isOpen: boolean;
    onClose: (result: KdsOrderDetailDialogCloseResult) => void; // Use the imported type
    order: KdsOrderDto | null;
    kdsStationId: string; // Added: ID of the current KDS station
}

const KdsOrderDetailDialog: React.FC<KdsOrderDetailDialogProps> = ({
    isOpen,
    onClose,
    order,
    kdsStationId, // Added
}) => {
    const [itemsStatus, setItemsStatus] = useState<{ [key: number]: KdsStatus }>({});
    const [loadingItems, setLoadingItems] = useState<Set<number>>(new Set());
    const [isConfirming, setIsConfirming] = useState(false); // Loading state for final confirmation
    const [orderUpdated, setOrderUpdated] = useState(false);

    // The backend API (`/orders/kds-station/{kdsId}`) is expected
    // to return only items relevant to this station.
    const itemsToDisplay = useMemo(() => order?.items ?? [], [order]);

    useEffect(() => {
        // Initialize local item statuses when the order changes or itemsToDisplay changes
        if (order) {
            const initialStatuses = order.items.reduce((acc, item) => {
                acc[item.orderItemId] = item.kdsStatus;
                return acc;
            }, {} as { [key: number]: KdsStatus });
            setItemsStatus(initialStatuses);
            setOrderUpdated(false); // Reset update flag
            setIsConfirming(false); // Reset confirming flag
        }
    }, [order, itemsToDisplay]); // Depend on itemsToDisplay as well

    const allItemsConfirmedLocally = useMemo(() => {
        if (!itemsToDisplay.length) return false; // Cannot confirm if no items
        return itemsToDisplay.every(item => itemsStatus[item.orderItemId] === KdsStatus.Confirmed);
    }, [itemsStatus, itemsToDisplay]);

    if (!order) return null;

    const handleItemToggle = async (item: KdsOrderItemDto) => {
        const currentStatus = itemsStatus[item.orderItemId];
        const newStatus = currentStatus === KdsStatus.Confirmed ? KdsStatus.Pending : KdsStatus.Confirmed;

        setLoadingItems(prev => new Set(prev).add(item.orderItemId));
        try {
            await apiClient.put(`/orders/${order.orderId}/items/${item.orderItemId}/kds-status`, {
                kdsStatus: newStatus,
            });
            setItemsStatus(prev => ({ ...prev, [item.orderItemId]: newStatus }));
            setOrderUpdated(true); // Mark that an update occurred
            toast.success(`Stato articolo "${item.menuItemName}" aggiornato.`);
        } catch (error: any) {
            console.error("Error updating KDS item status:", error);
            toast.error(`Errore aggiornamento stato articolo: ${error.message || 'Errore sconosciuto'}`);
        } finally {
            setLoadingItems(prev => {
                const newSet = new Set(prev);
                newSet.delete(item.orderItemId);
                return newSet;
            });
        }
    };

    const handleConfirmCompletion = async () => {
        if (!order || !allItemsConfirmedLocally || isConfirming) return;

        setIsConfirming(true);
        try {
            // Updated API endpoint to include kdsStationId
            await apiClient.put(`/orders/${order.orderId}/kds-confirm-complete/${kdsStationId}`);
            toast.success(`Completamento stazione per ordine ${order.displayOrderNumber ?? order.orderId.substring(0, 8)} confermato.`);
            onClose({ completed: true }); // Close dialog and signal station completion for this order
        } catch (error: any) {
            console.error("Error confirming KDS station completion:", error);
            toast.error(`Errore conferma completamento stazione: ${error.response?.data || error.message || 'Errore sconosciuto'}`);
            setIsConfirming(false); // Allow retry
        }
        // No finally block needed to reset isConfirming if onClose happens on success
    };

    const handleCloseDialog = () => {
        // Only close if not currently confirming the final step
        if (!isConfirming) {
            // Pass back whether updates were made if not completing the order
            onClose({ completed: false, updatedStatuses: orderUpdated ? itemsStatus : undefined });
        }
    };


    return (
        <Dialog open={isOpen} onOpenChange={(open) => !open && handleCloseDialog()}>
            <DialogContent className="sm:max-w-[600px]">
                <DialogHeader>
                    <DialogTitle>Dettaglio Ordine: {order.displayOrderNumber ?? order.orderId.substring(0, 8)}...</DialogTitle>
                    <DialogDescription>
                        <span className="font-semibold">Tavolo: {order.tableNumber || 'N/A'}</span>
                        {order.customerName && <span className="ml-2 font-semibold">Cliente: {order.customerName}</span>}
                        <span className="ml-2 text-muted-foreground">({new Date(order.orderDateTime).toLocaleTimeString()})</span>
                        <br />
                        Tocca un articolo per confermarne la preparazione per questa postazione.
                    </DialogDescription>
                </DialogHeader>
                <ScrollArea className="max-h-[60vh] pr-4">
                    <div className="space-y-3 py-4">
                        {itemsToDisplay.map((item) => {
                            const isConfirmed = itemsStatus[item.orderItemId] === KdsStatus.Confirmed;
                            const isLoading = loadingItems.has(item.orderItemId);
                            return (
                                <div
                                    key={item.orderItemId}
                                    className={cn(
                                        "flex items-center justify-between p-3 border rounded-md cursor-pointer transition-all",
                                        isConfirmed ? "bg-green-100 dark:bg-green-900 border-green-300 dark:border-green-700" : "hover:bg-muted/50",
                                        isLoading ? "opacity-50 cursor-not-allowed" : ""
                                    )}
                                    onClick={() => !isLoading && handleItemToggle(item)}
                                >
                                    <div className={cn("flex-1", isConfirmed ? "line-through text-muted-foreground" : "")}>
                                        <p className="font-semibold">{item.quantity}x {item.menuItemName}</p>
                                        {item.note && <p className="text-sm text-muted-foreground italic">Nota: {item.note}</p>}
                                    </div>
                                    <div className="ml-4 w-6 h-6 flex items-center justify-center">
                                        {isLoading ? (
                                            <Loader2 className="h-5 w-5 animate-spin" />
                                        ) : isConfirmed ? (
                                            <Check className="h-5 w-5 text-green-600 dark:text-green-400" />
                                        ) : null}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </ScrollArea>
                <DialogFooter>
                    <Button
                        type="button"
                        variant="secondary"
                        onClick={handleCloseDialog}
                        disabled={isConfirming} // Disable while confirming
                    >
                        Chiudi
                    </Button>
                    <Button
                        type="button"
                        onClick={handleConfirmCompletion}
                        disabled={!allItemsConfirmedLocally || isConfirming || loadingItems.size > 0} // Disable if not all confirmed, or confirming, or individual items loading
                    >
                        {isConfirming ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                        Conferma Completamento Stazione
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
};

export default KdsOrderDetailDialog;
