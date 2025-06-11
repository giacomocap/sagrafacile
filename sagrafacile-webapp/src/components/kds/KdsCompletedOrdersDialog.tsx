'use client';

import React, { useState, useEffect } from 'react';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogDescription,
    DialogFooter,
    // Removed duplicate DialogFooter and DialogClose
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { KdsOrderDto } from '@/types'; // Use KdsOrderDto
import apiClient from '@/services/apiClient';
import { toast } from 'sonner';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Terminal } from 'lucide-react'; // Removed Eye icon
import {
    Accordion,
    AccordionContent,
    AccordionItem,
    AccordionTrigger,
} from "@/components/ui/accordion"; // Import Accordion components


interface KdsCompletedOrdersDialogProps {
    isOpen: boolean;
    onClose: () => void;
    kdsStationId: string; // Now definitely needed
    areaId: string; // Keep for context if needed by API
    orgId: string; // Keep for context if needed by API
}

const KdsCompletedOrdersDialog: React.FC<KdsCompletedOrdersDialogProps> = ({
    isOpen,
    onClose,
    kdsStationId,
    areaId, // Included in case the new endpoint needs it
    orgId,
}) => {
    const [completedOrders, setCompletedOrders] = useState<KdsOrderDto[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    // Removed state for detail view dialog

    useEffect(() => {
        if (isOpen && kdsStationId) {
            const fetchCompletedOrders = async () => {
                setIsLoading(true);
                setError(null);
                setCompletedOrders([]);
                try {
                    // --- Use EXISTING endpoint with query parameter ---
                    // Backend needs modification to handle `includeCompleted=true`
                    console.log(`Fetching completed orders for KDS Station: ${kdsStationId} using existing endpoint with param`);
                    const response = await apiClient.get<KdsOrderDto[]>(`/orders/kds-station/${kdsStationId}?includeCompleted=true`); // Use existing endpoint + param

                    // Sort by order time descending? Or completion time if available from backend?
                    setCompletedOrders(response.data?.sort((a, b) => new Date(b.orderDateTime).getTime() - new Date(a.orderDateTime).getTime()) || []);
                    console.log("Fetched completed orders:", response.data); // Log fetched data
                } catch (err: any) {
                    console.error("Error fetching completed KDS orders:", err);
                    // Adjust error message slightly as endpoint should exist
                    const message = err.response?.status === 404 // Should ideally not happen if endpoint exists but param is ignored
                        ? "Errore nel recuperare gli ordini completati (endpoint non trovato)."
                        : `Errore nel caricare ordini completati: ${err.response?.data?.title || err.message || 'Errore sconosciuto'}`;
                    setError(message);
                    toast.error(message);
                } finally {
                    setIsLoading(false);
                }
            };
            fetchCompletedOrders();
        } else {
            // Reset state if dialog is closed or kdsStationId is missing
            setCompletedOrders([]);
            setError(null);
            setIsLoading(false);
        }
    }, [isOpen, kdsStationId, orgId, areaId]); // Dependencies

    // Removed handleViewDetails

    return (
        <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
                {/* Responsive width */}
                <DialogContent className="sm:max-w-[90vw] md:max-w-[80vw] lg:max-w-[70vw] xl:max-w-[60vw]">
                    <DialogHeader>
                        {/* Updated Title */}
                        <DialogTitle>Storico Ordini Completati (Stazione KDS: {kdsStationId})</DialogTitle>
                        <DialogDescription>
                            Visualizza gli ordini recentemente completati da questa postazione KDS.
                        </DialogDescription>
                    </DialogHeader>

                    {/* Scrollable content with padding and spacing */}
                    {/* Scrollable content with padding, no space-y needed for accordion */}
                    <div className="max-h-[70vh] overflow-y-auto pr-2 py-4">
                        {isLoading && (
                            // Use Skeletons mimicking Accordion structure
                            <div className="space-y-2">
                                {[...Array(3)].map((_, i) => (
                                    <Skeleton key={i} className="h-12 w-full rounded-md" />
                                ))}
                            </div>
                        )}
                        {error && (
                            <Alert variant="destructive" className="my-4"> {/* Added margin */}
                                <Terminal className="h-4 w-4" />
                                <AlertTitle>Errore</AlertTitle>
                                <AlertDescription>{error}</AlertDescription>
                            </Alert>
                        )}
                        {!isLoading && !error && completedOrders.length === 0 && (
                            <p className="text-center text-muted-foreground py-4">Nessun ordine completato trovato recentemente per questa postazione.</p>
                        )}
                        {/* Display using Accordion */}
                        {!isLoading && !error && completedOrders.length > 0 && (
                            <Accordion type="single" collapsible className="w-full">
                                {completedOrders.map((order) => (
                                    <AccordionItem value={order.orderId} key={order.orderId}>
                                        {/* Increased padding and font size for better touch */}
                                        <AccordionTrigger className="text-base px-6 py-4 hover:bg-muted/50 rounded-md">
                                            {/* Order Summary in Trigger */}
                                            <div className="flex justify-between w-full items-center gap-4"> {/* Increased gap */}
                                                <span className="font-medium truncate mr-3 flex-1 text-left"> {/* Increased margin */}
                                                    {order.customerName ? `${order.customerName}` : `Ordine ${order.orderId.substring(0, 6)}`}
                                                    {order.customerName && <span className="text-xs text-muted-foreground ml-1">({order.orderId.substring(0, 6)}...)</span>}
                                                </span>
                                                <span className="text-xs text-muted-foreground whitespace-nowrap flex-shrink-0"> {/* Prevent shrinking */}
                                                    T: {order.tableNumber || '-'} | {new Date(order.orderDateTime).toLocaleTimeString()}
                                                    {/* TODO: Add Completion Time */}
                                                </span>
                                            </div>
                                        </AccordionTrigger>
                                        {/* Increased padding and font size for better touch */}
                                        <AccordionContent className="px-6 pt-3 pb-4 bg-muted/30 rounded-b-md">
                                            {/* Item List in Content */}
                                            <ul className="list-disc pl-6 space-y-2 text-base"> {/* Increased padding/spacing/font */}
                                                {order.items.length > 0 ? (
                                                    order.items.map(item => (
                                                        <li key={item.orderItemId}>
                                                            <span className="font-semibold">{item.quantity} x</span> {item.menuItemName}
                                                            {item.note && <span className="text-xs text-muted-foreground italic ml-1"> - "{item.note}"</span>}
                                                        </li>
                                                    ))
                                                ) : (
                                                    <li className="text-muted-foreground italic">Nessun articolo trovato per questo ordine (potrebbe essere un errore).</li>
                                                )}
                                            </ul>
                                        </AccordionContent>
                                    </AccordionItem>
                                ))}
                            </Accordion>
                        )}
                    </div>

                    <DialogFooter>
                        <Button variant="outline" onClick={onClose}>Chiudi</Button>
                    </DialogFooter>
                </DialogContent>
            {/* Removed detail view dialog placeholder */}
        </Dialog>
    );
};

export default KdsCompletedOrdersDialog;
