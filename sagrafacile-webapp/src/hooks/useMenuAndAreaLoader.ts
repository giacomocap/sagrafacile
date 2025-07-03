import { useState, useEffect } from 'react';
import { useParams } from 'next/navigation';
import apiClient from '@/services/apiClient';
import {
    AreaDto,
    MenuCategoryDto,
    MenuItemDto,
    StockUpdateBroadcastDto,
} from '@/types';
import useSignalRHub from '@/hooks/useSignalRHub';
import { useAuth } from '@/contexts/AuthContext';
import { useOrganization } from '@/contexts/OrganizationContext';
import * as signalR from '@microsoft/signalr';
import { toast } from 'sonner';

export interface MenuAndAreaLoaderResult {
    currentArea: AreaDto | null;
    menuCategories: MenuCategoryDto[];
    menuItemsWithCategoryName: (MenuItemDto & { categoryName: string })[];
    isLoadingData: boolean;
    errorData: string | null;
    signalRConnection: signalR.HubConnection | null;
    signalRConnectionStatus: string;
}

const useMenuAndAreaLoader = (): MenuAndAreaLoaderResult => {
    const params = useParams();
    const { user, isLoading: isAuthLoading } = useAuth();
    const { isLoadingOrgs, selectedOrganizationId } = useOrganization();

    const orgId = params.orgId as string;
    const areaIdParam = params.areaId as string;

    const [currentArea, setCurrentArea] = useState<AreaDto | null>(null);
    const [menuCategories, setMenuCategories] = useState<MenuCategoryDto[]>([]);
    const [menuItemsWithCategoryName, setMenuItemsWithCategoryName] = useState<(MenuItemDto & { categoryName: string })[]>([]);
    const [isLoadingData, setIsLoadingData] = useState<boolean>(true);
    const [errorData, setErrorData] = useState<string | null>(null);

    // --- SignalR Setup ---
    const hubUrl = `${process.env.NEXT_PUBLIC_API_BASE_URL}/orderHub`;
    const { connection, connectionStatus, startConnection, stopConnection } = useSignalRHub(hubUrl);

    useEffect(() => {
        if (currentArea?.id) {
            startConnection();
        } else {
            stopConnection();
        }
        // Cleanup handled by useSignalRHub
    }, [currentArea, startConnection, stopConnection]);

    useEffect(() => {
        if (connectionStatus === 'Connected' && connection && currentArea?.id) {
            const areaIdString = currentArea.id.toString();
            connection.invoke('JoinAreaQueueGroup', areaIdString) // Using existing group, might need a dedicated one later
                .then(() => console.log(`useMenuAndAreaLoader: Successfully joined Area group for Area ${areaIdString}`))
                .catch(err => console.error(`useMenuAndAreaLoader: Failed to join Area group for Area ${areaIdString}`, err));

            return () => {
                if (connection && connection.state === signalR.HubConnectionState.Connected) {
                    connection.invoke('LeaveAreaQueueGroup', areaIdString)
                        .then(() => console.log(`useMenuAndAreaLoader: Successfully left Area group for Area ${areaIdString}`))
                        .catch(err => console.error(`useMenuAndAreaLoader: Failed to leave Area group for Area ${areaIdString}`, err));
                }
            };
        }
    }, [connectionStatus, connection, currentArea]);

    useEffect(() => {
        if (connectionStatus !== 'Connected' || !connection || !currentArea?.id) {
            return;
        }
        const localAreaId = currentArea.id;

        const handleReceiveStockUpdate = (data: StockUpdateBroadcastDto) => {
            if (data.areaId === localAreaId) {
                setMenuItemsWithCategoryName(prevItems =>
                    prevItems.map(item =>
                        item.id === data.menuItemId
                            ? { ...item, scorta: data.newScorta }
                            : item
                    )
                );
            }
        };

        connection.on('ReceiveStockUpdate', handleReceiveStockUpdate);
        return () => {
            if (connection) {
                connection.off('ReceiveStockUpdate', handleReceiveStockUpdate);
            }
        };
    }, [connectionStatus, connection, currentArea]);


    useEffect(() => {
        async function fetchData() {
            if (!orgId || !areaIdParam || isLoadingOrgs || isAuthLoading) {
                setIsLoadingData(false); // Ensure loading is false if prerequisites aren't met
                return;
            }
            setIsLoadingData(true);
            setErrorData(null);
            setCurrentArea(null);
            setMenuCategories([]);
            setMenuItemsWithCategoryName([]);

            try {
                // Fetch Area Details
                const areaResponse = await apiClient.get<AreaDto>(`/Areas/${areaIdParam}`);
                const fetchedArea = areaResponse.data;

                if (!fetchedArea) {
                    throw new Error(`Area con ID ${areaIdParam} non trovata.`);
                }
                if (fetchedArea.organizationId !== orgId) {
                     // Check against selectedOrganizationId as well, especially for SuperAdmin
                    if (user?.roles.includes('SuperAdmin') && selectedOrganizationId && fetchedArea.organizationId !== selectedOrganizationId) {
                        console.warn(`useMenuAndAreaLoader: Area ${fetchedArea.name} (Org ID: ${fetchedArea.organizationId}) does not match SuperAdmin's selected organization context (Org ID: ${selectedOrganizationId}). Proceeding as SuperAdmin might be viewing cross-org.`);
                    } else if (!user?.roles.includes('SuperAdmin')) {
                        throw new Error(`Area ${fetchedArea.name} non appartiene all'organizzazione corrente (URL Org ID: ${orgId}, Area Org ID: ${fetchedArea.organizationId}).`);
                    }
                }
                setCurrentArea(fetchedArea);

                // Fetch Menu Categories for the area
                const categoriesRes = await apiClient.get<MenuCategoryDto[]>(`/menucategories?areaId=${fetchedArea.id}`);
                const fetchedCategories = categoriesRes.data || [];
                setMenuCategories(fetchedCategories);

                if (fetchedCategories.length === 0) {
                    setMenuItemsWithCategoryName([]);
                    setIsLoadingData(false);
                    toast.info(`Nessuna categoria menu trovata per l'area '${fetchedArea.name}'.`);
                    return;
                }

                // Fetch Menu Items for each category
                const itemPromises = fetchedCategories.map(cat =>
                    apiClient.get<MenuItemDto[]>(`/menuitems?categoryId=${cat.id}`)
                );
                const itemResponses = await Promise.all(itemPromises);
                const allItems = itemResponses.flatMap(res => res.data || []);

                const itemsWithCatName = allItems.map(item => {
                    const category = fetchedCategories.find(cat => cat.id === item.menuCategoryId);
                    return { ...item, categoryName: category?.name || 'Uncategorized' };
                });
                setMenuItemsWithCategoryName(itemsWithCatName);

                if (itemsWithCatName.length === 0) {
                    toast.info(`Nessun prodotto menu trovato per l'area '${fetchedArea.name}'.`);
                }

            } catch (error: any) {
                const errorMessage = error.response?.data?.detail || error.response?.data?.message || error.message || "Errore nel caricamento dei dati.";
                setErrorData(errorMessage);
                toast.error(errorMessage);
                console.error("Error fetching data in useMenuAndAreaLoader:", error);
            } finally {
                setIsLoadingData(false);
            }
        }

        fetchData();
    }, [orgId, areaIdParam, isLoadingOrgs, isAuthLoading, user, selectedOrganizationId]); // Added user and selectedOrganizationId

    return {
        currentArea,
        menuCategories,
        menuItemsWithCategoryName,
        isLoadingData,
        errorData,
        signalRConnection: connection,
        signalRConnectionStatus: connectionStatus,
    };
};

export default useMenuAndAreaLoader;
