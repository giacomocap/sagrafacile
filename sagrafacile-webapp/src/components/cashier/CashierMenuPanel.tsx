'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Loader2, CheckCircle, StickyNote, History, Edit3, ScanLine, Ticket, ChevronRight, Volume2, AlertCircle } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import DayStatusIndicator from '@/components/DayStatusIndicator';
import { AreaDto, MenuItemDto, MenuCategoryDto, CashierStationDto, QueueStateDto } from '@/types';

interface GroupedMenuItems {
    [categoryName: string]: MenuItemDto[];
}

interface CashierMenuPanelProps {
    selectedArea: AreaDto | null | undefined;
    isLoadingMenu: boolean;
    menuItems: (MenuItemDto & { categoryName?: string })[]; // Assuming categoryName is added
    menuCategories: MenuCategoryDto[];
    searchTerm: string;
    onSearchTermChange: (term: string) => void;
    onAddItemToCart: (item: MenuItemDto) => void;
    justAddedItemId: number | null;
    onReprintClick: () => void;
    isLoadingCashierStations: boolean;
    selectedCashierStationId: number | null;
    availableCashierStations: CashierStationDto[];
    cashierStationError: string | null;
    filteredMenuItems: (MenuItemDto & { categoryName?: string })[]; // Pass pre-filtered items
    orderedCategoryNames: string[]; // Pass pre-ordered names
    itemsGroupedByCategory: GroupedMenuItems; // Pass pre-grouped items
    onRequestChangeStation: () => void; // New prop for requesting station change
    onScanPreOrderClick: () => void;
    onScanQrClick: () => void;
    isSubmittingOrder: boolean;
    isFetchingPreOrder: boolean;
    // Queue System Props
    areaSupportsQueue?: boolean;
    queueState?: QueueStateDto | null;
    isLoadingQueueState?: boolean;
    onCallNext?: () => void;
    onCallSpecific?: (numberToCall: number) => void;
    onRespeakLastCalled?: () => void;
}

