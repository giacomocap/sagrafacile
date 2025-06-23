'use client';

import React, { useState, useEffect, useCallback } from 'react';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogDescription,
    DialogFooter,
    DialogClose,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { AlertCircle, Loader2, Printer, } from 'lucide-react';
import apiClient from '@/services/apiClient';
import { OrderDto, OrderStatus, AreaDto, ReprintType, PaginatedResult, OrderQueryParameters } from '@/types'; // Added AreaDto, PaginatedResult, OrderQueryParameters
import { toast } from 'sonner';
import ReceiptDialog from '@/components/ReceiptDialog';
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";

// Placeholder for getOrderStatusVariant - replace with actual implementation or import
const getOrderStatusVariant = (status: OrderStatus): "default" | "secondary" | "destructive" | "outline" => {
    switch (status) {
        case OrderStatus.Paid:
        case OrderStatus.Completed:
            return "default"; // Or e.g., a success variant if you have one
        case OrderStatus.Cancelled:
            return "destructive";
        case OrderStatus.Preparing:
        case OrderStatus.ReadyForPickup:
            return "secondary";
        default:
            return "outline";
    }
};

interface ReprintOrderDialogProps {
    isOpen: boolean;
    onClose: () => void;
    areaId: number | null;
    orgId: string | null;
}

