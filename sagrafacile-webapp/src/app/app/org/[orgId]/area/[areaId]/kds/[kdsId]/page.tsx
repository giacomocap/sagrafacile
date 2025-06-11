'use client';

import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useParams } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';
import apiClient from '@/services/apiClient';
import { KdsOrderDto, KdsStatus } from '@/types'; // Import KdsStatus, KdsOrderItemDto removed
import useSignalRHub from '@/hooks/useSignalRHub';
import { toast } from 'sonner';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge'; // Import Badge
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import { Terminal, CheckCircle2, History } from 'lucide-react'; // Import CheckCircle2, History
import { KdsOrderDetailDialogCloseResult } from '@/types'; // Import the result type from types
// Import the Order Detail Dialog
import KdsOrderDetailDialog from '@/components/kds/KdsOrderDetailDialog';
// Import the new dialog
import KdsCompletedOrdersDialog from '@/components/kds/KdsCompletedOrdersDialog';
import NoDayOpenOverlay from '@/components/NoDayOpenOverlay'; // Import the overlay component

// KdsInterfacePageParams removed as it's unused

const KdsInterfacePage = () => {
    const params = useParams();
    // Safely extract parameters, ensuring they are strings
    const orgId = typeof params.orgId === 'string' ? params.orgId : '';
    const areaId = typeof params.areaId === 'string' ? params.areaId : '';
    const kdsId = typeof params.kdsId === 'string' ? params.kdsId : '';
    useAuth(); // user removed
    const [orders, setOrders] = useState<KdsOrderDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selectedOrder, setSelectedOrder] = useState<KdsOrderDto | null>(null);
    const [isDetailDialogOpen, setIsDetailDialogOpen] = useState(false);
    const [isCompletedOrdersDialogOpen, setIsCompletedOrdersDialogOpen] = useState(false); // State for completed orders dialog
    // Ref to track if connection succeeded at least once in the effect lifecycle
    const connectionSucceededRef = useRef(false);

    const hubUrl = `${process.env.NEXT_PUBLIC_API_BASE_URL}/orderHub`;
    // Destructure startConnection and stopConnection from the hook
    const { connection, connectionStatus, startConnection, stopConnection } = useSignalRHub(hubUrl);

    const fetchOrders = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const response = await apiClient.get<KdsOrderDto[]>(`/orders/kds-station/${kdsId}`);
            setOrders(response.data);
        } catch (err: unknown) {
            console.error("Error fetching KDS orders:", err);
            const error = err as { message?: string };
            setError(error.message || 'Failed to fetch orders.');
            toast.error('Errore nel caricare gli ordini KDS.');
        } finally {
            setLoading(false);
        }
    }, [kdsId]);

    useEffect(() => {
        fetchOrders();
     }, [fetchOrders]);

    // --- SignalR Connection Management ---
     useEffect(() => {
        connectionSucceededRef.current = false; // Reset on effect run
        console.log("KDS Page Effect: Starting connection...");
        startConnection();

        // Stop the connection ONLY if it successfully connected during this effect run
        return () => {
            console.log(`KDS Page Effect Cleanup: Connection succeeded during run? ${connectionSucceededRef.current}`);
            if (connectionSucceededRef.current) {
                 console.log("KDS Page Effect Cleanup: Stopping connection.");
                 stopConnection();
            } else {
                 console.log("KDS Page Effect Cleanup: Skipping stopConnection as connection didn't succeed during this run.");
                 // If the connection is somehow still connecting/disconnecting from a PREVIOUS effect run
                 // the hook's internal cleanup might still catch it, but this prevents the immediate stop.
            }
        };
    }, [startConnection, stopConnection]); // Depend on the stable hook functions

    // Effect to track if connection succeeds
    useEffect(() => {
        if (connectionStatus === 'Connected') {
            connectionSucceededRef.current = true;
            console.log("KDS Page Effect: Connection status is Connected, setting ref.");
        }
    }, [connectionStatus]);

     // --- SignalR Event Handlers ---
     useEffect(() => {
         console.log(`[SignalR Handlers Effect] Running. Connection state: ${connection?.state}, Status: ${connectionStatus}`);
         // Only setup listeners if connection exists and is connected
         if (!connection || connectionStatus !== 'Connected') {
             return;
         }

         console.log(`[SignalR Handlers Effect] Setting up listeners for connection ID: ${connection.connectionId}`);
         // Listen for new orders/items relevant to this KDS
        const handleOrderPreparing = (orderId: string, receivedAreaId: string) => {
            console.log(`SignalR: OrderPreparing received for order ${orderId} in area ${receivedAreaId}`);
            // Refetch orders if the area matches, converting received ID to string for comparison
            // More sophisticated logic could involve adding/updating specific orders
            if (String(receivedAreaId) === areaId) { // Convert receivedAreaId to string
                toast.info(`Nuovo ordine (${orderId.substring(0, 8)}...) in preparazione.`);
                fetchOrders();
            } else {
                 console.log(`[handleOrderPreparing] Area mismatch. Skipping fetchOrders.`); // Add log for mismatch case
            }
        };

        // Listen for status updates (e.g., when an order becomes ReadyForPickup elsewhere)
        const handleOrderStatusUpdate = (orderId: string, status: string) => {
            console.log(`SignalR: OrderStatusUpdate received for order ${orderId}, status: ${status}`);
            // Potentially remove order from list if status changes significantly,
            // but KDSArchitecture suggests removal happens when *this* KDS confirms its items.
            // For now, maybe just refetch to be safe or update specific order status if needed.
            // Let's refetch for simplicity for now.
             const orderExists = orders.some(o => o.orderId === orderId); // Use orderId
             if (orderExists) {
                 fetchOrders();
             }
        };

        connection.on('orderpreparing', handleOrderPreparing); // Match lowercase name from warning
        connection.on('OrderStatusUpdate', handleOrderStatusUpdate);

        // Cleanup listeners on component unmount or connection change
        return () => {
            console.log(`[SignalR Handlers Effect Cleanup] Removing listeners for connection ID: ${connection?.connectionId}`);
            // Check connection exists before calling off, though it should if we entered the effect setup
            connection?.off('orderpreparing', handleOrderPreparing); // Match lowercase name
            connection?.off('OrderStatusUpdate', handleOrderStatusUpdate);
         };
     }, [connection, connectionStatus, areaId, fetchOrders, orders]); // Added orders dependency for handleOrderStatusUpdate

     const handleOrderSelect = (order: KdsOrderDto) => {
        setSelectedOrder(order);
        setIsDetailDialogOpen(true);
    };

    const handleDialogClose = (result: KdsOrderDetailDialogCloseResult) => {
        console.log('[handleDialogClose] Called with result:', result); // DIAGNOSTIC LOG
        setIsDetailDialogOpen(false);
        const closedOrderId = selectedOrder?.orderId; // Store the ID before clearing selectedOrder
        setSelectedOrder(null);
        console.log(`[handleDialogClose] Closed dialog for order ID: ${closedOrderId}`); // DIAGNOSTIC LOG

        if (result.completed) {
            console.log('[handleDialogClose] Result indicates completion. Refetching orders.'); // DIAGNOSTIC LOG
            // Order was fully confirmed, refetch to remove it from the list
            fetchOrders();
        } else if (result.updatedStatuses && closedOrderId) {
            console.log('[handleDialogClose] Result indicates updates without completion. Updating local state.'); // DIAGNOSTIC LOG
            // Order was not completed, but items were updated. Update local state.
            setOrders(prevOrders =>
                prevOrders.map(order => {
                    if (order.orderId === closedOrderId) {
                        // Create a new order object with updated item statuses
                        const updatedItems = order.items.map(item => ({
                            ...item,
                            kdsStatus: result.updatedStatuses![item.orderItemId] ?? item.kdsStatus,
                        }));
                        return { ...order, items: updatedItems };
                    }
                    return order;
                })
            );
        } else {
            console.log('[handleDialogClose] Result indicates no completion and no updates.'); // DIAGNOSTIC LOG
        }
        // If completed is false and no updatedStatuses, do nothing (dialog closed without changes)
    };

    return (
        <NoDayOpenOverlay> {/* Wrap the entire page content */}
            <div className="container mx-auto p-4 h-full"> {/* Ensure container takes height */}
                <div className="flex justify-between items-center mb-4">
                    <h1 className="text-2xl font-bold">KDS Station: {kdsId}</h1>
                    <Button variant="outline" onClick={() => setIsCompletedOrdersDialogOpen(true)}>
                    <History className="mr-2 h-4 w-4" />
                    Storico Completati
                </Button>
            </div>
            <p className="mb-4">Connection Status: {connectionStatus}</p> {/* Use connectionStatus */}

            {loading && (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4"> {/* Added xl */}
                    {[...Array(3)].map((_, i) => (
                        <Card key={i}>
                            <CardHeader>
                                <Skeleton className="h-6 w-3/4" />
                            </CardHeader>
                            <CardContent>
                                <Skeleton className="h-4 w-full mb-2" />
                                <Skeleton className="h-4 w-1/2" />
                            </CardContent>
                        </Card>
                    ))}
                </div>
            )}

            {error && (
                <Alert variant="destructive">
                    <Terminal className="h-4 w-4" />
                    <AlertTitle>Error</AlertTitle>
                    <AlertDescription>{error}</AlertDescription>
                </Alert>
            )}

            {!loading && !error && (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                    {orders.length === 0 ? (
                        <p>Nessun ordine attivo per questa postazione.</p>
                    ) : (
                        orders.map((order) => (
                            <Card key={order.orderId} className="cursor-pointer hover:shadow-lg transition-shadow flex flex-col" onClick={() => handleOrderSelect(order)}>
                                <CardHeader className="pb-2"> {/* Reduced bottom padding */}
                                    <CardTitle className="text-lg"> {/* Slightly smaller title */}
                                        Ordine: {order.displayOrderNumber ?? order.orderId.substring(0, 6)}
                                    </CardTitle>
                                    {order.customerName && (
                                        <p className="text-sm text-muted-foreground">{order.customerName}</p>
                                    )}
                                    <p className="text-base font-semibold"> {/* Larger font for table */}
                                        Tavolo: {order.tableNumber || 'N/A'}
                                    </p>
                                    <p className="text-xs text-muted-foreground pt-1"> {/* Smaller time */}
                                        {new Date(order.orderDateTime).toLocaleTimeString()}
                                        {/* Optionally, show original ID if displayOrderNumber is present and different, for reference */}
                                        {order.displayOrderNumber && <span className="ml-2">(ID: {order.orderId.substring(0, 6)}...)</span>}
                                    </p>
                                </CardHeader>
                                <CardContent className="pt-2 flex-grow flex flex-col justify-between"> {/* Reduced top padding, allow content to grow, flex col */}
                                    <div> {/* Wrapper for item count */}
                                        {(() => {
                                            const totalItems = order.items.length;
                                            const confirmedItems = order.items.filter(item => item.kdsStatus === KdsStatus.Confirmed).length;
                                            // allConfirmed removed as it's unused here

                                            return (
                                                <p className="text-sm mb-2">
                                                    {confirmedItems} / {totalItems} articoli pronti
                                                </p>
                                            );
                                        })()}
                                    </div>
                                    {(() => {
                                         const totalItems = order.items.length;
                                         const confirmedItems = order.items.filter(item => item.kdsStatus === KdsStatus.Confirmed).length;
                                         const allConfirmed = confirmedItems === totalItems;
                                         if (allConfirmed && totalItems > 0) {
                                             return (
                                                 <Badge variant="outline" className="mt-auto bg-yellow-100 text-yellow-800 border-yellow-300 self-start">
                                                     <CheckCircle2 className="mr-1 h-3 w-3" />
                                                     Pronto per Conferma
                                                 </Badge>
                                             );
                                         }
                                         return null;
                                    })()}
                                </CardContent>
                            </Card>
                        ))
                    )}
                </div>
            )}

            {selectedOrder && (
                <KdsOrderDetailDialog
                    isOpen={isDetailDialogOpen}
                    onClose={handleDialogClose} // Pass the updated handler
                    order={selectedOrder}
                    kdsStationId={kdsId} // Pass the kdsId from params
                />
            )}

            {/* Completed Orders Dialog */}
            <KdsCompletedOrdersDialog
                isOpen={isCompletedOrdersDialogOpen}
                onClose={() => setIsCompletedOrdersDialogOpen(false)}
                kdsStationId={kdsId}
                areaId={areaId}
                orgId={orgId}
            />
            </div>
        </NoDayOpenOverlay>
    );
};

export default KdsInterfacePage;
