'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ScanLine, List, Hourglass, RefreshCw, AlertCircle } from 'lucide-react';
import apiClient from '@/services/apiClient';
import { OrderDto, OrderStatus, PaginatedResult } from '@/types';
import NoDayOpenOverlay from '@/components/NoDayOpenOverlay';
import { useParams } from 'next/navigation';
import OrderQrScanner from '@/components/shared/OrderQrScanner';
import OrderConfirmationView from '@/components/shared/OrderConfirmationView';

const WaiterPage = () => {
    const params = useParams();
    const areaId = params.areaId as string;
    const [showScanner, setShowScanner] = useState(false);
    const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
    const [viewMode, setViewMode] = useState<'tabs' | 'confirm'>('tabs');

    const [pendingOrders, setPendingOrders] = useState<OrderDto[]>([]);
    const [activeOrders, setActiveOrders] = useState<OrderDto[]>([]);
    const [isLoadingPending, setIsLoadingPending] = useState(true);
    const [isLoadingActive, setIsLoadingActive] = useState(true);
    const [errorPending, setErrorPending] = useState<string | null>(null);
    const [errorActive, setErrorActive] = useState<string | null>(null);

    const fetchOrders = useCallback(async () => {
        if (!areaId) return;
        setIsLoadingPending(true);
        setIsLoadingActive(true);
        setErrorPending(null);
        setErrorActive(null);

        try {
            const pendingResponse = await apiClient.get<PaginatedResult<OrderDto>>('/orders', {
                params: { statuses: [OrderStatus.Paid, OrderStatus.PreOrder], areaId }
            });
            setPendingOrders(pendingResponse.data.items);
        } catch (err: any) {
            console.error("Failed to fetch pending orders:", err);
            setErrorPending(err.response?.data?.message || err.message || 'Errore nel caricamento ordini da confermare.');
        } finally {
            setIsLoadingPending(false);
        }

        try {
            const activeResponse = await apiClient.get<PaginatedResult<OrderDto>>('/orders', {
                params: { statuses: [OrderStatus.Preparing, OrderStatus.ReadyForPickup], areaId }
            });
            setActiveOrders(activeResponse.data.items);
        } catch (err: any) {
            console.error("Failed to fetch active orders:", err);
            setErrorActive(err.response?.data?.message || err.message || 'Errore nel caricamento ordini in corso.');
        } finally {
            setIsLoadingActive(false);
        }
    }, [areaId]);

    useEffect(() => {
        fetchOrders();
    }, [fetchOrders]);

    useEffect(() => {
        if (viewMode === 'confirm') {
            window.scrollTo(0, 0);
        }
    }, [viewMode]);

    const handleScanSuccess = (orderId: string) => {
        setSelectedOrderId(orderId);
        setViewMode('confirm');
    };

    const handleSelectOrderToConfirm = (orderId: string) => {
        setSelectedOrderId(orderId);
        setViewMode('confirm');
    };

    const handleCancelConfirmation = () => {
        setSelectedOrderId(null);
        setViewMode('tabs');
        fetchOrders();
    };

    const OrderList = ({ orders, isLoading, error, onSelectOrder, onRefresh }: {
        title: string;
        orders: OrderDto[];
        isLoading: boolean;
        error: string | null;
        onSelectOrder: (orderId: string) => void;
        onRefresh: () => void;
    }) => {
        if (isLoading) {
            return (
                <div className="space-y-2">
                    <Skeleton className="h-8 w-1/2" />
                    <Skeleton className="h-10 w-full" />
                    <Skeleton className="h-10 w-full" />
                    <Skeleton className="h-10 w-full" />
                </div>
            );
        }

        if (error) {
            return (
                <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Errore Caricamento</AlertTitle>
                    <AlertDescription>
                        {error}
                        <Button variant="ghost" size="sm" onClick={onRefresh} className="ml-2">
                            <RefreshCw className="h-4 w-4 mr-1" /> Riprova
                        </Button>
                    </AlertDescription>
                </Alert>
            );
        }

        if (orders.length === 0) {
            return <p className="text-muted-foreground text-center py-4">Nessun ordine in questa categoria.</p>;
        }

        return (
            <div className="space-y-2">
                {orders.map((order) => (
                    <Button
                        key={order.id}
                        variant="outline"
                        className="w-full h-auto justify-between items-center p-3 text-left"
                        onClick={() => onSelectOrder(order.id)}
                    >
                        <div className="flex-1 overflow-hidden">
                            <p className="font-semibold truncate">Ordine: {order.displayOrderNumber ?? order.id}</p>
                            {order.displayOrderNumber && <p className="text-xs text-muted-foreground truncate">(ID Interno: {order.id})</p>}
                            <p className="text-sm text-muted-foreground truncate">Cliente: {order.customerName || 'N/A'}</p>
                            {order.tableNumber && <p className="text-sm text-muted-foreground">Tavolo: {order.tableNumber}</p>}
                            <p className="text-xs text-muted-foreground">
                                {new Date(order.orderDateTime).toLocaleDateString('it-IT', { day: '2-digit', month: '2-digit' })} {new Date(order.orderDateTime).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}
                            </p>
                        </div>
                        <div className="ml-2 text-right">
                            <p className="font-semibold">€{order.totalAmount.toFixed(2)}</p>
                            <span className={`text-xs px-2 py-0.5 rounded-full ${order.status === OrderStatus.Paid || order.status === OrderStatus.PreOrder ? 'bg-yellow-100 text-yellow-800' :
                                    order.status === OrderStatus.Preparing ? 'bg-blue-100 text-blue-800' :
                                        order.status === OrderStatus.ReadyForPickup ? 'bg-green-100 text-green-800' :
                                            'bg-gray-100 text-gray-800'
                                }`}>
                                {OrderStatus[order.status] ?? 'Sconosciuto'}
                            </span>
                        </div>
                    </Button>
                ))}
            </div>
        );
    };

    if (viewMode === 'confirm' && selectedOrderId) {
        return (
            <div className="container mx-auto p-0 sm:p-4 flex flex-col items-center">
                <OrderConfirmationView
                    orderId={selectedOrderId}
                    onCancel={handleCancelConfirmation}
                    onSuccess={handleCancelConfirmation}
                />
            </div>
        );
    }

    return (
        <NoDayOpenOverlay>
            <div className="container mx-auto p-4 flex flex-col space-y-4 h-full">
                <h1 className="text-2xl font-bold text-center">Interfaccia Cameriere</h1>
                
                <Tabs defaultValue="scan" className="w-full max-w-lg mx-auto">
                    <TabsList className="grid w-full grid-cols-1 sm:grid-cols-3 h-auto sm:h-10">
                        <TabsTrigger value="scan" className="py-2 sm:py-1">
                            <ScanLine className="mr-1 h-4 w-4" /> Scansiona
                        </TabsTrigger>
                        <TabsTrigger value="confirm" className="py-2 sm:py-1">
                            <List className="mr-1 h-4 w-4" /> Da Confermare ({pendingOrders.length})
                        </TabsTrigger>
                        <TabsTrigger value="active" className="py-2 sm:py-1">
                            <Hourglass className="mr-1 h-4 w-4" /> In Corso ({activeOrders.length})
                        </TabsTrigger>
                    </TabsList>

                    <TabsContent value="scan" className="pt-4">
                        <OrderQrScanner
                            showScanner={showScanner}
                            onShowScannerChange={setShowScanner}
                            onScanSuccess={handleScanSuccess}
                        />
                    </TabsContent>

                    <TabsContent value="confirm" className="pt-4">
                        <OrderList
                            title="Ordini da Confermare"
                            orders={pendingOrders}
                            isLoading={isLoadingPending}
                            error={errorPending}
                            onSelectOrder={handleSelectOrderToConfirm}
                            onRefresh={fetchOrders}
                        />
                    </TabsContent>

                    <TabsContent value="active" className="pt-4">
                        <OrderList
                            title="Ordini in Corso / Pronti"
                            orders={activeOrders}
                            isLoading={isLoadingActive}
                            error={errorActive}
                            onSelectOrder={handleSelectOrderToConfirm}
                            onRefresh={fetchOrders}
                        />
                    </TabsContent>
                </Tabs>
            </div>
        </NoDayOpenOverlay>
    );
};

export default WaiterPage;
