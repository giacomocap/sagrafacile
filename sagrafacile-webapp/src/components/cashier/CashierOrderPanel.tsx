'use client';

import React from 'react';
import {
    Card, CardContent, CardFooter
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import {
    PlusCircle, MinusCircle, XCircle, StickyNote, Loader2, CreditCard, Coins, AlertCircle, Ticket, ChevronRight, Volume2
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { CartItem, MenuItemDto, CashierStationDto, QueueStateDto } from '@/types'; // Assuming CartItem is defined in types or passed fully

interface CashierOrderPanelProps {
    currentOrder: CartItem[];
    menuItems: (MenuItemDto & { categoryName?: string })[]; // For finding item details if needed by note dialog trigger
    orderedCategoryNames: string[]; // For grouping cart display
    customerName: string;
    onCustomerNameChange: (name: string) => void;
    numberOfGuests: number;
    onNumberOfGuestsChange: (guests: number) => void;
    isTakeaway: boolean;
    onIsTakeawayChange: (takeaway: boolean) => void;
    onOpenNoteDialog: (item: CartItem) => void;
    onRemoveItemFromCart: (itemId: number) => void;
    onAddItemToCartFromPanel: (item: CartItem) => void; // Renamed to avoid conflict if parent also has onAddItemToCart
    onClearItemFromCart: (itemId: number) => void;
    orderTotals: {
        subtotal: number;
        guestChargeAmount: number;
        takeawayChargeAmount: number;
        finalTotal: number;
    };
    onClearEntireOrder: () => void;
    onOpenPaymentDialog: (paymentMethod: 'Contanti' | 'POS') => void;
    isSubmittingOrder: boolean;
    isFetchingPreOrder: boolean;
    // showScanner: boolean; // Parent controls scanner visibility, this component just needs to disable button
    selectedCashierStationId: number | null;
    availableCashierStations: CashierStationDto[];
    isLoadingCashierStations: boolean;
    cashierStationError: string | null;
    onSelectCashierStation: (stationId: number) => void;
    // Queue System Props
    areaSupportsQueue: boolean;
    queueState: QueueStateDto | null;
    isLoadingQueueState: boolean;
    onCallNext: () => void;
    onCallSpecific: (numberToCall: number) => void;
    onRespeakLastCalled?: () => void; // Optional for now, will be made required if parent implements it
}

const CashierOrderPanel: React.FC<CashierOrderPanelProps> = ({
    currentOrder,
    menuItems,
    orderedCategoryNames,
    customerName,
    onCustomerNameChange,
    numberOfGuests,
    onNumberOfGuestsChange,
    isTakeaway,
    onIsTakeawayChange,
    onOpenNoteDialog,
    onRemoveItemFromCart,
    onAddItemToCartFromPanel,
    onClearItemFromCart,
    orderTotals,
    onClearEntireOrder,
    onOpenPaymentDialog,
    isSubmittingOrder,
    isFetchingPreOrder,
    selectedCashierStationId,
    availableCashierStations,
    isLoadingCashierStations,
    cashierStationError,
    onSelectCashierStation,
    // Queue System Props
    areaSupportsQueue,
    queueState,
    isLoadingQueueState,
    onCallNext,
    onCallSpecific,
    onRespeakLastCalled,
}) => {
    const isPanelDisabled = !selectedCashierStationId && !cashierStationError && availableCashierStations.length > 0;
    const [specificNumberInput, setSpecificNumberInput] = React.useState('');
    const [guestInput, setGuestInput] = React.useState(String(numberOfGuests));

    // Effect to sync local input state when the parent prop changes from an external source
    React.useEffect(() => {
        const localNumber = parseInt(guestInput, 10);
        // If the local value is already what the parent wants, do nothing.
        // This handles the case where the user clears the input (local is '', parent is 0).
        if (isNaN(localNumber) && numberOfGuests === 0) {
            return;
        }
        // If the numbers are different, it means an external change happened.
        if (localNumber !== numberOfGuests) {
            setGuestInput(String(numberOfGuests));
        }
    }, [numberOfGuests, guestInput]); // Only re-sync when the parent prop changes

    const handleGuestsInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const value = e.target.value;
        setGuestInput(value); // Allow the input to be visually updated with any string

        // Update the parent state with a valid number
        if (value === '') {
            onNumberOfGuestsChange(0);
        } else {
            const num = parseInt(value, 10);
            if (!isNaN(num) && num >= 0) {
                onNumberOfGuestsChange(num);
            }
        }
    };

    const handleCallSpecificClick = () => {
        const num = parseInt(specificNumberInput, 10);
        if (!isNaN(num) && num > 0) {
            onCallSpecific(num);
            setSpecificNumberInput(''); // Clear input after calling
        }
    };

    return (
        <Card className="w-full md:w-2/5 flex flex-col m-2 overflow-hidden py-2 gap-2">

            {!isLoadingCashierStations && !cashierStationError && !selectedCashierStationId && availableCashierStations.length > 1 && (
                <CardContent className="p-4 border-b bg-orange-50 dark:bg-orange-900/30">
                    <h3 className="text-md font-semibold text-orange-700 dark:text-orange-300 mb-2">Seleziona Postazione Cassa:</h3>
                    <div className="grid grid-cols-2 gap-2">
                        {availableCashierStations.map(station => (
                            <Button
                                key={station.id}
                                variant="outline"
                                className="w-full"
                                onClick={() => onSelectCashierStation(station.id)}
                            >
                                {station.name}
                            </Button>
                        ))}
                    </div>
                </CardContent>
            )}
            {!isLoadingCashierStations && cashierStationError && !selectedCashierStationId && (
                <CardContent className="p-4 border-b bg-red-50 dark:bg-red-900/30">
                    <Alert variant="destructive">
                        <AlertCircle className="h-4 w-4" />
                        <AlertTitle>Errore Postazione Cassa</AlertTitle>
                        <AlertDescription>{cashierStationError}</AlertDescription>
                    </Alert>
                </CardContent>
            )}

            {/* Customer Queue System Section */}
            {areaSupportsQueue && (
                <CardContent className="p-3 border-b bg-slate-50 dark:bg-slate-800/30">
                    {isLoadingQueueState ? (
                        <div className="flex items-center text-muted-foreground">
                            <Loader2 className="h-4 w-4 animate-spin mr-2" /> Caricamento stato coda...
                        </div>
                    ) : queueState ? (
                        queueState.isQueueSystemEnabled ? (
                            <div className="space-y-1">
                                <div className="flex items-stretch gap-2">
                                    <div className="flex-1 grid grid-cols-2 gap-2 text-center p-2 rounded-md bg-slate-100 dark:bg-slate-900/70">
                                        <div>
                                            <p className="text-xs text-muted-foreground uppercase tracking-wider">Serviamo</p>
                                            <p className="text-2xl font-bold">{queueState.lastCalledNumber || '--'}</p>
                                        </div>
                                        <div>
                                            <p className="text-xs text-muted-foreground uppercase tracking-wider">Prossimo</p>
                                            <p className="text-2xl font-bold">{queueState.nextSequentialNumber}</p>
                                        </div>
                                    </div>
                                    <Button
                                        className="w-1/3 text-base"
                                        onClick={onCallNext}
                                        disabled={isSubmittingOrder || isPanelDisabled}
                                    >
                                        <Ticket className="mr-2 h-5 w-5" /> Chiama Prossimo
                                    </Button>
                                </div>
                                <div className="grid grid-cols-2 gap-1">
                                    <Button
                                        size="sm"
                                        variant="outline"
                                        className="w-full"
                                        onClick={onRespeakLastCalled}
                                        disabled={isSubmittingOrder || isPanelDisabled || !queueState.lastCalledNumber}
                                    >
                                        <Volume2 className="mr-2 h-4 w-4" /> Ripeti
                                    </Button>
                                    <div className="flex items-center space-x-1">
                                        <Input
                                            type="number"
                                            placeholder="N. specifico"
                                            value={specificNumberInput}
                                            onChange={(e) => setSpecificNumberInput(e.target.value)}
                                            className="flex-grow h-9"
                                            disabled={isSubmittingOrder || isPanelDisabled}
                                            min={1}
                                        />
                                        <Button
                                            size="sm"
                                            variant="outline"
                                            onClick={handleCallSpecificClick}
                                            disabled={!specificNumberInput || isSubmittingOrder || isPanelDisabled}
                                        >
                                            <ChevronRight className="mr-1 h-4 w-4" />
                                        </Button>
                                    </div>
                                </div>
                            </div>
                        ) : (
                            <Alert variant="default" className="bg-amber-50 border-amber-300 dark:bg-amber-900/50 dark:border-amber-700">
                                <AlertCircle className="h-4 w-4 text-amber-600 dark:text-amber-400" />
                                <AlertTitle className="text-amber-700 dark:text-amber-300">Sistema Coda Disabilitato</AlertTitle>
                                <AlertDescription className="text-amber-600 dark:text-amber-400">
                                    Il sistema di gestione code clienti non è attivo per quest'area.
                                </AlertDescription>
                            </Alert>
                        )
                    ) : (
                        <p className="text-sm text-muted-foreground">Impossibile caricare lo stato della coda.</p>
                    )}
                </CardContent>
            )}

            <ScrollArea className="flex-grow p-4 border-t border-b min-h-0">
                {isFetchingPreOrder ? (
                    <div className="flex justify-center items-center h-full text-muted-foreground">
                        <Loader2 className="h-6 w-6 animate-spin mr-2" /> Caricamento pre-ordine...
                    </div>
                ) : currentOrder.length === 0 ? (
                    <p className="text-muted-foreground text-center py-10">Aggiungi prodotti o scansiona un pre-ordine.</p>
                ) : (
                    <ul className="space-y-1">
                        {orderedCategoryNames.map(categoryName => {
                            const itemsInCategory = currentOrder.filter(cartItem => {
                                const menuItemDetail = menuItems.find(mi => mi.id === cartItem.menuItemId);
                                return menuItemDetail?.categoryName === categoryName;
                            });

                            if (itemsInCategory.length === 0) return null;

                            return (
                                <React.Fragment key={categoryName}>
                                    <li className="pb-1">
                                        <h4 className="text-sm font-semibold text-muted-foreground border-b pb-1">{categoryName}</h4>
                                    </li>
                                    {itemsInCategory.map((item: CartItem) => (
                                        <li key={item.menuItemId} className="flex items-center justify-between border-b py-1.5">
                                            <div className="flex-1 mr-2">
                                                <p className="font-medium leading-tight">{item.name}</p>
                                                <p className="text-sm text-muted-foreground">
                                                    €{item.unitPrice.toFixed(2)} x {item.quantity}
                                                    {item.note && <span className="italic text-blue-600 ml-2">(Nota)</span>}
                                                </p>
                                                {item.isOutOfStock&& <p className="text-xs text-red-500">Prodotto Esaurito!</p>}
                                                {item.isNoteRequired && !item.note?.trim() && <p className="text-xs text-red-500">Nota richiesta!</p>}
                                            </div>
                                            <div className="flex items-center space-x-2">
                                                <span className="font-semibold w-14 text-right">€{item.totalPrice.toFixed(2)}</span>
                                                <Button variant="outline" size="icon" className="h-8 w-8" onClick={() => onOpenNoteDialog(item)} title="Modifica Nota">
                                                    <StickyNote className={`h-4 w-4 ${item.note ? 'text-blue-600' : ''}`} />
                                                </Button>
                                                <Button variant="outline" size="icon" className="h-8 w-8" onClick={() => onRemoveItemFromCart(item.menuItemId)} title="Rimuovi Uno">
                                                    <MinusCircle className="h-4 w-4" />
                                                </Button>
                                                <Button variant="outline" size="icon" className="h-8 w-8" onClick={() => onAddItemToCartFromPanel(item)} title="Aggiungi Uno">
                                                    <PlusCircle className="h-4 w-4" />
                                                </Button>
                                                <Button variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive hover:bg-destructive/10" onClick={() => onClearItemFromCart(item.menuItemId)} title="Rimuovi Articolo">
                                                    <XCircle className="h-5 w-5" />
                                                </Button>
                                            </div>
                                        </li>
                                    ))}
                                </React.Fragment>
                            );
                        })}
                    </ul>
                )}
            </ScrollArea>
            <CardFooter className="flex flex-col items-stretch pt-3">
                <div className="mb-2">
                    <label htmlFor="customerName" className="block text-sm font-medium text-muted-foreground mb-1">Nome Cliente (Obbligatorio)</label>
                    <Input
                        id="customerNameOrderId"
                        type="text"
                        placeholder="Mario Rossi"
                        value={customerName}
                        onChange={(e) => onCustomerNameChange(e.target.value)}
                        disabled={isSubmittingOrder || isPanelDisabled}
                        required
                    />
                </div>
                <div className="flex items-center justify-between mb-2 space-x-4">
                    <div className="flex items-center space-x-2">
                        <Checkbox
                            id="isTakeawayOrderId"
                            checked={isTakeaway}
                            onCheckedChange={(checked) => onIsTakeawayChange(checked as boolean)}
                            disabled={isPanelDisabled}
                        />
                        <Label htmlFor="isTakeawayOrderId" className="text-sm font-medium">Asporto</Label>
                    </div>
                    <div className="flex items-center space-x-2">
                        <Label htmlFor="numberOfGuestsOrderId" className="text-sm font-medium">Coperti</Label>
                        <Input
                            id="numberOfGuestsOrderId"
                            type="number"
                            value={guestInput}
                            onChange={handleGuestsInputChange}
                            onBlur={() => {
                                // On blur, normalize the input to ensure it's not left empty
                                if (guestInput === '') {
                                    setGuestInput('0');
                                }
                            }}
                            min={0}
                            className="w-20"
                            disabled={isTakeaway || isSubmittingOrder || isPanelDisabled}
                            placeholder="1"
                        />
                    </div>
                </div>
                <div className="space-y-1 text-sm mb-2">
                    <div className="flex justify-between">
                        <span>Subtotale</span>
                        <span>€{orderTotals.subtotal.toFixed(2)}</span>
                    </div>
                    {orderTotals.guestChargeAmount > 0 && (
                        <div className="flex justify-between">
                            <span>Coperto</span>
                            <span>€{orderTotals.guestChargeAmount.toFixed(2)}</span>
                        </div>
                    )}
                    {orderTotals.takeawayChargeAmount > 0 && (
                        <div className="flex justify-between">
                            <span>Asporto</span>
                            <span>€{orderTotals.takeawayChargeAmount.toFixed(2)}</span>
                        </div>
                    )}
                </div>

                <div className="flex justify-between font-bold text-lg mb-2 pt-2 border-t">
                    <span>Totale:</span>
                    <span>€{orderTotals.finalTotal.toFixed(2)}</span>
                </div>
                {currentOrder.length > 0 && (
                    <Button
                        variant="destructive"
                        className="w-full mb-2"
                        onClick={onClearEntireOrder}
                        disabled={isSubmittingOrder || isPanelDisabled}
                    >
                        <XCircle className="mr-2 h-4 w-4" />
                        Svuota Carrello
                    </Button>
                )}
                <div className="grid grid-cols-2 gap-2">
                    <Button
                        size="lg"
                        className="text-lg py-5"
                        onClick={() => onOpenPaymentDialog('Contanti')}
                        disabled={currentOrder.length === 0 || isSubmittingOrder || isPanelDisabled}
                        variant="outline"
                    >
                        <Coins className="mr-2 h-5 w-5" />
                        Paga Contanti
                    </Button>
                    <Button
                        size="lg"
                        className="text-lg py-5"
                        onClick={() => onOpenPaymentDialog('POS')}
                        disabled={currentOrder.length === 0 || isSubmittingOrder || isPanelDisabled}
                    >
                        <CreditCard className="mr-2 h-5 w-5" />
                        Paga POS
                    </Button>
                </div>
            </CardFooter>
        </Card>
    );
};

export default CashierOrderPanel;
