'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { useParams } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { XCircle, Loader2 } from 'lucide-react';
import { toast } from "sonner";
import { Scanner, IDetectedBarcode } from '@yudiel/react-qr-scanner';
import apiClient from '@/services/apiClient';
import {
    MenuItemDto,
    OrderDto,
    OrderStatus,
    OrderItemDto,
    CashierStationDto,
    QueueStateDto,
    CalledNumberBroadcastDto,
    QueueResetBroadcastDto,
    QueueStateUpdateBroadcastDto,
    UserDto,
    AppCartItem,
    CartItem,
} from '@/types';
import ReceiptDialog from '@/components/ReceiptDialog';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogDescription,
    DialogFooter,
    DialogClose,
} from "@/components/ui/dialog";
import {
    AlertDialog,
    AlertDialogContent,
    AlertDialogHeader,
    AlertDialogTitle,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogCancel,
} from "@/components/ui/alert-dialog";
import ReprintOrderDialog from '@/components/cashier/ReprintOrderDialog';
import CashierMenuPanel from '@/components/cashier/CashierMenuPanel';
import OrderQrScanner from "@/components/shared/OrderQrScanner";
import OrderConfirmationView from "@/components/shared/OrderConfirmationView";
import CashierOrderPanel from '@/components/cashier/CashierOrderPanel';
import queueService from '@/services/queueService';
import useMenuAndAreaLoader from '@/hooks/useMenuAndAreaLoader';
import useAppCart from '@/hooks/useAppCart';
import useOrderHandler from '@/hooks/useOrderHandler';
import OperationalHeader from '@/components/shared/OperationalHeader';

