'use client';

import React, { useState, useEffect } from 'react';
import { useParams } from 'next/navigation';
import { useOrganization } from '@/contexts/OrganizationContext';
import { useAuth } from '@/contexts/AuthContext';
import apiClient from '@/services/apiClient';
import * as dayService from '@/services/dayService'; // Import day service
import { OrderDto, AreaDto, DayDto, DayStatus, PrinterDto, ReprintType } from '@/types'; // Added AreaDto, DayDto, DayStatus, PrinterDto
import printerService from '@/services/printerService'; // Import printerService
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "@/components/ui/select";
import { Label } from "@/components/ui/label";
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import OrderTable from '@/components/OrderTable';
import ReceiptDialog from '@/components/ReceiptDialog'; // Import ReceiptDialog

const OrdersPage = () => {
    useParams(); // Called for potential side effects, if any, or to satisfy lint if it complains about unused import directly
    const { selectedOrganizationId, isLoadingOrgs, currentDay, isLoadingDay } = useOrganization(); // Added currentDay, isLoadingDay
    const { user, isLoading: isAuthLoading } = useAuth();
    const [areas, setAreas] = useState<AreaDto[]>([]);
    const [selectedAreaId, setSelectedAreaId] = useState<string>('');
    const [isLoadingAreas, setIsLoadingAreas] = useState(true);
    const [days, setDays] = useState<DayDto[]>([]); // State for days
    const [selectedDayId, setSelectedDayId] = useState<string>('current'); // State for selected day ID ('current', 'all', or specific ID) - Default to 'current'
    const [isLoadingDays, setIsLoadingDays] = useState(true); // State for loading days
    const [orders, setOrders] = useState<OrderDto[]>([]);
    const [isLoadingOrders, setIsLoadingOrders] = useState(false);
    const [printers, setPrinters] = useState<PrinterDto[]>([]); // State for printers
    const [isLoadingPrinters, setIsLoadingPrinters] = useState(true); // State for loading printers

    // State for Receipt Dialog
    const [isReceiptDialogOpen, setIsReceiptDialogOpen] = useState(false);
    const [selectedOrderForReprint, setSelectedOrderForReprint] = useState<OrderDto | null>(null);

    // Effect to fetch areas when organization context is ready
    useEffect(() => {
        const fetchAreas = async () => {
            if (!selectedOrganizationId || isLoadingOrgs || isAuthLoading) {
                return; // Wait for context
            }
            setIsLoadingAreas(true);
            setSelectedAreaId(''); // Reset selection
            setOrders([]); // Clear orders when org changes
            try {
                // Fetch areas accessible by the user (implicitly filtered by org via backend logic/auth)
                const response = await apiClient.get<AreaDto[]>('/Areas');
                const accessibleAreas = response.data || [];
                setAreas(accessibleAreas);
                if (accessibleAreas.length > 0) {
                    setSelectedAreaId(accessibleAreas[0].id.toString()); // Default to first area
                } else {
                    toast.info("Nessuna area trovata per questa organizzazione.");
                }
            } catch (error) {
                console.error("Error fetching areas:", error);
                toast.error("Impossibile caricare le aree.");
                setAreas([]);
            } finally {
                setIsLoadingAreas(false);
            }
        };
        fetchAreas();
    }, [selectedOrganizationId, isLoadingOrgs, isAuthLoading]); // Keep dependencies as is

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

    // Effect to fetch orders when filters (area, day) or context changes
    useEffect(() => {
        const fetchOrders = async () => {
            // Wait for context and dependent data (areas, days) to be loaded.
            if (isLoadingAreas || isLoadingDays || isLoadingDay || isLoadingOrgs || isAuthLoading) {
                setOrders([]);
                setIsLoadingOrders(false); // Ensure loading state is false if prerequisites aren't met
                return;
            }

            // Determine the actual dayId to query based on selection
            let dayIdParam: number | null = null;
            if (selectedDayId === 'current') {
                // If 'current' is selected, send the currentDay ID if it exists.
                // If currentDay is null, send nothing (backend defaults to current open day or empty list).
                if (currentDay) {
                    dayIdParam = currentDay.id;
                }
                // else dayIdParam remains null
            } else {
                // It's a specific numeric ID string for a historical day
                const parsedDayId = parseInt(selectedDayId, 10);
                if (!isNaN(parsedDayId)) {
                    dayIdParam = parsedDayId;
                }
            }

            // Prevent fetching if SuperAdmin hasn't selected an org
            if (user?.roles?.includes('SuperAdmin') && !selectedOrganizationId) {
                toast.info("SuperAdmin: Seleziona un'organizzazione per vedere gli ordini.");
                setOrders([]);
                setIsLoadingOrders(false);
                return;
            }

            setIsLoadingOrders(true);
            setOrders([]); // Clear previous orders

            // Build query parameters
            const queryParams = new URLSearchParams();
            if (user?.roles?.includes('SuperAdmin') && selectedOrganizationId) {
                queryParams.append('organizationId', selectedOrganizationId.toString());
            }
            if (selectedAreaId) { // Only add areaId if it's selected
                queryParams.append('areaId', selectedAreaId);
            }
            if (dayIdParam !== null) { // Add dayId ONLY if we determined a specific one to send
                queryParams.append('dayId', dayIdParam.toString());
            }
            // Note: If dayIdParam is null (e.g., 'current' selected but no day open),
            // the backend default behavior applies (returns empty list as no day is open).

            try {
                const url = `/Orders?${queryParams.toString()}`;
                console.log("Fetching orders with URL:", url); // Debug log
                const response = await apiClient.get<OrderDto[]>(url);
                setOrders(response.data || []);
                if (!response.data || response.data.length === 0) {
                    // Adjust message based on filters
                    let filterDesc = selectedAreaId ? `nell'area selezionata` : `nell'organizzazione`;
                    if (selectedDayId === 'current') {
                        filterDesc += currentDay ? ` per la giornata corrente` : ` (nessuna giornata aperta)`;
                    } else {
                        // Find the selected day's date for a better message
                        const selectedDay = days.find(d => d.id.toString() === selectedDayId);
                        const dateString = selectedDay ? new Date(selectedDay.startTime).toLocaleDateString('it-IT') : 'selezionata';
                        filterDesc += ` per la giornata del ${dateString}`;
                    }
                    toast.info(`Nessun ordine trovato ${filterDesc}.`);
                }
            } catch (error: unknown) {
                console.error(`Error fetching orders with params ${queryParams.toString()}:`, error);
                const errorResponse = (error as { response?: { data?: { message?: string } }, message?: string });
                const errorMsg = errorResponse.response?.data?.message || errorResponse.message || "Errore sconosciuto";
                toast.error(`Impossibile caricare gli ordini: ${errorMsg}`);
                setOrders([]); // Clear orders on error
            } finally {
                setIsLoadingOrders(false);
            }
        };

        fetchOrders();
    }, [
        selectedOrganizationId,
        selectedAreaId,
        selectedDayId, // Add dependency
        currentDay, // Add dependency
        isLoadingAreas,
        isLoadingDays, // Add dependency
        isLoadingDay, // Add dependency
        isLoadingOrgs,
        isAuthLoading,
        user,
        days // Added missing dependency
    ]);

    const handleOpenReprintDialog = (order: OrderDto) => {
        // Find the area associated with the order to get charge rules
        const orderArea = areas.find(a => a.id === order.areaId);

        // Calculate charges, similar to the cashier interface
        let guestChargeAmount = 0;
        let takeawayChargeAmount = 0;

        if (orderArea) {
            if (!order.isTakeaway && orderArea.guestCharge > 0 && order.numberOfGuests > 0) {
                guestChargeAmount = orderArea.guestCharge * order.numberOfGuests;
            }
            if (order.isTakeaway && orderArea.takeawayCharge > 0) {
                takeawayChargeAmount = orderArea.takeawayCharge;
            }
        }

        // The backend already sends the final totalAmount including charges.
        // We are adding guestCharge and takeawayCharge to the DTO for display purposes in the dialog.
        // The original OrderDto from the server might not have these fields populated for historical orders.
        const augmentedOrder = {
            ...order,
            guestCharge: guestChargeAmount,
            takeawayCharge: takeawayChargeAmount,
        };

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

    if (isLoadingOrgs || isAuthLoading || isLoadingDay || isLoadingPrinters) { // Add isLoadingPrinters here too
        return (
            <div className="flex justify-center items-center h-[calc(100vh-150px)]">
                <Loader2 className="h-16 w-16 animate-spin" />
            </div>
        );
    }

    return (
        <>
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle>Storico Ordini</CardTitle>
                    {/* Filter Selectors */}
                    <div className="flex items-center space-x-4"> {/* Increased spacing */}
                        {/* Day Selector */}
                        <div className="flex items-center space-x-2">
                            <Label htmlFor="day-select">Giornata:</Label>
                            {isLoadingDays || isLoadingDay ? ( // Check both loading states
                                <Loader2 className="h-5 w-5 animate-spin" />
                            ) : (
                                <Select
                                    value={selectedDayId}
                                    onValueChange={setSelectedDayId}
                                >
                                    <SelectTrigger id="day-select" className="w-[180px]">
                                        <SelectValue placeholder="Seleziona Giornata" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="current">
                                            {currentDay ? `Corrente (${new Date(currentDay.startTime).toLocaleDateString('it-IT')})` : 'Corrente (Nessuna Aperta)'}
                                        </SelectItem>
                                        {days.map((day: DayDto) => (
                                            <SelectItem key={day.id} value={day.id.toString()}>
                                                {new Date(day.startTime).toLocaleDateString('it-IT')} {day.status === DayStatus.Closed ? '(Chiusa)' : ''}
                                            </SelectItem>
                                        ))}
                                        {days.length === 0 && !isLoadingDays && <SelectItem value="" disabled>Nessuna giornata storica</SelectItem>}
                                    </SelectContent>
                                </Select>
                            )}
                        </div>
                        {/* Area Selector */}
                        <div className="flex items-center space-x-2">
                            <Label htmlFor="area-select">Area:</Label>
                            {isLoadingAreas ? (
                                <Loader2 className="h-5 w-5 animate-spin" />
                            ) : (
                                <Select
                                    value={selectedAreaId}
                                    onValueChange={setSelectedAreaId}
                                    disabled={areas.length === 0}
                                >
                                    <SelectTrigger id="area-select" className="w-[180px]">
                                        <SelectValue placeholder="Seleziona Area" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {areas.map((area: AreaDto) => ( // Added type annotation for area
                                            <SelectItem key={area.id} value={area.id.toString()}>
                                                {area.name}
                                            </SelectItem>
                                        ))}
                                        {areas.length === 0 && <SelectItem value="" disabled>Nessuna area</SelectItem>}
                                    </SelectContent>
                                </Select>
                            )}
                        </div>
                    </div> {/* Added missing closing div for Filter Selectors */}
                </CardHeader>
                <CardContent>
                    {isLoadingOrders ? (
                        <div className="flex justify-center items-center py-10">
                            <Loader2 className="h-8 w-8 animate-spin" />
                            <span className="ml-2">Caricamento ordini...</span>
                        </div>
                    ) : !selectedAreaId ? (
                        <div className="text-center py-10 text-muted-foreground">
                            {isLoadingAreas ? 'Caricamento aree...' : 'Seleziona un\'area per visualizzare gli ordini.'}
                        </div>
                    ) : orders.length === 0 ? (
                        <div className="text-center py-10 text-muted-foreground">
                            Nessun ordine trovato per l'area selezionata.
                        </div>
                    ) : (
                        <OrderTable
                            orders={orders}
                            onReprintClick={handleOpenReprintDialog} // Pass the handler
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
