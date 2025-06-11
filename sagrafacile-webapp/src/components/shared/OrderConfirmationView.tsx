"use client";

import React, { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AlertCircle, Loader2, Send } from 'lucide-react';
import { toast } from 'sonner';
import apiClient from '@/services/apiClient';
import { OrderDto, OrderStatus } from '@/types';

interface OrderConfirmationViewProps {
    orderId: string;
    onCancel: () => void;
    onSuccess: () => void;
}

const OrderConfirmationView: React.FC<OrderConfirmationViewProps> = ({ orderId, onCancel, onSuccess }) => {
    const [order, setOrder] = useState<OrderDto | null>(null);
    const [tableNumber, setTableNumber] = useState('');
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [isSubmitting, setIsSubmitting] = useState(false);

    useEffect(() => {
        const fetchOrderDetails = async () => {
            setIsLoading(true);
            setError(null);
            try {
                const response = await apiClient.get<OrderDto>(`/orders/${orderId}`);
                const fetchedOrder = response.data;

                if (fetchedOrder.status !== OrderStatus.Paid && fetchedOrder.status !== OrderStatus.PreOrder) {
                    const statusString = OrderStatus[fetchedOrder.status] ?? 'Sconosciuto';
                    setError(`Ordine ${fetchedOrder.displayOrderNumber ?? orderId} non è in uno stato confermabile (Stato attuale: ${statusString}).`);
                    toast.info(`Ordine ${fetchedOrder.displayOrderNumber ?? orderId} già assegnato ${fetchedOrder.tableNumber ? `al tavolo ${fetchedOrder.tableNumber}` : ''}. Visualizzazione sola lettura.`);
                }

                setOrder(fetchedOrder);

                if (fetchedOrder.tableNumber) {
                    setTableNumber(fetchedOrder.tableNumber);
                }

            } catch (err: any) {
                console.error("Failed to fetch order:", err);
                const errorMsg = err.response?.data?.message || err.message || 'Errore sconosciuto nel caricamento dell\'ordine.';
                setError(`Impossibile caricare l'ordine ${orderId}: ${errorMsg}`);
                toast.error(`Errore caricamento ordine ${orderId}`);
            } finally {
                setIsLoading(false);
            }
        };

        if (orderId) {
            fetchOrderDetails();
        }
    }, [orderId]);

    const handleConfirm = async () => {
        if (!tableNumber.trim()) {
            toast.error("Inserire il numero del tavolo.");
            return;
        }
        if (order?.tableNumber) {
            toast.warning("Questo ordine è già stato confermato.");
            return;
        }
        setIsSubmitting(true);
        setError(null);
        try {
            await apiClient.put(`/orders/${orderId}/confirm-preparation`, { tableNumber });
            toast.success(`Ordine ${orderId} confermato per tavolo ${tableNumber} e inviato in preparazione.`);
            onSuccess();
        } catch (err: any) {
            console.error("Failed to confirm order preparation:", err);
            const errorMsg = err.response?.data?.message || err.message || 'Errore sconosciuto durante la conferma dell\'ordine.';
            setError(`Errore conferma ordine: ${errorMsg}`);
            toast.error(`Errore conferma ordine: ${errorMsg}`);
        } finally {
            setIsSubmitting(false);
        }
    };

    if (isLoading) {
        return (
            <div className="space-y-4 w-full max-w-lg p-4">
                <Skeleton className="h-8 w-3/4" />
                <Skeleton className="h-4 w-1/2" />
                <Skeleton className="h-20 w-full" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-1/2" />
            </div>
        );
    }

    if (error && !order) {
        return (
            <div className="w-full max-w-lg p-4">
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Errore Caricamento Ordine</AlertTitle>
                    <AlertDescription>{error}</AlertDescription>
                </Alert>
                <Button variant="outline" onClick={onCancel} className="mt-4">
                    Indietro
                </Button>
            </div>
        );
    }

    if (!order) {
        return <p className="p-4">Nessun dato ordine disponibile.</p>;
    }

    const total = order.items.reduce((sum, item) => sum + item.unitPrice * item.quantity, 0);
    const isConfirmable = order.status === OrderStatus.Paid || order.status === OrderStatus.PreOrder;
    const isAlreadyConfirmed = !!order.tableNumber;

    return (
        <div className="space-y-4 w-full max-w-lg p-4">
            <h2 className="text-2xl font-semibold">
                Ordine: {order.displayOrderNumber ?? order.id}
                {order.displayOrderNumber && <span className="block text-sm text-muted-foreground">(ID Interno: {order.id})</span>}
            </h2>

            {error && !isAlreadyConfirmed && (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Attenzione</AlertTitle>
                    <AlertDescription>{error}</AlertDescription>
                </Alert>
            )}

            {isAlreadyConfirmed && (
                <Alert>
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Ordine Già Confermato</AlertTitle>
                    <AlertDescription>Questo ordine è già stato assegnato al tavolo {order.tableNumber}.</AlertDescription>
                </Alert>
            )}

            <div className="border rounded-lg">
                <div className="p-4">
                    <h3 className="text-lg font-semibold">Dettagli Ordine</h3>
                    <p className="text-sm text-muted-foreground">Cliente: {order.customerName || 'N/A'}</p>
                    <p className="text-sm text-muted-foreground">Stato: {OrderStatus[order.status] ?? 'Sconosciuto'}</p>
                </div>
                <div className="p-4 pt-0 overflow-x-auto">
                    <Table className="min-w-full">
                        <TableHeader>
                            <TableRow>
                                <TableHead>Qtà</TableHead>
                                <TableHead>Articolo</TableHead>
                                <TableHead className="text-right">Prezzo</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {order.items.map((item) => (
                                <TableRow key={item.menuItemId}>
                                    <TableCell>{item.quantity}</TableCell>
                                    <TableCell>
                                        {item.menuItemName}
                                        {item.note && <p className="text-xs text-muted-foreground">({item.note})</p>}
                                    </TableCell>
                                    <TableCell className="text-right">€{(item.unitPrice * item.quantity).toFixed(2)}</TableCell>
                                </TableRow>
                            ))}
                            <TableRow className="font-bold">
                                <TableCell colSpan={2}>Totale</TableCell>
                                <TableCell className="text-right">€{total.toFixed(2)}</TableCell>
                            </TableRow>
                        </TableBody>
                    </Table>
                </div>
            </div>

            {isConfirmable && !isAlreadyConfirmed && (
                <div className="space-y-2">
                    <Label htmlFor="tableNumber">Numero Tavolo *</Label>
                    <Input
                        id="tableNumber"
                        value={tableNumber}
                        onChange={(e) => setTableNumber(e.target.value)}
                        placeholder="Es. T1, 15, A3..."
                        disabled={isSubmitting}
                    />
                </div>
            )}

            <div className="flex flex-col sm:flex-row sm:justify-between items-center gap-2 pt-4">
                <Button variant="outline" onClick={onCancel} disabled={isSubmitting} className="w-full sm:w-auto">
                    {isConfirmable && !isAlreadyConfirmed ? 'Annulla' : 'Chiudi'}
                </Button>
                {isConfirmable && !isAlreadyConfirmed && (
                    <Button onClick={handleConfirm} disabled={!tableNumber.trim() || isSubmitting}>
                        {isSubmitting ? (
                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                            <Send className="mr-2 h-4 w-4" />
                        )}
                        Conferma e Invia
                    </Button>
                )}
            </div>
        </div>
    );
};

export default OrderConfirmationView;