const CashierPage = () => {
    const params = useParams();
    const { user, isLoading: isAuthLoading } = useAuth();

    const orgId = params.orgId as string;
    const areaIdParam = params.areaId ? parseInt(params.areaId as string, 10) : null;

    // --- Custom Hooks ---
    const {
        currentArea,
        menuCategories,
        menuItemsWithCategoryName: menuItems,
        isLoadingData: isLoadingMenuAndArea,
        errorData: menuAndAreaError,
        signalRConnection: connection,
        signalRConnectionStatus: connectionStatus,
    } = useMenuAndAreaLoader();

    const {
        cart,
        setCart,
        addToCart,
        increaseQuantity,
        decreaseQuantity,
        removeFromCart,
        openNoteDialog,
        saveNote,
        closeNoteDialog,
        clearCart,
        cartTotal,
        isNoteDialogOpen,
        currentItemForNote,
        currentNoteValue,
        setCurrentNoteValue,
    } = useAppCart();

    const {
        isSubmitting: isSubmittingOrder,
        isReceiptOpen,
        pendingOrderForReceipt,
        openReceiptDialog,
        submitOrderToServer,
        closeReceiptDialog,
    } = useOrderHandler({
        onOrderSuccess: () => {
            handleClearOrder();
        }
    });

    // --- Page-Specific State ---
    const [searchTerm, setSearchTerm] = useState('');
    const [justAddedItemId, setJustAddedItemId] = useState<number | null>(null);
    const [isReprintDialogOpen, setIsReprintDialogOpen] = useState(false);
    const [showScanner, setShowScanner] = useState(false);
    const [isFetchingPreOrder, setIsFetchingPreOrder] = useState(false);
    const [scannedPreOrderId, setScannedPreOrderId] = useState<string | null>(null);
    const [numberOfGuests, setNumberOfGuests] = useState(1);
    const [isTakeaway, setIsTakeaway] = useState(false);
    const [customerName, setCustomerName] = useState('');

    const orderTotals = React.useMemo(() => {
        const subtotal = cartTotal;
        let guestChargeAmount = 0;
        let takeawayChargeAmount = 0;

        if (currentArea) {
            if (!isTakeaway && currentArea.guestCharge > 0 && numberOfGuests > 0) {
                guestChargeAmount = currentArea.guestCharge * numberOfGuests;
            }
            if (isTakeaway && currentArea.takeawayCharge > 0) {
                takeawayChargeAmount = currentArea.takeawayCharge;
            }
        }

        const finalTotal = subtotal + guestChargeAmount + takeawayChargeAmount;

        return {
            subtotal,
            guestChargeAmount,
            takeawayChargeAmount,
            finalTotal,
        };
    }, [cartTotal, currentArea, isTakeaway, numberOfGuests]);

    const [availableCashierStations, setAvailableCashierStations] = useState<CashierStationDto[]>([]);
    const [selectedCashierStationId, setSelectedCashierStationId] = useState<number | null>(null);
    const [isLoadingCashierStations, setIsLoadingCashierStations] = useState(true);
    const [cashierStationError, setCashierStationError] = useState<string | null>(null);
    const [queueState, setQueueState] = useState<QueueStateDto | null>(null);
    const [isLoadingQueueState, setIsLoadingQueueState] = useState(false);

    // Scanner State
    const [isScanDialogOpen, setIsScanDialogOpen] = useState(false);
    const [scannedOrderIdForConfirmation, setScannedOrderIdForConfirmation] = useState<string | null>(null);
    const [showScannerInDialog, setShowScannerInDialog] = useState(true);


    useEffect(() => {
        if (isTakeaway) {
            setNumberOfGuests(0);
        } else {
            setNumberOfGuests(1);
        }
    }, [isTakeaway]);

    const fetchInitialQueueState = useCallback(async () => {
        if (currentArea?.id && currentArea.enableQueueSystem) {
            setIsLoadingQueueState(true);
            try {
                const state = await queueService.getQueueState(currentArea.id);
                setQueueState(state);
            } catch (err) {
                console.error(`Error fetching queue state for area ${currentArea.id}:`, err);
                toast.error('Impossibile caricare lo stato della coda.');
                setQueueState(null);
            } finally {
                setIsLoadingQueueState(false);
            }
        } else {
            setQueueState(null);
        }
    }, [currentArea]);

    useEffect(() => {
        fetchInitialQueueState();
    }, [fetchInitialQueueState]);

    useEffect(() => {
        if (connectionStatus === 'Connected' && connection && currentArea?.id) {
            const handleQueueNumberCalled = (data: CalledNumberBroadcastDto) => {
                if (currentArea && currentArea.id.toString() === String(data.areaId)) {
                    setQueueState(prevState => ({
                        ...(prevState || { areaId: currentArea.id, isQueueSystemEnabled: true, nextSequentialNumber: 1 }),
                        lastCalledNumber: data.ticketNumber,
                        lastCalledCashierStationId: data.cashierStationId,
                        lastCalledCashierStationName: data.cashierStationName,
                        lastCallTimestamp: data.timestamp,
                        nextSequentialNumber: data.ticketNumber + 1,
                    }));
                }
            };
            const handleQueueReset = (data: QueueResetBroadcastDto) => {
                if (String(data.areaId) === currentArea.id.toString()) {
                    toast.info("La coda è stata resettata.");
                    fetchInitialQueueState();
                }
            };
            const handleQueueStateUpdated = (data: QueueStateUpdateBroadcastDto) => {
                if (String(data.areaId) === currentArea.id.toString()) {
                    setQueueState(data.newState);
                }
            };

            connection.on('QueueNumberCalled', handleQueueNumberCalled);
            connection.on('QueueReset', handleQueueReset);
            connection.on('QueueStateUpdated', handleQueueStateUpdated);

            return () => {
                connection?.off('QueueNumberCalled', handleQueueNumberCalled);
                connection?.off('QueueReset', handleQueueReset);
                connection?.off('QueueStateUpdated', handleQueueStateUpdated);
            };
        }
    }, [currentArea, connection, connectionStatus, fetchInitialQueueState]);

    useEffect(() => {
        if (!currentArea?.id || isLoadingMenuAndArea) return;
        const fetchCashierStations = async () => {
            setIsLoadingCashierStations(true);
            setCashierStationError(null);
            try {
                const response = await apiClient.get<CashierStationDto[]>(`/cashierstations/area/${currentArea.id}`);
                const enabledStations = (response.data || []).filter((s: CashierStationDto) => s.isEnabled);
                setAvailableCashierStations(enabledStations);

                const storedStationId = localStorage.getItem(`selectedStation_${currentArea.id}`);
                const storedStation = enabledStations.find((s: CashierStationDto) => s.id.toString() === storedStationId);

                if (enabledStations.length === 1) {
                    setSelectedCashierStationId(enabledStations[0].id);
                } else if (storedStation) {
                    setSelectedCashierStationId(storedStation.id);
                } else {
                    setSelectedCashierStationId(null);
                    if (enabledStations.length === 0) {
                        setCashierStationError("Nessuna postazione cassa abilitata per quest'area.");
                    }
                }
            } catch {
                setCashierStationError('Impossibile caricare le postazioni cassa.');
                toast.error('Impossibile caricare le postazioni cassa.');
            } finally {
                setIsLoadingCashierStations(false);
            }
        };
        fetchCashierStations();
    }, [currentArea, isLoadingMenuAndArea]);

    const handleSelectCashierStation = (stationId: number) => {
        setSelectedCashierStationId(stationId);
        if (currentArea?.id) {
            localStorage.setItem(`selectedStation_${currentArea.id}`, String(stationId));
            toast.success(`Postazione cassa selezionata.`);
        }
    };

    const handleRequestChangeStation = () => {
        setSelectedCashierStationId(null);
        if (currentArea?.id) {
            localStorage.removeItem(`selectedStation_${currentArea.id}`);
        }
    };

    const handleAddItem = (item: MenuItemDto) => {
        setJustAddedItemId(item.id);
        setTimeout(() => setJustAddedItemId(null), 500);
        const category = menuCategories.find(c => c.id === item.menuCategoryId);
        addToCart(item, category?.name || 'Uncategorized');
    };

    const handleIncreaseItem = (cartItemId: string) => {
        increaseQuantity(cartItemId);
    };

    const handleDecreaseItemByMenuId = (menuItemId: number) => {
        const item = [...cart].reverse().find(i => i.menuItemId === menuItemId);
        if (item) {
            decreaseQuantity(item.cartItemId);
        }
    };

    const handleRemoveItemFromCartByMenuId = (menuItemId: number) => {
        const item = [...cart].reverse().find(i => i.menuItemId === menuItemId);
        if (item) {
            removeFromCart(item.cartItemId);
        }
    };

    const handleClearOrder = () => {
        clearCart();
        setCustomerName('');
        setSearchTerm('');
        setScannedPreOrderId(null);
        setNumberOfGuests(1);
        setIsTakeaway(false);
    };

    const handleSaveNote = () => {
        saveNote(currentNoteValue);
    };

    const handlePayment = (paymentMethod: 'Contanti' | 'POS') => {
        if (!customerName.trim()) {
            toast.error("Il nome del cliente è obbligatorio.");
            return;
        }
        if (numberOfGuests < 1 && !isTakeaway) {
            toast.error("Il numero di coperti deve essere almeno 1.");
            return;
        }
        openReceiptDialog(
            paymentMethod,
            { customerName, numberOfGuests, isTakeaway },
            cart,
            currentArea,
            user as unknown as UserDto,
            orderTotals,
            scannedPreOrderId
        );
    };

    const handleCloseReceiptDialog = (success?: boolean) => {
        closeReceiptDialog(success);
        if (success) {
            handleClearOrder();
        }
    };

    const handleScanResult = async (result: IDetectedBarcode[]) => {
        if (isFetchingPreOrder || !result || result.length === 0) return;
        const scannedId = result[0].rawValue;
        setShowScanner(false);
        setIsFetchingPreOrder(true);
        toast.info(`Caricamento pre-ordine ${scannedId}...`);
        try {
            const response = await apiClient.get<OrderDto>(`/orders/${scannedId}`);
            const preOrder = response.data;
            if (preOrder.status !== OrderStatus.PreOrder) throw new Error(`L'ordine non è un pre-ordine valido.`);
            if (preOrder.areaId !== currentArea?.id) throw new Error(`Il pre-ordine appartiene a un'altra area.`);

            if (cart.length > 0) toast.warning("Carrello corrente svuotato per caricare il pre-ordine.");
            setCustomerName(preOrder.customerName || '');
            setNumberOfGuests(preOrder.numberOfGuests || 1);
            setIsTakeaway(preOrder.isTakeaway || false);
            const newCartItems: AppCartItem[] = preOrder.items.map((item: OrderItemDto) => {
                const menuItem = menuItems.find(mi => mi.id === item.menuItemId);
                return {
                    cartItemId: `cart-${Date.now()}-${Math.random()}`,
                    menuItemId: item.menuItemId,
                    name: item.menuItemName,
                    quantity: item.quantity,
                    unitPrice: item.unitPrice,
                    totalPrice: item.unitPrice * item.quantity,
                    note: item.note || null,
                    isNoteRequired: menuItem?.isNoteRequired || false,
                    noteSuggestion: menuItem?.noteSuggestion,
                    categoryName: menuCategories.find(mc => mc.id === menuItem?.menuCategoryId)?.name || 'N/A',
                } as AppCartItem;
            });
            setCart(newCartItems);
            setScannedPreOrderId(preOrder.id);
            toast.success(`Pre-ordine ${scannedId} caricato.`);

            const stockIssues = newCartItems.filter(ci => {
                const mi = menuItems.find(i => i.id === ci.menuItemId);
                return mi && typeof mi.scorta === 'number' && mi.scorta < ci.quantity;
            });
            if (stockIssues.length > 0) {
                toast.warning(`Attenzione, alcuni articoli potrebbero avere scorte insufficienti: ${stockIssues.map(i => i.name).join(', ')}`, { duration: 8000 });
            }
        } catch (error: any) {
            toast.error(error.message || 'Errore nel caricamento del pre-ordine.');
            setScannedPreOrderId(null);
        } finally {
            setIsFetchingPreOrder(false);
        }
    };

    const handleScanError = (error: unknown) => {
        toast.error('Errore scanner: ' + (error instanceof Error ? error.message : 'sconosciuto'));
        setShowScanner(false);
    };

    const filteredMenuItems = menuItems.filter(item => item.name.toLowerCase().includes(searchTerm.toLowerCase()));
    const itemsGroupedByCategory = filteredMenuItems.reduce((acc, item) => {
        const categoryName = item.categoryName || 'Uncategorized';
        if (!acc[categoryName]) acc[categoryName] = [];
        acc[categoryName].push(item);
        return acc;
    }, {} as Record<string, (MenuItemDto & { categoryName: string })[]>);
    const orderedCategoryNames = menuCategories.map(cat => cat.name).filter(name => itemsGroupedByCategory[name]?.length > 0);

    const handleCallNext = useCallback(async () => {
        if (!currentArea?.id || !queueState?.isQueueSystemEnabled) return;
        try {
            await queueService.callNext(currentArea.id, { cashierStationId: selectedCashierStationId });
        } catch (err: any) {
            toast.error(err.response?.data?.detail || 'Errore chiamata prossimo numero.');
        }
    }, [currentArea, queueState, selectedCashierStationId]);

    const handleCallSpecific = useCallback(async (numberToCall: number) => {
        if (!currentArea?.id || !queueState?.isQueueSystemEnabled || !numberToCall) return;
        try {
            await queueService.callSpecific(currentArea.id, { ticketNumber: numberToCall, cashierStationId: selectedCashierStationId });
        } catch (err: any) {
            toast.error(err.response?.data?.detail || 'Errore chiamata numero specifico.');
        }
    }, [currentArea, queueState, selectedCashierStationId]);

    const handleRespeakLastCalled = useCallback(async () => {
        if (!currentArea?.id || !queueState?.isQueueSystemEnabled || !selectedCashierStationId) return;
        if (!queueState.lastCalledNumber) {
            toast.info("Nessun numero ancora chiamato.");
            return;
        }
        try {
            await queueService.respeakLastCalledNumber(currentArea.id, { cashierStationId: selectedCashierStationId });
        } catch (err: any) {
            toast.error(err.response?.data?.detail || 'Errore ripetizione numero.');
        }
    }, [currentArea, queueState, selectedCashierStationId]);

    if (isAuthLoading || isLoadingMenuAndArea) {
        return <div className="flex justify-center items-center h-screen"><Loader2 className="h-16 w-16 animate-spin" /></div>;
    }
    if (menuAndAreaError) {
        return <div className="p-4 text-center text-red-500">{menuAndAreaError}</div>;
    }
    if (!currentArea) {
        return <div className="p-4 text-center text-red-500">Area non trovata o non accessibile.</div>;
    }

    if (showScanner) {
        return (
            <div className="fixed inset-0 bg-black bg-opacity-75 flex flex-col items-center justify-center z-50 p-4">
                <p className="text-white text-lg mb-4">Inquadra il QR code...</p>
                <div className="w-full max-w-md relative aspect-square">
                    <Scanner onScan={handleScanResult} onError={handleScanError} components={{ finder: true, torch: true }} />
                    <Button variant="destructive" size="sm" onClick={() => setShowScanner(false)} className="absolute top-2 right-2 z-10">
                        <XCircle className="h-4 w-4 mr-1" /> Annulla
                    </Button>
                </div>
                {isFetchingPreOrder && <div className="mt-4 flex items-center text-white"><Loader2 className="h-5 w-5 animate-spin mr-2" /> Caricamento...</div>}
            </div>
        );
    }

    return (
        <>
            <OperationalHeader
                title="Cassa"
                areaName={currentArea?.name}
                orgId={orgId}
                role="cashier"
                compact={true}
            />
            <div className="flex flex-col md:flex-row h-[calc(100vh-100px)]">
                <CashierMenuPanel
                    selectedArea={currentArea}
                    isLoadingMenu={isLoadingMenuAndArea}
                    menuItems={menuItems}
                    menuCategories={menuCategories}
                    searchTerm={searchTerm}
                    onSearchTermChange={setSearchTerm}
                    onAddItemToCart={handleAddItem}
                    justAddedItemId={justAddedItemId}
                    onReprintClick={() => setIsReprintDialogOpen(true)}
                    isLoadingCashierStations={isLoadingCashierStations}
                    selectedCashierStationId={selectedCashierStationId}
                    availableCashierStations={availableCashierStations}
                    cashierStationError={cashierStationError}
                    filteredMenuItems={filteredMenuItems}
                    orderedCategoryNames={orderedCategoryNames}
                    itemsGroupedByCategory={itemsGroupedByCategory}
                    onRequestChangeStation={handleRequestChangeStation}
                    onScanPreOrderClick={() => setShowScanner(true)}
                    onScanQrClick={() => {
                        setScannedOrderIdForConfirmation(null);
                        setShowScannerInDialog(true);
                        setIsScanDialogOpen(true);
                    }}
                    isSubmittingOrder={isSubmittingOrder}
                    isFetchingPreOrder={isFetchingPreOrder}
                />

                <CashierOrderPanel
                    currentOrder={cart}
                    menuItems={menuItems}
                    orderedCategoryNames={orderedCategoryNames}
                    customerName={customerName}
                    onCustomerNameChange={setCustomerName}
                    numberOfGuests={numberOfGuests}
                    onNumberOfGuestsChange={setNumberOfGuests}
                    isTakeaway={isTakeaway}
                    onIsTakeawayChange={setIsTakeaway}
                    onOpenNoteDialog={(item: CartItem) => openNoteDialog(item as AppCartItem)}
                    onRemoveItemFromCart={handleDecreaseItemByMenuId}
                    onAddItemToCartFromPanel={(cartItem: CartItem) => handleIncreaseItem(cartItem.cartItemId)}
                    onClearItemFromCart={handleRemoveItemFromCartByMenuId}
                    orderTotals={orderTotals}
                    onClearEntireOrder={handleClearOrder}
                    onOpenPaymentDialog={handlePayment}
                    isSubmittingOrder={isSubmittingOrder}
                    isFetchingPreOrder={isFetchingPreOrder}
                    selectedCashierStationId={selectedCashierStationId}
                    availableCashierStations={availableCashierStations}
                    isLoadingCashierStations={isLoadingCashierStations}
                    cashierStationError={cashierStationError}
                    onSelectCashierStation={handleSelectCashierStation}
                    areaSupportsQueue={currentArea?.enableQueueSystem}
                    queueState={queueState}
                    isLoadingQueueState={isLoadingQueueState}
                    onCallNext={handleCallNext}
                    onCallSpecific={handleCallSpecific}
                    onRespeakLastCalled={handleRespeakLastCalled}
                />

                <Dialog open={isNoteDialogOpen} onOpenChange={closeNoteDialog}>
                    <DialogContent>
                        <DialogHeader>
                            <DialogTitle>Nota per {currentItemForNote?.name}</DialogTitle>
                            <DialogDescription>
                                {currentItemForNote?.noteSuggestion && `Suggerimento: ${currentItemForNote.noteSuggestion}`}
                                {currentItemForNote?.isNoteRequired && <span className="text-red-500 ml-2">(Nota richiesta)</span>}
                            </DialogDescription>
                        </DialogHeader>
                        <Textarea value={currentNoteValue} onChange={(e) => setCurrentNoteValue(e.target.value)} rows={4} />
                        <DialogFooter>
                            <DialogClose asChild><Button type="button" variant="secondary">Annulla</Button></DialogClose>
                            <Button type="button" onClick={handleSaveNote}>Salva Nota</Button>
                        </DialogFooter>
                    </DialogContent>
                </Dialog>

                {pendingOrderForReceipt && (
                    <ReceiptDialog
                        order={pendingOrderForReceipt}
                        isOpen={isReceiptOpen}
                        onClose={handleCloseReceiptDialog}
                        onSubmitOrder={() => submitOrderToServer(
                            pendingOrderForReceipt.paymentMethod as 'Contanti' | 'POS',
                            pendingOrderForReceipt.amountPaid ?? null,
                            { customerName, numberOfGuests, isTakeaway },
                            cart,
                            currentArea,
                            user as unknown as UserDto,
                            scannedPreOrderId,
                            selectedCashierStationId
                        )}
                        menuItems={menuItems}
                        menuCategories={menuCategories}
                    />
                )}

                <ReprintOrderDialog
                    isOpen={isReprintDialogOpen}
                    onClose={() => setIsReprintDialogOpen(false)}
                    areaId={areaIdParam}
                    orgId={orgId}
                />

                {/* Scan Dialog */}
                <AlertDialog open={isScanDialogOpen} onOpenChange={setIsScanDialogOpen}>
                    <AlertDialogContent className="max-w-lg">
                        <AlertDialogHeader>
                            <AlertDialogTitle>{scannedOrderIdForConfirmation ? 'Dettaglio Ordine Scansionato' : 'Scansiona QR Code'}</AlertDialogTitle>
                            <AlertDialogDescription>
                                {scannedOrderIdForConfirmation ? 'Controlla i dettagli dell\'ordine.' : 'Inquadra il QR code sulla ricevuta per visualizzare o confermare un ordine.'}
                            </AlertDialogDescription>
                        </AlertDialogHeader>

                        {!scannedOrderIdForConfirmation ? (
                            <OrderQrScanner
                                showScanner={showScannerInDialog}
                                onShowScannerChange={setShowScannerInDialog}
                                onScanSuccess={setScannedOrderIdForConfirmation}
                                forceShowScanner={true}
                            />
                        ) : (
                            <OrderConfirmationView
                                orderId={scannedOrderIdForConfirmation}
                                onCancel={() => {
                                    setScannedOrderIdForConfirmation(null);
                                    setIsScanDialogOpen(false);
                                }}
                                onSuccess={() => {
                                    setScannedOrderIdForConfirmation(null);
                                    setIsScanDialogOpen(false);
                                }}
                            />
                        )}

                        <AlertDialogFooter className="mt-4">
                            <AlertDialogCancel onClick={() => {
                                setScannedOrderIdForConfirmation(null);
                                setIsScanDialogOpen(false);
                            }}>Chiudi</AlertDialogCancel>
                        </AlertDialogFooter>
                    </AlertDialogContent>
                </AlertDialog>
            </div>
        </>
    );
};

export default CashierPage;