const CashierMenuPanel: React.FC<CashierMenuPanelProps> = ({
    selectedArea,
    isLoadingMenu,
    menuItems, // Keep for reference if needed, e.g. total items count
    menuCategories,
    searchTerm,
    onSearchTermChange,
    onAddItemToCart,
    justAddedItemId,
    onReprintClick,
    isLoadingCashierStations,
    selectedCashierStationId,
    availableCashierStations,
    cashierStationError,
    filteredMenuItems,
    orderedCategoryNames,
    itemsGroupedByCategory,
    onRequestChangeStation, // Destructure new prop
    onScanPreOrderClick,
    onScanQrClick,
    isSubmittingOrder,
    isFetchingPreOrder,
    // Queue System Props
    areaSupportsQueue,
    queueState,
    isLoadingQueueState,
    onCallNext,
    onCallSpecific,
    onRespeakLastCalled,
}) => {
    const [specificNumberInput, setSpecificNumberInput] = useState('');
    const isSearchDisabled = isLoadingMenu || !selectedArea || (!selectedCashierStationId && !cashierStationError && availableCashierStations.length > 0);
    const canChangeStation = selectedCashierStationId !== null && availableCashierStations.length > 1;
    const isActionDisabled = isSubmittingOrder || isFetchingPreOrder || isSearchDisabled;
    const isPanelDisabled = !selectedCashierStationId && !cashierStationError && availableCashierStations.length > 0;

    const handleCallSpecificClick = () => {
        const num = parseInt(specificNumberInput, 10);
        if (!isNaN(num) && num > 0 && onCallSpecific) {
            onCallSpecific(num);
            setSpecificNumberInput(''); // Clear input after calling
        }
    };

    return (
        <Card className="w-full md:w-3/5 flex flex-col m-2">
            <CardHeader className="space-y-2 py-3">
                <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                        <DayStatusIndicator />
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={onScanQrClick}
                            disabled={isActionDisabled}
                            className="text-xs px-2 py-1 h-7"
                        >
                            <ScanLine className="mr-1 h-3 w-3" />
                            QR
                        </Button>
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={onScanPreOrderClick}
                            disabled={isActionDisabled}
                            className="text-xs px-2 py-1 h-7"
                        >
                            <ScanLine className="mr-1 h-3 w-3" />
                            Pre-Ordine
                        </Button>
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={onReprintClick}
                            disabled={!selectedArea || !selectedArea.organizationId}
                            className="text-xs px-2 py-1 h-7"
                        >
                            <History className="mr-1 h-3 w-3" />
                            Storico
                        </Button>
                    </div>
                    
                    <div className="flex items-center">
                        {isLoadingCashierStations ? (
                            <div className="text-sm text-muted-foreground flex items-center">
                                <Loader2 className="h-4 w-4 animate-spin mr-2" /> Caricamento...
                            </div>
                        ) : selectedCashierStationId ? (
                            <div className="flex items-center gap-2">
                                <span className="text-sm text-green-600 dark:text-green-400 font-medium">
                                    {availableCashierStations.find(s => s.id === selectedCashierStationId)?.name || 'Sconosciuta'}
                                </span>
                                {canChangeStation && (
                                    <Button variant="outline" size="sm" onClick={onRequestChangeStation} className="h-7 text-xs">
                                        <Edit3 className="h-3 w-3 mr-1" /> Cambia
                                    </Button>
                                )}
                            </div>
                        ) : cashierStationError ? (
                            <div className="text-sm text-red-500 font-medium">
                                Errore postazioni
                            </div>
                        ) : availableCashierStations.length > 1 && !selectedCashierStationId ? (
                            <div className="text-sm text-orange-600 font-medium">
                                Seleziona postazione
                            </div>
                        ) : null}
                    </div>
                </div>

                {/* Show detailed error message if there's a cashier station error */}
                {cashierStationError && (
                    <div className="text-sm text-red-500 font-medium p-2 border border-red-500 bg-red-50 dark:bg-red-900/30 rounded-md text-center">
                        {cashierStationError}
                    </div>
                )}

                {/* Customer Queue System Section */}
                {areaSupportsQueue && (
                    <div className="p-2 border rounded-md bg-slate-50 dark:bg-slate-800/30">
                        {isLoadingQueueState ? (
                            <div className="flex items-center text-muted-foreground text-sm">
                                <Loader2 className="h-4 w-4 animate-spin mr-2" /> Caricamento stato coda...
                            </div>
                        ) : queueState ? (
                            queueState.isQueueSystemEnabled ? (
                                <div className="space-y-2">
                                    <div className="flex items-center gap-2">
                                        <div className="flex-1 grid grid-cols-2 gap-2 text-center p-2 rounded bg-slate-100 dark:bg-slate-900/70">
                                            <div>
                                                <p className="text-xs text-muted-foreground uppercase tracking-wider">Serviamo</p>
                                                <p className="text-lg font-bold">{queueState.lastCalledNumber || '--'}</p>
                                            </div>
                                            <div>
                                                <p className="text-xs text-muted-foreground uppercase tracking-wider">Prossimo</p>
                                                <p className="text-lg font-bold">{queueState.nextSequentialNumber}</p>
                                            </div>
                                        </div>
                                        <Button
                                            size="sm"
                                            onClick={onCallNext}
                                            disabled={isSubmittingOrder || isPanelDisabled}
                                            className="px-3"
                                        >
                                            <Ticket className="mr-1 h-4 w-4" /> Prossimo
                                        </Button>
                                    </div>
                                    <div className="grid grid-cols-2 gap-1">
                                        <Button
                                            size="sm"
                                            variant="outline"
                                            onClick={onRespeakLastCalled}
                                            disabled={isSubmittingOrder || isPanelDisabled || !queueState.lastCalledNumber}
                                            className="text-xs"
                                        >
                                            <Volume2 className="mr-1 h-3 w-3" /> Ripeti
                                        </Button>
                                        <div className="flex items-center space-x-1">
                                            <Input
                                                type="number"
                                                placeholder="N."
                                                value={specificNumberInput}
                                                onChange={(e) => setSpecificNumberInput(e.target.value)}
                                                className="flex-grow h-8 text-sm"
                                                disabled={isSubmittingOrder || isPanelDisabled}
                                                min={1}
                                            />
                                            <Button
                                                size="sm"
                                                variant="outline"
                                                onClick={handleCallSpecificClick}
                                                disabled={!specificNumberInput || isSubmittingOrder || isPanelDisabled}
                                                className="px-2"
                                            >
                                                <ChevronRight className="h-3 w-3" />
                                            </Button>
                                        </div>
                                    </div>
                                </div>
                            ) : (
                                <Alert variant="default" className="bg-amber-50 border-amber-300 dark:bg-amber-900/50 dark:border-amber-700 py-2">
                                    <AlertCircle className="h-4 w-4 text-amber-600 dark:text-amber-400" />
                                    <AlertTitle className="text-amber-700 dark:text-amber-300 text-sm">Sistema Coda Disabilitato</AlertTitle>
                                    <AlertDescription className="text-amber-600 dark:text-amber-400 text-xs">
                                        Il sistema di gestione code clienti non è attivo per quest'area.
                                    </AlertDescription>
                                </Alert>
                            )
                        ) : (
                            <p className="text-sm text-muted-foreground">Impossibile caricare lo stato della coda.</p>
                        )}
                    </div>
                )}

                <div className="flex items-center space-x-2">
                    <Input
                        placeholder="Cerca prodotti..."
                        value={searchTerm}
                        onChange={(e) => onSearchTermChange(e.target.value)}
                        className="flex-grow"
                        disabled={isSearchDisabled}
                    />
                </div>
            </CardHeader>
            <CardContent className="flex-grow p-0 overflow-hidden relative">
                {isLoadingMenu ? (
                    <div className="flex justify-center items-center h-full">
                        <Loader2 className="h-8 w-8 animate-spin" />
                        <span className="ml-2 text-muted-foreground">Caricamento Menu...</span>
                    </div>
                ) : selectedArea && menuItems.length === 0 && !isLoadingMenu ? (
                    <div className="flex justify-center items-center h-full text-muted-foreground">
                        {menuCategories.length === 0 ? `Nessuna categoria trovata per l'area '${selectedArea.name}'.` : `Nessun prodotto menu trovato per l'area '${selectedArea.name}'.`}
                    </div>
                ) : selectedArea && filteredMenuItems.length === 0 && !isLoadingMenu ? (
                    <div className="flex justify-center items-center h-full text-muted-foreground">Nessun prodotto trovato per la ricerca.</div>
                ) : selectedArea && !isLoadingMenu ? (
                    <ScrollArea className="h-full p-4">
                        {orderedCategoryNames.map(categoryName => (
                            <div key={categoryName} className="mb-6">
                                <h3 className="text-lg font-semibold mb-3 sticky top-0 bg-background py-1 px-2 -mx-2 border-b">{categoryName}</h3>
                                <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
                                    {itemsGroupedByCategory[categoryName]?.map((item: MenuItemDto) => (
                                        <Button
                                            key={item.id}
                                            variant="outline"
                                            onClick={() => onAddItemToCart(item)}
                                            style={justAddedItemId === item.id ? { backgroundColor: 'hsl(var(--primary) / 0.1)' } : {}}
                                            disabled={isSearchDisabled || item.scorta === 0}
                                            className={`h-auto min-h-[6rem] flex flex-col justify-center items-center text-center p-2 whitespace-normal relative transition-colors duration-100 ${item.scorta === 0 ? 'opacity-50 cursor-not-allowed' : ''}`}
                                        >
                                            {justAddedItemId === item.id ? (
                                                <CheckCircle className="absolute top-1 left-1 h-4 w-4 text-green-500" />
                                            ) : (
                                                item.isNoteRequired && <StickyNote className="absolute top-1 right-1 h-3 w-3 text-orange-500" />
                                            )}
                                            <span className="text-sm font-medium">{item.name}</span>
                                            <span className="text-xs text-muted-foreground">€{item.price.toFixed(2)}</span>
                                            <span className={`text-xs mt-1 ${item.scorta === 0 ? 'text-red-500 font-semibold' : 'text-muted-foreground'}`}>
                                                {item.scorta === null || item.scorta === undefined ? null : item.scorta === 0 ? 'Esaurito' : `Scorta: ${item.scorta}`}
                                            </span>
                                        </Button>
                                    ))}
                                </div>
                            </div>
                        ))}
                    </ScrollArea>
                ) : null}
            </CardContent>
        </Card>
    );
};

export default CashierMenuPanel;
