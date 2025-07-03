'use client';

import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { useParams } from 'next/navigation';
import { useOrganization } from '@/contexts/OrganizationContext';
import { useAuth } from '@/contexts/AuthContext';
import * as dayService from '@/services/dayService';
import { OrderDto, DayDto, DayStatus, PrinterDto, ReprintType, OrderQueryParameters, PaginatedResult, OrderStatus as OrderStatusEnum } from '@/types';
import printerService from '@/services/printerService';
import orderService from '@/services/orderService';
import apiClient from '@/services/apiClient'; // Import apiClient
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Label } from "@/components/ui/label";
import { Loader2, Printer } from 'lucide-react';
import { toast } from 'sonner';
import ReceiptDialog from '@/components/ReceiptDialog';
import PaginatedTable from '@/components/common/PaginatedTable';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import AdminAreaSelector from '@/components/shared/AdminAreaSelector';

const OrdersPage = () => {
    const { orgId } = useParams();
    const { selectedOrganizationId, isLoadingOrgs, currentDay, isLoadingDay } = useOrganization();
    const { isLoading: isAuthLoading } = useAuth();
    const [days, setDays] = useState<DayDto[]>([]);
    const [isLoadingDays, setIsLoadingDays] = useState(true);
    const [printers, setPrinters] = useState<PrinterDto[]>([]);
    const [isLoadingPrinters, setIsLoadingPrinters] = useState(true);

    const [paginatedOrders, setPaginatedOrders] = useState<PaginatedResult<OrderDto> | null>(null);
    const [isLoadingOrders, setIsLoadingOrders] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const [queryParams, setQueryParams] = useState<OrderQueryParameters>({
        page: 1,
        pageSize: 20,
        sortBy: 'orderDateTime',
        sortAscending: false,
        areaId: undefined,
        dayId: 'current',
        organizationId: selectedOrganizationId || undefined,
    });

    // State for Receipt Dialog
    const [isReceiptDialogOpen, setIsReceiptDialogOpen] = useState(false);
    const [selectedOrderForReprint, setSelectedOrderForReprint] = useState<OrderDto | null>(null);

    useEffect(() => {
        if (selectedOrganizationId) {
            setQueryParams(prev => ({ ...prev, organizationId: selectedOrganizationId }));
        }
    }, [selectedOrganizationId]);

    const handleQueryChange = (newParams: Partial<OrderQueryParameters>) => {
        setQueryParams(prev => ({ ...prev, ...newParams, page: 1 }));
    };

    const handleAreaChange = (areaId: string | undefined) => {
        handleQueryChange({ areaId: areaId ? Number(areaId) : undefined });
    };

    const handleDayChange = (dayId: string) => {
        handleQueryChange({ dayId: dayId === 'current' ? 'current' : Number(dayId) });
    };

    // Effect to fetch days when organization context is ready
    useEffect(() => {
        const fetchDaysData = async () => {
            if (!selectedOrganizationId || isLoadingOrgs || isAuthLoading) {
                return; // Wait for context
            }
            setIsLoadingDays(true);
            setDays([]); // Clear previous days
            try {
                const fetchedDays = await dayService.getDays(); // Assuming getDays fetches for the current org context
                // Sort days descending by start time for the dropdown
                const sortedDays = (fetchedDays || []).sort((a, b) => new Date(b.startTime).getTime() - new Date(a.startTime).getTime());
                setDays(sortedDays);
            } catch (error) {
                console.error("Error fetching days:", error);
                toast.error("Impossibile caricare l'elenco delle giornate.");
                setDays([]);
            } finally {
                setIsLoadingDays(false);
            }
        };
        fetchDaysData();
    }, [selectedOrganizationId, isLoadingOrgs, isAuthLoading]);

    // Effect to fetch printers when organization context is ready
    useEffect(() => {
        const fetchPrintersData = async () => {
            if (!selectedOrganizationId || isLoadingOrgs || isAuthLoading) {
                return; // Wait for context
            }
            setIsLoadingPrinters(true);
            setPrinters([]);
            try {
                const fetchedPrinters = await printerService.getPrinters(); // Assuming getPrinters fetches for the current org context
                setPrinters(fetchedPrinters || []);
            } catch (error) {
                console.error("Error fetching printers:", error);
                toast.error("Impossibile caricare l'elenco delle stampanti.");
                setPrinters([]);
            } finally {
                setIsLoadingPrinters(false);
            }
        };
        fetchPrintersData();
    }, [selectedOrganizationId, isLoadingOrgs, isAuthLoading]);

    const fetchOrders = useCallback(async () => {
        if (!queryParams.organizationId || !queryParams.areaId) {
            setPaginatedOrders(null);
            return;
        }
        setIsLoadingOrders(true);
        setError(null);

        // Create a new object for the API call to handle the 'current' dayId case
        const apiParams: OrderQueryParameters = { ...queryParams };
        if (apiParams.dayId === 'current') {
            // The backend will interpret a missing dayId as 'current open day'
            delete apiParams.dayId;
        }

        try {
            const data = await orderService.getOrders(apiParams);
            setPaginatedOrders(data);
        } catch (err) {
            console.error("Error fetching orders:", err);
            setError("Impossibile caricare gli ordini.");
            toast.error("Errore", { description: "Impossibile caricare gli ordini." });
        } finally {
            setIsLoadingOrders(false);
        }
    }, [queryParams]);

    useEffect(() => {
        const handler = setTimeout(() => {
            fetchOrders();
        }, 300); // Debounce fetching
        return () => clearTimeout(handler);
    }, [fetchOrders]);

    const handleOpenReprintDialog = (order: OrderDto) => {
        // This logic might need adjustment if guest/takeaway charges are not on the order DTO
        // For now, we assume they might be, or we can add them if needed.
        const augmentedOrder = { ...order };
        setSelectedOrderForReprint(augmentedOrder);
        setIsReceiptDialogOpen(true);
    };

    const handleCloseReceiptDialog = (success?: boolean) => {
        setIsReceiptDialogOpen(false);
        setSelectedOrderForReprint(null);
        if (success) {
            // Optionally refetch orders or update UI if needed after a successful reprint action
            // For now, just closing the dialog.
        }
    };

    const handleInitiateAdminReprint = async (
        orderId: string,
        reprintJobType: ReprintType,
        printerId?: number // Optional printerId for admin reprints
    ): Promise<{ success: boolean; message?: string }> => {
        interface ReprintPayload {
            reprintJobType: ReprintType;
            printerId?: number;
        }
        try {
            const payload: ReprintPayload = { reprintJobType };
            if (printerId) {
                payload.printerId = printerId;
            }
            // Assuming the endpoint might be /orders/{orderId}/reprint or similar
            // The backend needs to handle the optional printerId
            const response = await apiClient.post<{ message?: string }>(`/orders/${orderId}/reprint`, payload);
            return { success: true, message: response.data.message || "Ristampa avviata con successo." };
        } catch (err: unknown) {
            console.error("Error initiating admin reprint:", err);
            const errorResponse = (err as { response?: { data?: { message?: string } } });
            return { success: false, message: errorResponse.response?.data?.message || "Errore durante l'avvio della ristampa." };
        }
    };

    const columns = useMemo(() => [
        { key: 'displayOrderNumber', label: 'ID Ordine', sortable: true },
        { key: 'orderDateTime', label: 'Data/Ora', sortable: true },
        { key: 'areaName', label: 'Area', sortable: true },
        { key: 'totalAmount', label: 'Totale', sortable: true },
        { key: 'paymentMethod', label: 'Metodo Pagamento', sortable: true },
        { key: 'status', label: 'Stato', sortable: true },
        { key: 'tableNumber', label: 'Tavolo', sortable: true },
    ], []);

    const formatCurrency = (amount: number) => new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' }).format(amount);
    const formatDate = (dateString: string) => new Date(dateString).toLocaleString('it-IT');

    const renderCell = (order: OrderDto, columnKey: string) => {
        switch (columnKey) {
            case 'displayOrderNumber':
                return <span className="font-medium">{order.displayOrderNumber ?? order.id}</span>;
            case 'orderDateTime':
                return formatDate(order.orderDateTime);
            case 'areaName':
                return order.areaName;
            case 'totalAmount':
                return <div className="text-right">{formatCurrency(order.totalAmount)}</div>;
            case 'paymentMethod':
                return <Badge variant={order.paymentMethod === 'Contanti' ? 'secondary' : 'default'}>{order.paymentMethod || 'N/D'}</Badge>;
            case 'status':
                return <Badge>{OrderStatusEnum[order.status] ?? 'Sconosciuto'}</Badge>;
            case 'tableNumber':
                return order.tableNumber || 'N/A';
            default:
                return null;
        }
    };

    const renderActions = (order: OrderDto) => (
        <Button variant="outline" size="sm" onClick={() => handleOpenReprintDialog(order)}>
            <Printer className="mr-2 h-4 w-4" /> Ristampa
        </Button>
    );

    if (isLoadingOrgs || isAuthLoading || isLoadingDay || isLoadingPrinters) {
        return <div className="flex justify-center items-center h-[calc(100vh-150px)]"><Loader2 className="h-16 w-16 animate-spin" /></div>;
    }

    return (
        <>
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle>Storico Ordini</CardTitle>
                    {/* Filter Selectors */}
                    <div className="flex items-center space-x-4">
                        <div className="flex items-center space-x-2">
                            <Label htmlFor="day-select">Giornata:</Label>
                            {isLoadingDays || isLoadingDay ? <Loader2 className="h-5 w-5 animate-spin" /> : (
                                <Select value={String(queryParams.dayId)} onValueChange={handleDayChange}>
                                    <SelectTrigger id="day-select" className="w-[220px]"><SelectValue placeholder="Seleziona Giornata" /></SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="current">{currentDay ? `Corrente (${new Date(currentDay.startTime).toLocaleDateString('it-IT')})` : 'Corrente (Nessuna Aperta)'}</SelectItem>
                                        {days.map((day: DayDto) => (<SelectItem key={day.id} value={String(day.id)}>{new Date(day.startTime).toLocaleDateString('it-IT')} {day.status === DayStatus.Closed ? '(Chiusa)' : ''}</SelectItem>))}
                                        {days.length === 0 && !isLoadingDays && <SelectItem value="" disabled>Nessuna giornata storica</SelectItem>}
                                    </SelectContent>
                                </Select>
                            )}
                        </div>
                    </div>
                </CardHeader>
                <CardContent className="space-y-4">
                    <AdminAreaSelector
                        selectedAreaId={queryParams.areaId?.toString()}
                        onAreaChange={handleAreaChange}
                        title="Filtra per Area"
                        description="Seleziona un'area per visualizzare i relativi ordini."
                    />
                    {queryParams.areaId && (
                        <PaginatedTable
                            storageKey={`orders_${orgId}_${queryParams.areaId}`}
                            columns={columns}
                            paginatedData={paginatedOrders}
                            isLoading={isLoadingOrders}
                            error={error}
                            queryParams={queryParams}
                            onQueryChange={(newParams) => setQueryParams(prev => ({ ...prev, ...newParams }))}
                            renderCell={renderCell}
                            renderActions={renderActions}
                            itemKey={(order) => order.id}
                        />
                    )}
                </CardContent>
            </Card>
            {selectedOrderForReprint && (
                <ReceiptDialog
                    isOpen={isReceiptDialogOpen}
                    onClose={handleCloseReceiptDialog}
                    order={selectedOrderForReprint}
                    isReprintMode={true}
                    onSubmitOrder={async () => null} // Not used in reprint mode for admin
                    onInitiateReprint={(orderId, reprintJobType, printerId) => // Ensure this matches ReceiptDialog's expected signature
                        handleInitiateAdminReprint(orderId, reprintJobType, printerId)
                    }
                    availablePrinters={printers} // Pass available printers
                />
            )}
        </>
    );
};

export default OrdersPage;
