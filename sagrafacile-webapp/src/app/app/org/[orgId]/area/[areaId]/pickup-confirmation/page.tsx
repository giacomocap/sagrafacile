'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { useParams } from 'next/navigation';
import { getPublicReadyForPickupOrders, confirmOrderPickup } from '@/services/apiClient';
import { OrderDto, OrderStatus } from '@/types';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { toast } from 'sonner';
import { Loader2, PackageCheck, ServerCrash, RefreshCw, CheckCircle2, AlertCircle, Volume2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
// It's good practice to also use the useSignalRHub for real-time updates on this staff page,
// so it reflects changes made by the KDS or other systems immediately.
import useSignalRHub from '@/hooks/useSignalRHub';
import { OrderStatusBroadcastDto } from '@/types'; // For SignalR
import useAnnouncements from '@/hooks/useAnnouncements'; // Import the new hook

export default function StaffPickupConfirmationPage() {
    const params = useParams();
    // const audioRef = React.useRef<HTMLAudioElement | null>(null); // Removed, handled by hook
    const orgId = typeof params.orgId === 'string' ? params.orgId : ''; // May be needed for auth or specific fetches
    const areaId = typeof params.areaId === 'string' ? params.areaId : '';

    const [orders, setOrders] = useState<OrderDto[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [confirmingOrderId, setConfirmingOrderId] = useState<string | null>(null);

    // Initialize the announcements hook
    const { playNotificationSound, speakAnnouncement } = useAnnouncements({
        soundUrl: '/sounds/pickup-chime.mp3', // Optional: can be customized or use default
    });

    // SignalR setup (similar to public display for consistency and real-time updates)
    const hubUrl = `${process.env.NEXT_PUBLIC_API_BASE_URL}/orderHub`;
    const { connection, connectionStatus, startConnection, stopConnection } = useSignalRHub(hubUrl);
    const connectionSucceededRef = React.useRef(false);

    const fetchReadyOrders = useCallback(async () => {
        if (!areaId) {
            setError("Area ID non trovato nell'URL.");
            setIsLoading(false);
            return;
        }
        setIsLoading(true);
        setError(null);
        try {
            const numericAreaId = parseInt(areaId, 10);
            if (isNaN(numericAreaId)) {
                setError("Area ID non valido.");
                setIsLoading(false);
                return;
            }
            // Using getPublicReadyForPickupOrders for now. 
            // If a staff-specific endpoint with dayId filtering is needed, this should be updated.
            const fetchedOrders = await getPublicReadyForPickupOrders(numericAreaId);
            setOrders(fetchedOrders.sort((a, b) => new Date(a.orderDateTime).getTime() - new Date(b.orderDateTime).getTime())); // Show oldest first for staff
        } catch (err: unknown) {
            console.error("Error fetching ready orders:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            const errorMsg = error.response?.data?.message || error.message || 'Impossibile caricare gli ordini pronti per il ritiro.';
            setError(errorMsg);
            setOrders([]);
        } finally {
            setIsLoading(false);
        }
    }, [areaId]);

    useEffect(() => {
        fetchReadyOrders();
    }, [fetchReadyOrders]);

    // SignalR Connection Management
    useEffect(() => {
        connectionSucceededRef.current = false;
        if (areaId) {
            startConnection();
        }
        return () => {
            if (connectionSucceededRef.current) {
                stopConnection();
            }
        };
    }, [areaId, startConnection, stopConnection]);

    useEffect(() => {
        if (connectionStatus === 'Connected' && connection && areaId) {
            connectionSucceededRef.current = true;
            // Use the existing JoinAreaQueueGroup method as it joins the correct "Area-{areaId}" group
            connection.invoke('JoinAreaQueueGroup', areaId.toString())
                .then(() => console.log(`StaffPickupConfirmation: Joined Area group for Area ${areaId} using JoinAreaQueueGroup.`))
                .catch(err => console.error(`StaffPickupConfirmation: Failed to join Area group for Area ${areaId}`, err));
        }
    }, [connectionStatus, connection, areaId]);
    
    // SignalR Event Handlers for real-time updates
    useEffect(() => {
        if (!connection || connectionStatus !== 'Connected' || !areaId) return;

        const handleOrderStatusUpdate = (data: OrderStatusBroadcastDto) => {
            console.log("StaffPickupConfirmation SignalR: ReceiveOrderStatusUpdate received:", data);
            if (String(data.areaId) === areaId) {
                setOrders(prevOrders => {
                    const existingOrderIndex = prevOrders.findIndex(o => o.id === data.orderId);

                    if (data.newStatus === OrderStatus.ReadyForPickup) {
                        if (existingOrderIndex !== -1) {
                            // Order already in list, update its status and timestamp
                            const updatedOrder = { ...prevOrders[existingOrderIndex], status: data.newStatus, orderDateTime: data.statusChangeTime };
                            const otherOrders = prevOrders.filter(o => o.id !== data.orderId);
                            return [...otherOrders, updatedOrder].sort((a, b) => new Date(a.orderDateTime).getTime() - new Date(b.orderDateTime).getTime());
                        } else {
                            // New order ready for pickup, add to list
                             // New order ready for pickup, add to list
                            // Attempt to construct a more complete OrderDto from broadcast data
                            // Fields not in OrderStatusBroadcastDto will be undefined or default
                            const newOrder: OrderDto = {
                                id: data.orderId,
                                areaId: data.areaId,
                                customerName: data.customerName,
                                tableNumber: data.tableNumber,
                                orderDateTime: data.statusChangeTime,
                                status: data.newStatus,
                                // Default values for fields not in OrderStatusBroadcastDto
                                // These might need to be fetched if full display is required immediately
                                areaName: prevOrders.find(o => o.areaId === data.areaId)?.areaName || 'N/D', // Try to get from existing orders
                                totalAmount: 0, // Not in broadcast
                                items: [], // Not in broadcast
                                numberOfGuests: 0, // Not in broadcast
                                isTakeaway: false, // Not in broadcast
                                paymentMethod: undefined,
                                amountPaid: undefined,
                                qrCodeBase64: undefined,
                                dayId: prevOrders.find(o => o.dayId)?.dayId || undefined, // Try to get from existing orders
                                cashierId: undefined,
                                cashierName: undefined,
                                waiterId: undefined,
                                displayOrderNumber: data.displayOrderNumber, // MAP displayOrderNumber from broadcast
                            };
                            console.log("StaffPickupConfirmation SignalR: Adding new order to list:", newOrder);
                            const displayIdForToastAndSpeech = newOrder.displayOrderNumber ?? newOrder.id.substring(newOrder.id.length-4).toUpperCase();
                            // Play sound and announce for new orders appearing on this staff page too
                            playNotificationSound(); 
                            speakAnnouncement(displayIdForToastAndSpeech, newOrder.customerName);
                            toast.info(`Nuovo ordine pronto: ${displayIdForToastAndSpeech}`);
                            return [...prevOrders, newOrder].sort((a, b) => new Date(a.orderDateTime).getTime() - new Date(b.orderDateTime).getTime());
                        }
                    } else if (data.newStatus === OrderStatus.Completed) {
                        // Order completed, remove from this list
                        if (existingOrderIndex !== -1) {
                            console.log("StaffPickupConfirmation SignalR: Removing completed order from list:", data.orderId);
                            return prevOrders.filter(o => o.id !== data.orderId);
                        }
                    } else {
                        // If an order's status changes to something other than ReadyForPickup or Completed
                        // (e.g., back to Preparing, or Cancelled), remove it from this list.
                        if (existingOrderIndex !== -1) {
                            console.log(`StaffPickupConfirmation SignalR: Order ${data.orderId} changed to ${data.newStatus}, removing from ReadyForPickup list.`);
                            return prevOrders.filter(o => o.id !== data.orderId);
                        }
                    }
                    return prevOrders; // No change
                });
            }
        };

        connection.on('ReceiveOrderStatusUpdate', handleOrderStatusUpdate);

        return () => {
            connection?.off('ReceiveOrderStatusUpdate', handleOrderStatusUpdate);
            if (connection && areaId && connectionSucceededRef.current) {
                connection.invoke('LeaveAreaQueueGroup', areaId.toString())
                    .then(() => console.log(`StaffPickupConfirmation: Left Area group for Area ${areaId} using LeaveAreaQueueGroup.`))
                    .catch(err => console.error(`StaffPickupConfirmation: Failed to leave Area group for Area ${areaId}`, err));
            }
        };
    }, [connection, connectionStatus, areaId, playNotificationSound, speakAnnouncement]); // Added dependencies

    const handleRechimeOrder = (order: OrderDto) => {
        const displayId = order.displayOrderNumber ?? order.id.substring(order.id.length - 4).toUpperCase();
        console.log(`Rechiming order: ${displayId} (Internal ID: ${order.id})`);
        playNotificationSound();
        speakAnnouncement(displayId, order.customerName);
        toast.info(`Ordine ${displayId} richiamato.`);
    };

    const handleConfirmPickup = async (order: OrderDto) => { // Changed to accept full order object
        setConfirmingOrderId(order.id); // Still use internal ID for confirming state
        try {
            await confirmOrderPickup(order.id);
            const displayId = order.displayOrderNumber ?? order.id.substring(order.id.length - 4).toUpperCase();
            toast.success(`Ordine ${displayId} confermato come ritirato.`);
            // Optimistic update or rely on SignalR to remove it
            setOrders(prevOrders => prevOrders.filter(o => o.id !== order.id));
        } catch (err: unknown) {
            console.error("Error confirming pickup:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            const errorMsg = error.response?.data?.message || error.message || "Errore durante la conferma del ritiro.";
            toast.error(errorMsg);
        } finally {
            setConfirmingOrderId(null);
        }
    };

    if (!areaId || !orgId) {
         return (
            <div className="container mx-auto p-4">
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Errore di Configurazione</AlertTitle>
                    <AlertDescription>ID Organizzazione o Area non specificati.</AlertDescription>
                </Alert>
            </div>
        );
    }

    if (isLoading) {
        return (
            <div className="container mx-auto p-4 flex justify-center items-center min-h-[300px]">
                <Loader2 className="h-12 w-12 animate-spin text-primary" />
            </div>
        );
    }

    if (error) {
        return (
            <div className="container mx-auto p-4">
                <Alert variant="destructive">
                    <ServerCrash className="h-4 w-4" />
                    <AlertTitle>Errore di Caricamento</AlertTitle>
                    <AlertDescription>{error}</AlertDescription>
                </Alert>
            </div>
        );
    }

    return (
        <div className="container mx-auto p-4 md:p-6">
            <Card>
                <CardHeader>
                    <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-2">
                        <div>
                            <CardTitle className="text-2xl">Conferma Ritiro Ordini</CardTitle>
                            <CardDescription>
                                Visualizza gli ordini pronti per il ritiro e confermali. Area ID: {areaId}
                            </CardDescription>
                        </div>
                        <Button onClick={fetchReadyOrders} variant="outline" size="sm" disabled={isLoading}>
                            <RefreshCw className={`mr-2 h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
                            Aggiorna Lista
                        </Button>
                    </div>
                </CardHeader>
                <CardContent>
                    {orders.length === 0 ? (
                        <div className="text-center py-10">
                            <PackageCheck className="h-16 w-16 text-muted-foreground mx-auto mb-4" />
                            <p className="text-lg text-muted-foreground">Nessun ordine attualmente pronto per il ritiro.</p>
                            <p className="text-sm text-muted-foreground">La lista si aggiornerà in tempo reale.</p>
                        </div>
                    ) : (
                        <ScrollArea className="h-[calc(100vh-250px)]"> {/* Adjust height as needed */}
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>Ordine</TableHead>
                                        <TableHead>Cliente</TableHead>
                                        <TableHead>Ora Pronto</TableHead>
                                        <TableHead className="text-right">Azioni</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {orders.map((order) => (
                                        <TableRow key={order.id}>
                                            <TableCell className="font-medium">
                                                {order.displayOrderNumber ?? order.id.substring(order.id.length - 6).toUpperCase()}
                                                {order.displayOrderNumber && <span className="block text-xs text-muted-foreground">(ID: {order.id.substring(order.id.length - 6).toUpperCase()})</span>}
                                            </TableCell>
                                            <TableCell>{order.customerName || 'N/A'}</TableCell>
                                            <TableCell>{new Date(order.orderDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</TableCell>
                                            <TableCell className="text-right space-x-2">
                                                <Button
                                                    onClick={() => handleRechimeOrder(order)}
                                                    size="sm"
                                                    variant="outline"
                                                    aria-label="Richiama ordine"
                                                >
                                                    <Volume2 className="h-4 w-4" />
                                                </Button>
                                                <Button
                                                    onClick={() => handleConfirmPickup(order)} // Pass full order object
                                                    disabled={confirmingOrderId === order.id}
                                                    size="sm"
                                                    variant="default"
                                                >
                                                    {confirmingOrderId === order.id ? (
                                                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                                    ) : (
                                                        <CheckCircle2 className="mr-2 h-4 w-4" />
                                                    )}
                                                    Conferma
                                                </Button>
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </ScrollArea>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