const ReprintOrderDialog: React.FC<ReprintOrderDialogProps> = ({ isOpen, onClose, areaId, orgId }) => {
    const [orders, setOrders] = useState<OrderDto[]>([]);
    const [currentArea, setCurrentArea] = useState<AreaDto | null>(null); // State to hold current area details
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [selectedOrderForReprint, setSelectedOrderForReprint] = useState<OrderDto | null>(null);
    const [isReceiptDialogOpen, setIsReceiptDialogOpen] = useState(false);
    const [searchTerm, setSearchTerm] = useState('');

    const fetchInitialData = useCallback(async () => {
        if (!isOpen || !areaId || !orgId) return;

        setIsLoading(true);
        setError(null);
        try {
            // Fetch both orders and the current area details in parallel
            const [ordersResponse, areaResponse] = await Promise.all([
                apiClient.get<PaginatedResult<OrderDto>>('/orders', {
                    params: {
                        organizationId: parseInt(orgId, 10),
                        areaId: areaId,
                    } as OrderQueryParameters // Cast to ensure type compatibility
                }),
                apiClient.get<AreaDto>(`/Areas/${areaId}`)
            ]);

            // Sort orders by date descending
            const sortedOrders = ordersResponse.data.items.sort((a, b) =>
                new Date(b.orderDateTime).getTime() - new Date(a.orderDateTime).getTime()
            );
            setOrders(sortedOrders);
            setCurrentArea(areaResponse.data);

        } catch (err: any) {
            console.error("Error fetching data for reprint dialog:", err);
            setError("Impossibile caricare i dati necessari.");
            toast.error("Errore nel caricamento dei dati.");
        } finally {
            setIsLoading(false);
        }
    }, [isOpen, areaId, orgId]);


    // Fetch orders when the dialog opens or when areaId/orgId changes while open
    useEffect(() => {
        fetchInitialData();
    }, [fetchInitialData]);

    const handleInitiateReprint = async (orderId: string, reprintJobType: ReprintType): Promise<{ success: boolean; message?: string }> => {
        try {
            const response = await apiClient.post(`/orders/${orderId}/reprint`, { reprintJobType });

            return { success: true, message: response.data.message || "Ristampa avviata con successo" };
        } catch (err: any) {
            console.error("Error initiating reprint:", err);
            return { success: false, message: err.response?.data?.message || "Errore durante l'avvio della ristampa." };
        }
    };

    const handleReprintClick = async (orderId: string) => {
        setError(null);
        try {
            // Fetch full details for the specific order
            const response = await apiClient.get<OrderDto>(`/orders/${orderId}`);
            const order = response.data;

            // Calculate charges, similar to the cashier interface
            let guestChargeAmount = 0;
            let takeawayChargeAmount = 0;

            if (currentArea) {
                if (!order.isTakeaway && currentArea.guestCharge > 0 && order.numberOfGuests > 0) {
                    guestChargeAmount = currentArea.guestCharge * order.numberOfGuests;
                }
                if (order.isTakeaway && currentArea.takeawayCharge > 0) {
                    takeawayChargeAmount = currentArea.takeawayCharge;
                }
            }

            const augmentedOrder = {
                ...order,
                guestCharge: guestChargeAmount,
                takeawayCharge: takeawayChargeAmount,
            };

            setSelectedOrderForReprint(augmentedOrder);
            setIsReceiptDialogOpen(true); // Open the receipt dialog
        } catch (err) {
            console.error("Error fetching order details for reprint:", err);
            toast.error("Impossibile caricare i dettagli dell'ordine per la ristampa.");
            setError("Errore nel recupero dei dettagli ordine.");
        }
    };

    const handleReceiptDialogClose = () => {
        setIsReceiptDialogOpen(false);
        setSelectedOrderForReprint(null); // Clear selected order
    };

    // Filter orders based on search term
    const filteredOrders = orders.filter(order =>
        order.customerName?.toLowerCase().includes(searchTerm.toLowerCase())
    );

    return (
        <>
            <Dialog open={isOpen} onOpenChange={onClose}>
                <DialogContent className="p-0 sm:max-w-3xl">
                    <DialogHeader className="p-6 pb-4"> {/* Adjusted padding */}
                        <DialogTitle>Storico Ordini / Ristampa Ricevuta</DialogTitle>
                        <DialogDescription>
                            Cerca per nome cliente o seleziona un ordine per visualizzare i dettagli e ristampare.
                        </DialogDescription>
                    </DialogHeader>

                    <div className="px-6 pb-6 space-y-4">
                        <Input
                            placeholder="Cerca per nome cliente..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            className="mb-4"
                        />
                        <div className="max-h-[calc(70vh-120px)] overflow-y-auto"> {/* Adjust max-h to account for search bar and padding */}
                            {isLoading && !error && (
                                <div className="flex justify-center items-center py-10">
                                    <Loader2 className="h-8 w-8 animate-spin" />
                                </div>
                            )}
                            {error && (
                                <Alert variant="destructive">
                                    <AlertCircle className="h-4 w-4" />
                                    <AlertTitle>Errore</AlertTitle>
                                    <AlertDescription>{error}</AlertDescription>
                                </Alert>
                            )}
                            {!isLoading && !error && filteredOrders.length === 0 && orders.length > 0 && (
                                <p className="text-center text-muted-foreground py-10">Nessun ordine trovato per "{searchTerm}".</p>
                            )}
                            {!isLoading && !error && orders.length === 0 && (
                                <p className="text-center text-muted-foreground py-10">Nessun ordine trovato nelle ultime 24 ore per quest'area.</p>
                            )}
                            {filteredOrders.length > 0 && (
                                <div className="border rounded-lg overflow-hidden">
                                    <Table>
                                        <TableHeader>
                                            <TableRow>
                                                <TableHead>ID Ordine</TableHead>
                                                <TableHead>Cliente</TableHead>
                                                <TableHead>Data/Ora</TableHead>
                                                <TableHead className="text-right">Totale</TableHead>
                                                <TableHead className="text-center">Stato</TableHead>
                                                <TableHead className="text-right">Azioni</TableHead>
                                            </TableRow>
                                        </TableHeader>
                                        <TableBody>
                                            {filteredOrders.map((order) => (
                                                <TableRow key={order.id} onClick={() => handleReprintClick(order.id)} className="cursor-pointer hover:bg-muted/50">
                                                    <TableCell className="font-mono text-xs">
                                                        {order.displayOrderNumber ?? order.id.substring(0, 8)}
                                                        {order.displayOrderNumber && <span className="block text-muted-foreground">(ID: {order.id.substring(0, 8)})</span>}
                                                        ...
                                                    </TableCell>
                                                    <TableCell>{order.customerName || 'N/D'}</TableCell>
                                                    <TableCell>{new Date(order.orderDateTime).toLocaleString()}</TableCell>
                                                    <TableCell className="text-right">€{order.totalAmount.toFixed(2)}</TableCell>
                                                    <TableCell className="text-center">
                                                        <Badge variant={getOrderStatusVariant(order.status)}>{OrderStatus[order.status]}</Badge>
                                                    </TableCell>
                                                    <TableCell className="text-right">
                                                        <Button variant="outline" size="sm" onClick={(e) => { e.stopPropagation(); handleReprintClick(order.id); }}>
                                                            <Printer className="mr-2 h-4 w-4" /> Ristampa
                                                        </Button>
                                                    </TableCell>
                                                </TableRow>
                                            ))}
                                        </TableBody>
                                    </Table>
                                </div>
                            )}
                        </div>
                    </div>

                    <DialogFooter className="p-6 pt-0 border-t"> {/* Added border-t */}
                        <DialogClose asChild>
                            <Button type="button" variant="outline">Chiudi</Button>
                        </DialogClose>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {selectedOrderForReprint && (
                <ReceiptDialog
                    isOpen={isReceiptDialogOpen}
                    onClose={handleReceiptDialogClose}
                    order={selectedOrderForReprint}
                    isReprintMode={true}
                    onSubmitOrder={async () => null}
                    onInitiateReprint={handleInitiateReprint}
                />
            )}
        </>
    );
};

export default ReprintOrderDialog;
