'use client';

import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useParams } from 'next/navigation';
import { getPublicReadyForPickupOrders } from '@/services/apiClient'; // Removed apiBaseUrl
import { OrderDto, OrderStatus, OrderStatusBroadcastDto } from '@/types';
import useSignalRHub from '@/hooks/useSignalRHub';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { AlertCircle, Loader2, ServerCrash, PackageSearch } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { toast } from 'sonner';
import useAnnouncements from '@/hooks/useAnnouncements'; // Import the new hook
import AdCarousel from '@/components/public/AdCarousel';
import { useAds } from '@/hooks/useAds';

export default function PublicPickupDisplayPage() {
    const params = useParams();
    const areaId = typeof params.areaId === 'string' ? params.areaId : '';
    // const orgSlug = typeof params.orgSlug === 'string' ? params.orgSlug : ''; // For future use if needed

    const [orders, setOrders] = useState<OrderDto[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // const audioRef = useRef<HTMLAudioElement | null>(null); // Removed, handled by hook
    const connectionSucceededRef = useRef(false);

    const hubUrl = `${process.env.NEXT_PUBLIC_API_BASE_URL}/orderHub`;
    const { connection, connectionStatus, startConnection, stopConnection } = useSignalRHub(hubUrl);

    // Initialize the announcements hook
    const { playNotificationSound, speakAnnouncement } = useAnnouncements({
        soundUrl: '/sounds/pickup-chime.mp3', // Default, can be customized
        speechRate: 0.8, // Slightly slower for public display
    });

    // Use the new useAds hook
    const { adMediaItems } = useAds(areaId);

    const fetchInitialOrders = useCallback(async () => {
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
                console.error(`PublicPickupDisplay: Invalid Area ID: ${areaId}`);
                return;
            }
            console.log(`PublicPickupDisplay: Fetching initial orders for Area ID: ${numericAreaId}`);
            const fetchedOrders = await getPublicReadyForPickupOrders(numericAreaId);
            console.log(`PublicPickupDisplay: Fetched initial orders for Area ID ${numericAreaId}:`, fetchedOrders);
            setOrders(prevOrders => {
                const newOrderList = fetchedOrders.sort((a, b) => new Date(b.orderDateTime).getTime() - new Date(a.orderDateTime).getTime());
                console.log(`PublicPickupDisplay: Setting orders after initial fetch. Prev count: ${prevOrders.length}, New count: ${newOrderList.length}`);
                return newOrderList;
            });
        } catch (err: unknown) {
            console.error(`PublicPickupDisplay: Error fetching initial orders for Area ID ${areaId}:`, err);
            let errorMsg = 'Impossibile caricare gli ordini pronti.';
            if (typeof err === 'object' && err !== null && 'message' in err) {
                errorMsg = String((err as { message: string }).message);
                if ('response' in err && typeof (err as { response?: { data?: { message?: string } } }).response?.data?.message === 'string') {
                    errorMsg = String((err as { response: { data: { message: string } } }).response.data.message);
                }
            }
            setError(errorMsg);
            setOrders(prevOrders => {
                console.log(`PublicPickupDisplay: Setting orders to empty array due to fetch error. Prev count: ${prevOrders.length}`);
                return [];
            });
        } finally {
            setIsLoading(false);
        }
    }, [areaId]);

    useEffect(() => {
        console.log("PublicPickupDisplay: areaId changed or component mounted, calling fetchInitialOrders. AreaId:", areaId);
        if (areaId) { // Ensure areaId is present before fetching
            fetchInitialOrders();
        } else {
            console.warn("PublicPickupDisplay: fetchInitialOrders not called because areaId is not yet available.");
            setIsLoading(false); // Stop loading if no areaId
            setError("ID Area non disponibile per caricare gli ordini.");
        }
    }, [fetchInitialOrders, areaId]); // Added areaId to dependencies to re-fetch if it changes late

    // SignalR Connection Management
    useEffect(() => {
        connectionSucceededRef.current = false;
        if (areaId) { // Only start connection if areaId is present
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
                .then(() => console.log(`PickupDisplay: Joined Area group for Area ${areaId} using JoinAreaQueueGroup.`))
                .catch(err => console.error(`PickupDisplay: Failed to join Area group for Area ${areaId}`, err));
        }
    }, [connectionStatus, connection, areaId]);

    // SignalR Event Handlers
    useEffect(() => {
        if (!connection || connectionStatus !== 'Connected' || !areaId) return;

        const handleOrderStatusUpdate = (data: OrderStatusBroadcastDto) => {
            console.log("PublicPickupDisplay SignalR: ReceiveOrderStatusUpdate received:", data);
            if (!areaId) {
                console.warn("PublicPickupDisplay SignalR: areaId is not set, cannot process update for data:", data);
                return;
            }
            if (String(data.areaId) === areaId) {
                console.log(`PublicPickupDisplay SignalR: Area ID matches (${data.areaId}). Processing update for order ${data.orderId}.`);
                setOrders(prevOrders => {
                    console.log(`PublicPickupDisplay SignalR: setOrders called. Prev orders count: ${prevOrders.length}, OrderId: ${data.orderId}, NewStatus: ${data.newStatus}`);
                    const existingOrderIndex = prevOrders.findIndex(o => o.id === data.orderId);
                    let newOrdersList = [...prevOrders];

                    if (data.newStatus === OrderStatus.ReadyForPickup) {
                        const orderPayload: OrderDto = {
                            id: data.orderId,
                            areaId: data.areaId,
                            customerName: data.customerName,
                            tableNumber: data.tableNumber,
                            orderDateTime: data.statusChangeTime,
                            status: data.newStatus,
                            displayOrderNumber: data.displayOrderNumber,
                            areaName: prevOrders.find(o => o.areaId === data.areaId)?.areaName || '', // Try to get from existing
                            totalAmount: 0, items: [], numberOfGuests: 0, isTakeaway: false, // Defaults
                            guestCharge: 0, // Add default value
                            takeawayCharge: 0, // Add default value
                            paymentMethod: undefined, amountPaid: undefined, qrCodeBase64: undefined,
                            dayId: prevOrders.find(o => o.dayId)?.dayId || undefined, // Try to get from existing
                            cashierId: undefined, cashierName: undefined, waiterId: undefined,
                        };

                        if (existingOrderIndex !== -1) {
                            console.log(`PublicPickupDisplay SignalR: Updating existing order ${data.orderId} to ReadyForPickup.`);
                            newOrdersList[existingOrderIndex] = { ...newOrdersList[existingOrderIndex], ...orderPayload };
                        } else {
                            console.log(`PublicPickupDisplay SignalR: Adding new order ${data.orderId} as ReadyForPickup.`);
                            newOrdersList.push(orderPayload);
                        }
                        playNotificationSound();
                        speakAnnouncement(data.displayOrderNumber ?? data.orderId, data.customerName);
                        toast.success(`Ordine ${data.displayOrderNumber ?? data.orderId.substring(data.orderId.length - 4).toUpperCase()} è pronto per il ritiro!`);
                    } else if (data.newStatus === OrderStatus.Completed) {
                        if (existingOrderIndex !== -1) {
                            console.log(`PublicPickupDisplay SignalR: Removing completed order ${data.orderId}.`);
                            newOrdersList = newOrdersList.filter(o => o.id !== data.orderId);
                            toast.info(`Ordine ${data.displayOrderNumber ?? data.orderId.substring(data.orderId.length - 4).toUpperCase()} ritirato.`);
                        } else {
                             console.log(`PublicPickupDisplay SignalR: Received Completed status for order ${data.orderId} not in list.`);
                        }
                    } else { // Other statuses (e.g., Preparing, Cancelled)
                        if (existingOrderIndex !== -1) {
                            console.log(`PublicPickupDisplay SignalR: Order ${data.orderId} changed to ${data.newStatus}, removing from display.`);
                            newOrdersList = newOrdersList.filter(o => o.id !== data.orderId);
                        } else {
                            console.log(`PublicPickupDisplay SignalR: Received status ${data.newStatus} for order ${data.orderId} not in list.`);
                        }
                    }
                    
                    const sortedList = newOrdersList.sort((a, b) => new Date(b.orderDateTime).getTime() - new Date(a.orderDateTime).getTime());
                    console.log(`PublicPickupDisplay SignalR: Orders after update. New count: ${sortedList.length}`);
                    return sortedList;
                });
            } else {
                console.log(`PublicPickupDisplay SignalR: Received update for different Area ID. Expected: ${areaId}, Got: ${data.areaId}. Ignoring.`);
            }
        };

        connection.on('ReceiveOrderStatusUpdate', handleOrderStatusUpdate);

        return () => {
            connection?.off('ReceiveOrderStatusUpdate', handleOrderStatusUpdate);
            if (connection && areaId && connectionSucceededRef.current) {
                // Use the existing LeaveAreaQueueGroup method
                connection.invoke('LeaveAreaQueueGroup', areaId.toString())
                    .then(() => console.log(`PickupDisplay: Left Area group for Area ${areaId} using LeaveAreaQueueGroup.`))
                    .catch(err => console.error(`PickupDisplay: Failed to leave Area group for Area ${areaId}`, err));
            }
        };
    }, [connection, connectionStatus, areaId, playNotificationSound, speakAnnouncement]);


    const renderOrderCard = (order: OrderDto) => {
        const displayId = order.displayOrderNumber ?? order.id.substring(order.id.length - 4).toUpperCase();
        // Simple way to check if an order was recently added/updated via SignalR for highlighting
        // This is a basic example; a more robust solution might involve timestamps or specific flags
        const isNew = new Date().getTime() - new Date(order.orderDateTime).getTime() < 60000; // Highlight if updated in last minute

        return (
            <Card
                key={order.id}
                className={`shadow-lg transition-all duration-500 ease-out ${isNew ? 'ring-2 ring-primary animate-pulse-slow' : 'border-border'}`}
            >
                <CardHeader className="pb-2">
                    <CardTitle className="text-4xl md:text-5xl font-bold text-center text-primary">
                        {displayId}
                    </CardTitle>
                </CardHeader>
                <CardContent className="text-center">
                    {order.customerName && (
                        <p className="text-xl md:text-2xl font-semibold text-foreground truncate" title={order.customerName}>
                            {order.customerName}
                        </p>
                    )}
                    {!order.customerName && (
                        <p className="text-xl md:text-2xl font-semibold text-muted-foreground">Cliente Anonimo</p>
                    )}
                    <p className="text-sm text-muted-foreground mt-1">
                        Pronto alle: {new Date(order.orderDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    </p>
                </CardContent>
            </Card>
        );
    };

    if (!areaId) {
        return (
            <div className="min-h-screen flex items-center justify-center p-6 bg-gradient-to-br from-background to-muted">
                <Alert variant="destructive" className="w-full max-w-md mx-auto">
                    <AlertCircle className="h-5 w-5" />
                    <AlertTitle>Errore Configurazione</AlertTitle>
                    <AlertDescription>
                        ID Area non specificato nell'URL. Impossibile caricare la pagina.
                    </AlertDescription>
                </Alert>
            </div>
        );
    }

    if (isLoading) {
        return (
            <div className="min-h-screen flex items-center justify-center p-6 bg-gradient-to-br from-background to-muted">
                <div className="text-center">
                    <Loader2 className="h-16 w-16 text-primary animate-spin mx-auto mb-4" />
                    <p className="text-xl text-muted-foreground">Caricamento ordini pronti...</p>
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="min-h-screen flex items-center justify-center p-6 bg-gradient-to-br from-background to-muted">
                <Alert variant="destructive" className="w-full max-w-md mx-auto">
                    <ServerCrash className="h-5 w-5" />
                    <AlertTitle>Errore di Caricamento</AlertTitle>
                    <AlertDescription>
                        {error}
                        <p className="mt-2">Impossibile caricare gli ordini. Riprova più tardi o contatta l'assistenza.</p>
                    </AlertDescription>
                </Alert>
            </div>
        );
    }

    return (
        <div className="h-screen w-screen bg-gradient-to-br from-background to-muted flex flex-col overflow-hidden">
            <style jsx global>{`
                @keyframes pulse-slow {
                    0%, 100% { opacity: 1; box-shadow: 0 0 0 0 rgba(var(--primary-rgb), 0.4); }
                    50% { opacity: 0.9; box-shadow: 0 0 0 0.5rem rgba(var(--primary-rgb), 0); }
                }
                .animate-pulse-slow { animation: pulse-slow 2s cubic-bezier(0.4, 0, 0.6, 1) infinite; }
                :root { --primary-rgb: 220, 38, 38; /* Example: Tailwind's red-600. Adjust if your primary color changes */ }
            `}</style>
            <main className="flex-grow p-4 md:p-8 overflow-y-auto">
                <header className="mb-8 md:mb-12 text-center">
                    <h1 className="text-4xl sm:text-5xl md:text-6xl font-bold text-foreground">
                        Ordini Pronti per il Ritiro
                    </h1>
                </header>

                {orders.length === 0 ? (
                    <Card className="w-full max-w-2xl mx-auto shadow-xl">
                        <CardHeader className="items-center">
                            <PackageSearch className="h-20 w-20 text-primary mb-4" />
                            <CardTitle className="text-2xl">Nessun Ordine Pronto</CardTitle>
                        </CardHeader>
                        <CardContent className="text-center">
                            <p className="text-muted-foreground">
                                Al momento non ci sono ordini pronti per il ritiro in quest'area.
                            </p>
                            <p className="text-sm text-muted-foreground mt-1">
                                La lista si aggiornerà automaticamente.
                            </p>
                        </CardContent>
                    </Card>
                ) : (
                    <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4 md:gap-6">
                        {orders.map(renderOrderCard)}
                    </div>
                )}
            </main>
            <footer className="flex-shrink-0 h-[35vh] bg-transparent">
                <AdCarousel mediaItems={adMediaItems} />
            </footer>
        </div>
    );
}
