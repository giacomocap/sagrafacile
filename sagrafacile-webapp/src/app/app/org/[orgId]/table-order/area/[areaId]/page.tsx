"use client";

import React, { useEffect, useState, useRef } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
    OrderDto,
    OrderStatus,
    MenuItemDto,
    AppCartItem,
    UserDto,
    PaginatedResult,
} from "@/types";
import { toast } from "sonner";
import { useForm, Controller, SubmitHandler } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import {
    Plus,
    Minus,
    Trash2,
    StickyNote,
    ShoppingCart,
    X,
    Loader2,
    AlertCircle,
    CreditCard,
    Coins,
    History,
    ScanLine,
    Home,
} from "lucide-react";
import Link from "next/link";
import { useParams } from "next/navigation";
import apiClient from "@/services/apiClient";
import { useAuth } from "@/contexts/AuthContext";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from "@/components/ui/table";
import ReceiptDialog from '@/components/ReceiptDialog';
import useMenuAndAreaLoader from "@/hooks/useMenuAndAreaLoader";
import useAppCart from "@/hooks/useAppCart";
import useOrderHandler from "@/hooks/useOrderHandler";
import { getOrderStatusBadgeVariant } from "@/lib/utils";
import OrderQrScanner from "@/components/shared/OrderQrScanner";
import OrderConfirmationView from "@/components/shared/OrderConfirmationView";

// Zod schema for the table order form
const tableOrderFormSchema = z.object({
    customerName: z.string().min(1, "Il nome del cliente è obbligatorio."),
    tableNumber: z.string().min(1, "Il numero del tavolo è obbligatorio."),
    isTakeaway: z.boolean(),
    numberOfGuests: z.coerce
        .number({
            required_error: "Il numero degli ospiti è obbligatorio.",
            invalid_type_error: "Deve essere un numero"
        })
        .int("Deve essere un numero intero.")
        .min(0, "Deve essere almeno 0."), // Allow 0 for takeaway
}).refine(data => {
    if (data.isTakeaway) {
        return data.numberOfGuests === 0; // Must be 0 if takeaway
    }
    return data.numberOfGuests >= 1; // Must be at least 1 if not takeaway
}, {
    message: "Per l'asporto i coperti devono essere 0. Per ordini al tavolo, almeno 1.",
    path: ["numberOfGuests"],
});

type TableOrderFormValues = z.infer<typeof tableOrderFormSchema>;

const MobileTableOrderPage = () => {
    const params = useParams();
    const orgId = params.orgId as string; // orgId is a guaranteed part of the route
    const { user, isLoading: isAuthLoading } = useAuth();

    const [isCartSheetOpen, setIsCartSheetOpen] = useState(false);
    const cartSheetRef = useRef<HTMLDivElement>(null);

    const form = useForm<TableOrderFormValues>({
        resolver: zodResolver(tableOrderFormSchema),
        defaultValues: {
            customerName: '',
            tableNumber: '',
            isTakeaway: false,
            numberOfGuests: 0,
        }
    });
    const { register, handleSubmit: handleFormSubmit, control, watch, setValue, formState: { errors }, getValues } = form;

    // --- Custom Hooks ---
    const {
        currentArea,
        menuCategories,
        menuItemsWithCategoryName: menuItems,
        isLoadingData,
        errorData,
    } = useMenuAndAreaLoader();

    const {
        cart,
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

    const isTakeawayWatched = watch('isTakeaway');
    const numberOfGuestsWatched = watch('numberOfGuests');

    const orderTotals = React.useMemo(() => {
        const subtotal = cartTotal;
        let guestChargeAmount = 0;
        let takeawayChargeAmount = 0;

        if (currentArea) {
            if (!isTakeawayWatched && currentArea.guestCharge > 0 && numberOfGuestsWatched > 0) {
                guestChargeAmount = currentArea.guestCharge * numberOfGuestsWatched;
            }
            if (isTakeawayWatched && currentArea.takeawayCharge > 0) {
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
    }, [cartTotal, currentArea, isTakeawayWatched, numberOfGuestsWatched]);

    const {
        isSubmitting,
        isReceiptOpen,
        pendingOrderForReceipt,
        openReceiptDialog: openPaymentDialog,
        submitOrderToServer,
        closeReceiptDialog,
    } = useOrderHandler({
        onOrderSuccess: () => {
            clearCart();
            form.reset();
            setIsCartSheetOpen(false);
        }
    });

    // Past Orders State (kept local as it's specific to this page's UI)
    const [isPastOrdersDialogOpen, setIsPastOrdersDialogOpen] = useState(false);
    const [pastOrders, setPastOrders] = useState<OrderDto[]>([]);
    const [isLoadingPastOrders, setIsLoadingPastOrders] = useState(false);
    const [pastOrdersError, setPastOrdersError] = useState<string | null>(null);
    const [selectedPastOrderForDetail, setSelectedPastOrderForDetail] = useState<OrderDto | null>(null);
    const [isPastOrderDetailDialogOpen, setIsPastOrderDetailDialogOpen] = useState(false);

    // QR Scanner state
    const [isScanDialogOpen, setIsScanDialogOpen] = useState(false);
    const [scannedOrderId, setScannedOrderId] = useState<string | null>(null);
    const [showScanner, setShowScanner] = useState(true);

    useEffect(() => {
        const currentNumberOfGuests = getValues("numberOfGuests");
        if (isTakeawayWatched) {
            if (currentNumberOfGuests !== 0) {
                setValue('numberOfGuests', 0, { shouldValidate: true });
            }
        }
    }, [isTakeawayWatched, setValue, getValues]);

    useEffect(() => {
        document.body.style.overflow = isCartSheetOpen ? 'hidden' : '';
        return () => { document.body.style.overflow = ''; };
    }, [isCartSheetOpen]);

    const handleAddToCart = (item: MenuItemDto, categoryName: string) => {
        addToCart(item, categoryName);
        toast.success(`${item.name} aggiunto al carrello`);
    };

    const handleSaveNote = () => {
        saveNote(currentNoteValue);
    };

    const handlePayment = (paymentMethod: 'Contanti' | 'POS') => {
        openPaymentDialog(
            paymentMethod,
            getValues(),
            cart,
            currentArea,
            user as unknown as UserDto | null,
            orderTotals
        );
    };

    const onFinalSubmit: SubmitHandler<TableOrderFormValues> = () => {
        toast.info("Seleziona un metodo di pagamento.");
    };

    const handleClearOrder = () => {
        clearCart();
        form.reset();
        setIsCartSheetOpen(false);
    };

    const handleCloseReceipt = (success?: boolean) => {
        closeReceiptDialog(success);
        if (success) {
            handleClearOrder();
        }
    };

    // --- Past Orders Logic ---
    const fetchPastOrders = async () => {
        if (!currentArea?.id) return;
        setIsLoadingPastOrders(true);
        setPastOrdersError(null);
        try {
            const response = await apiClient.get<PaginatedResult<OrderDto>>('/orders', {
                params: {
                    areaId: currentArea.id,
                    sortBy: 'orderDateTime',
                    sortAscending: false,
                }
            });
            setPastOrders(response.data.items);
        } catch (error) {
            console.error("Error fetching past orders:", error);
            setPastOrdersError("Impossibile caricare lo storico ordini.");
            toast.error("Errore nel caricamento dello storico ordini.");
        } finally {
            setIsLoadingPastOrders(false);
        }
    };

    const handleOpenPastOrdersDialog = () => {
        fetchPastOrders();
        setIsPastOrdersDialogOpen(true);
    };

    const handleViewPastOrderDetail = (order: OrderDto) => {
        setSelectedPastOrderForDetail(order);
        setIsPastOrderDetailDialogOpen(true);
    };

    if (isLoadingData || isAuthLoading) {
        return (
            <div className="flex justify-center items-center h-screen">
                <Loader2 className="h-8 w-8 animate-spin mr-2 text-primary" />
                <span className="text-foreground font-medium">Caricamento...</span>
            </div>
        );
    }

    if (errorData) {
        return (
            <div className="flex flex-col justify-center items-center h-screen p-4 text-center">
                <AlertCircle className="h-12 w-12 text-red-500 mb-4" />
                <h2 className="text-xl font-semibold text-red-600 mb-2">Errore</h2>
                <p className="text-muted-foreground mb-4">{errorData}</p>
                <Button onClick={() => window.location.reload()}>Riprova</Button>
            </div>
        );
    }

    if (!currentArea || !user) {
        return <div className="flex justify-center items-center h-screen">Dati area o utente non disponibili.</div>;
    }

    return (
        <div className="flex flex-col h-full overflow-hidden bg-background">
            <header className="bg-card shadow-md p-4 sticky top-0 z-10 flex items-center justify-between">
                <div className="flex-1">
                    {orgId && (
                        <Link href={`/app/org/${orgId}`}>
                            <Button variant="outline" size="icon" aria-label="Torna alla dashboard admin">
                                <Home className="h-5 w-5" />
                            </Button>
                        </Link>
                    )}
                </div>
                <h1 className="text-xl font-semibold text-center text-foreground flex-grow">
                    {currentArea.name}
                </h1>
                <div className="flex-1 flex justify-end gap-2">
                    <Button variant="outline" size="sm" onClick={() => {
                        setScannedOrderId(null);
                        setShowScanner(true);
                        setIsScanDialogOpen(true);
                    }}>
                        <ScanLine className="mr-2 h-4 w-4" />
                        Scansiona
                    </Button>
                    <Button variant="outline" size="sm" onClick={handleOpenPastOrdersDialog}>
                        <History className="mr-2 h-4 w-4" />
                        Storico
                    </Button>
                </div>
            </header>

            <div className="flex-1 overflow-y-auto p-4 space-y-6">
                {menuCategories.map((category) => {
                    const itemsInCategory = menuItems.filter(item => item.menuCategoryId === category.id);
                    if (itemsInCategory.length === 0) return null;
                    return (
                        <div key={category.id} className="space-y-3">
                            <h2 className="text-lg font-semibold border-b pb-2 text-foreground">
                                {category.name}
                            </h2>
                            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                                {itemsInCategory.map((item) => (
                                    <div
                                        key={item.id}
                                        className={`bg-card rounded-lg shadow-sm p-3 flex justify-between items-center border hover:border-primary/50 transition-colors ${item.scorta === 0 ? 'opacity-60' : ''}`}
                                    >
                                        <div className="flex-1">
                                            <div className="flex justify-between items-start">
                                                <h3 className="font-medium text-card-foreground">{item.name}</h3>
                                                <span className="font-semibold ml-2 text-primary">€{item.price?.toFixed(2)}</span>
                                            </div>
                                            {item.description && (
                                                <p className="text-sm text-muted-foreground mt-1">{item.description}</p>
                                            )}
                                            <div className="text-xs mt-1">
                                                {item.isNoteRequired && (
                                                    <span className="text-destructive mr-2">Nota richiesta</span>
                                                )}
                                                {item.scorta !== null && (
                                                    <span className={item.scorta === 0 ? 'text-red-500 font-semibold' : 'text-muted-foreground'}>
                                                        Scorta: {item.scorta === 0 ? 'Esaurito' : item.scorta}
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                        <button
                                            className={`ml-3 p-2 rounded-full text-primary-foreground transition-colors ${item.scorta === 0 ? 'bg-muted cursor-not-allowed' : 'bg-primary hover:bg-primary/90'}`}
                                            onClick={() => item.scorta !== 0 && handleAddToCart(item, category.name)}
                                            disabled={item.scorta === 0}
                                        >
                                            <Plus size={16} />
                                        </button>
                                    </div>
                                ))}
                            </div>
                        </div>
                    );
                })}

                {/* Spacer to prevent the floating cart button from overlapping the last item */}
                {cart.length > 0 && <div className="h-24" />}
            </div>

            {cart.length > 0 && (
                <div
                    className="fixed inset-x-0 flex justify-center z-20"
                    style={{ bottom: 'max(1.5rem, calc(1.5rem + env(safe-area-inset-bottom)))' }}
                >
                    <button
                        className="bg-primary text-primary-foreground px-6 py-3 rounded-full shadow-lg flex items-center hover:bg-primary/90 transition-colors"
                        onClick={() => setIsCartSheetOpen(true)}
                    >
                        <ShoppingCart size={20} className="mr-2" />
                        <span className="font-medium">Vedi Ordine</span>
                        <Badge className="ml-2 bg-primary-foreground text-primary">
                            {cart.reduce((sum, item) => sum + item.quantity, 0)}
                        </Badge>
                        <span className="ml-3 font-medium">€{cartTotal.toFixed(2)}</span>
                    </button>
                </div>
            )}

            {isCartSheetOpen && (
                <>
                    <div className="fixed inset-0 bg-black/60 z-30" onClick={() => setIsCartSheetOpen(false)} />
                    <div
                        ref={cartSheetRef}
                        className="fixed inset-0 bg-card z-40 shadow-xl transition-transform flex flex-col sm:inset-auto sm:bottom-0 sm:left-0 sm:right-0 sm:rounded-t-xl"
                        style={{
                            maxHeight: 'max(100vh, -webkit-fill-available)',
                        }}
                    >
                        <div className="sheet-header flex justify-between items-center flex-shrink-0 bg-muted/50 border-b w-full p-4 sticky top-0 z-50"
                            style={{ paddingTop: 'max(1rem, env(safe-area-inset-top))' }}>
                            <h2 className="text-lg font-semibold text-foreground">Il tuo ordine</h2>
                            <Button variant="ghost" size="sm" onClick={() => setIsCartSheetOpen(false)} className="text-muted-foreground hover:bg-muted">
                                <span className="mr-1 inline">Chiudi Riepilogo</span>
                                <X size={20} />
                            </Button>
                        </div>

                        <div className="flex-1 overflow-y-auto p-4 space-y-4">
                            {cart.length === 0 ? (
                                <p className="text-center text-muted-foreground py-8">Il carrello è vuoto.</p>
                            ) : (
                                Object.entries(
                                    cart.reduce((acc, item) => {
                                        if (!acc[item.categoryName]) acc[item.categoryName] = [];
                                        acc[item.categoryName].push(item);
                                        return acc;
                                    }, {} as Record<string, AppCartItem[]>)
                                ).map(([categoryName, items]) => (
                                    <div key={categoryName} className="space-y-3">
                                        <h3 className="text-md font-semibold text-foreground border-b pb-1">{categoryName}</h3>
                                        {items.map((item: AppCartItem) => (
                                            <div key={item.cartItemId} className="border-b pb-3 last:border-b-0">
                                                <div className="flex justify-between items-center">
                                                    <div className="flex-1">
                                                        <h4 className="font-medium text-card-foreground">{item.name}</h4>
                                                        <p className="text-sm text-muted-foreground">€{item.unitPrice?.toFixed(2)}</p>
                                                        {item.isNoteRequired && !item.note && <span className="text-xs text-red-500">Nota richiesta</span>}
                                                    </div>
                                                    <div className="flex items-center gap-2">
                                                        <button onClick={() => decreaseQuantity(item.cartItemId)} className="p-1"><Minus size={16} /></button>
                                                        <span>{item.quantity}</span>
                                                        <button onClick={() => increaseQuantity(item.cartItemId)} className="p-1"><Plus size={16} /></button>
                                                    </div>
                                                </div>
                                                {item.note && <p className="text-sm mt-1 bg-muted/50 p-2 rounded border border-border">Nota: {item.note}</p>}
                                                <div className="flex justify-end gap-2 mt-2">
                                                    <Button variant="link" size="sm" className="text-primary" onClick={() => openNoteDialog(item)}>
                                                        <StickyNote size={14} className="mr-1" /> {item.note ? 'Modifica' : 'Aggiungi'} nota
                                                    </Button>
                                                    <Button variant="link" size="sm" className="text-destructive" onClick={() => removeFromCart(item.cartItemId)}>
                                                        <Trash2 size={14} className="mr-1" /> Rimuovi
                                                    </Button>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                ))
                            )}
                        </div>

                        {cart.length > 0 && (
                            <div className="flex-shrink-0 p-4 border-t bg-muted/50 shadow-inner"
                                style={{ paddingBottom: 'max(1rem, env(safe-area-inset-bottom))' }}>
                                <form onSubmit={handleFormSubmit(onFinalSubmit)} className="space-y-4">
                                    <div className="flex gap-x-4 items-end">
                                        <div className="flex-grow">
                                            <Label htmlFor="customerName" className="text-foreground">Nome Cliente</Label>
                                            <Input id="customerName" type="text" {...register("customerName")} disabled={isSubmitting} className="mt-1" />
                                            {errors.customerName && <p className="text-sm text-red-500 mt-1">{errors.customerName.message}</p>}
                                        </div>
                                        <div className="w-1/4">
                                            <Label htmlFor="tableNumber" className="text-foreground">Numero Tavolo</Label>
                                            <Input id="tableNumber" type="text" {...register("tableNumber")} disabled={isSubmitting} className="mt-1" />
                                            {errors.tableNumber && <p className="text-sm text-red-500 mt-1">{errors.tableNumber.message}</p>}
                                        </div>
                                    </div>
                                    <div className="grid grid-cols-2 gap-4">
                                        <div>
                                            <Label className="text-foreground block mb-1">Tipo ordine</Label>
                                            <div className="flex items-center h-10 mt-1">
                                                <Controller name="isTakeaway" control={control} render={({ field }) => (
                                                    <Checkbox id="isTakeaway" checked={field.value} onCheckedChange={field.onChange} disabled={isSubmitting} />
                                                )} />
                                                <Label htmlFor="isTakeaway" className="text-sm ml-2 font-medium text-foreground">Asporto</Label>
                                            </div>
                                        </div>
                                        <div>
                                            <Label htmlFor="numberOfGuests" className="text-foreground block mb-1">Coperti</Label>
                                            <Input id="numberOfGuests" type="number" {...register("numberOfGuests", { valueAsNumber: true })} disabled={isSubmitting || isTakeawayWatched} className="h-10 mt-1" min="0" />
                                            {errors.numberOfGuests && <p className="text-sm text-red-500 mt-1">{errors.numberOfGuests.message}</p>}
                                        </div>
                                    </div>
                                    <div className="space-y-1 text-sm mb-3">
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
                                    <div className="flex justify-between items-center mb-3 pt-3 border-t">
                                        <h3 className="text-lg font-semibold text-foreground">Totale:</h3>
                                        <h3 className="text-lg font-semibold text-foreground">€{orderTotals.finalTotal.toFixed(2)}</h3>
                                    </div>
                                    <div className="grid grid-cols-2 gap-2">
                                        <Button type="button" onClick={() => handlePayment('Contanti')} className="py-3" disabled={isSubmitting}>
                                            <Coins className="mr-2 h-5 w-5" /> Paga Contanti
                                        </Button>
                                        <Button type="button" onClick={() => handlePayment('POS')} className="py-3" disabled={isSubmitting}>
                                            <CreditCard className="mr-2 h-5 w-5" /> Paga POS
                                        </Button>
                                    </div>
                                    <Button type="button" variant="outline" onClick={handleClearOrder} className="w-full mt-2" disabled={isSubmitting}>
                                        Cancella Ordine
                                    </Button>
                                </form>
                            </div>
                        )}
                    </div>
                </>
            )}

            <AlertDialog open={isNoteDialogOpen} onOpenChange={closeNoteDialog}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Nota per {currentItemForNote?.name}</AlertDialogTitle>
                        <AlertDialogDescription>
                            {currentItemForNote?.isNoteRequired ? 'Nota richiesta. ' : 'Nota opzionale. '}
                            {currentItemForNote?.noteSuggestion && `Suggerimento: ${currentItemForNote.noteSuggestion}`}
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <Textarea value={currentNoteValue} onChange={(e) => setCurrentNoteValue(e.target.value)} rows={3} />
                    <AlertDialogFooter>
                        <AlertDialogCancel>Annulla</AlertDialogCancel>
                        <AlertDialogAction onClick={handleSaveNote} disabled={currentItemForNote?.isNoteRequired && !currentNoteValue.trim()}>Salva Nota</AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>

            {pendingOrderForReceipt && (
                <ReceiptDialog
                    order={pendingOrderForReceipt}
                    isOpen={isReceiptOpen}
                    onClose={handleCloseReceipt}
                    onSubmitOrder={() => {
                        const paymentMethod = pendingOrderForReceipt.paymentMethod as 'Contanti' | 'POS';
                        const amountPaid = pendingOrderForReceipt.amountPaid ?? null;
                        return submitOrderToServer(paymentMethod, amountPaid, getValues(), cart, currentArea, user as unknown as UserDto | null);
                    }}
                    menuItems={menuItems}
                    menuCategories={menuCategories}
                />
            )}

            {/* Past Orders Dialog */}
            <AlertDialog open={isPastOrdersDialogOpen} onOpenChange={setIsPastOrdersDialogOpen}>
                <AlertDialogContent className="max-w-3xl">
                    <AlertDialogHeader>
                        <AlertDialogTitle>Storico Ordini Recenti - {currentArea.name}</AlertDialogTitle>
                        <AlertDialogDescription>
                            Visualizza gli ordini recenti per quest'area. Clicca su un ordine per i dettagli.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <div className="max-h-[60vh] overflow-hidden flex flex-col">
                        {isLoadingPastOrders ? (
                            <div className="flex justify-center items-center py-10 flex-1">
                                <Loader2 className="h-8 w-8 animate-spin text-primary" />
                            </div>
                        ) : pastOrdersError ? (
                            <div className="text-red-500 py-10 text-center flex-1">{pastOrdersError}</div>
                        ) : pastOrders.length === 0 ? (
                            <p className="text-muted-foreground text-center py-10 flex-1">Nessun ordine recente trovato.</p>
                        ) : (
                            <ScrollArea className="flex-1 pr-2">
                                <Table>
                                    <TableHeader className="sticky top-0 bg-card z-10">
                                        <TableRow>
                                            <TableHead>Ordine</TableHead>
                                            <TableHead>Cliente</TableHead>
                                            <TableHead>Tavolo</TableHead>
                                            <TableHead>Ora</TableHead>
                                            <TableHead>Stato</TableHead>
                                            <TableHead className="text-right">Totale</TableHead>
                                        </TableRow>
                                    </TableHeader>
                                    <TableBody>
                                        {pastOrders.map((order) => (
                                            <TableRow key={order.id} onClick={() => handleViewPastOrderDetail(order)} className="cursor-pointer hover:bg-muted/50">
                                                <TableCell className="font-medium">{order.displayOrderNumber || order.id.substring(0, 8)}</TableCell>
                                                <TableCell>{order.customerName}</TableCell>
                                                <TableCell>{order.tableNumber || '-'}</TableCell>
                                                <TableCell>{new Date(order.orderDateTime).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}</TableCell>
                                                <TableCell>
                                                    <Badge variant={getOrderStatusBadgeVariant(order.status)}>
                                                        {OrderStatus[order.status] || 'Sconosciuto'}
                                                    </Badge>
                                                </TableCell>
                                                <TableCell className="text-right">€{order.totalAmount.toFixed(2)}</TableCell>
                                            </TableRow>
                                        ))}
                                    </TableBody>
                                </Table>
                            </ScrollArea>
                        )}
                    </div>
                    <AlertDialogFooter className="mt-4">
                        <AlertDialogCancel>Chiudi</AlertDialogCancel>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>

            {/* Past Order Detail Dialog (using ReceiptDialog) */}
            {selectedPastOrderForDetail && (
                <ReceiptDialog
                    order={selectedPastOrderForDetail}
                    isOpen={isPastOrderDetailDialogOpen}
                    onClose={() => {
                        setIsPastOrderDetailDialogOpen(false);
                        setSelectedPastOrderForDetail(null);
                    }}
                    isReprintMode={true}
                    onSubmitOrder={async () => {
                        setIsPastOrderDetailDialogOpen(false);
                        setSelectedPastOrderForDetail(null);
                        return null;
                    }}
                    menuItems={menuItems}
                    menuCategories={menuCategories}
                />
            )}

            {/* Scan Dialog */}
            <AlertDialog open={isScanDialogOpen} onOpenChange={setIsScanDialogOpen}>
                <AlertDialogContent className="max-w-lg">
                    <AlertDialogHeader>
                        <AlertDialogTitle>{scannedOrderId ? 'Dettaglio Ordine Scansionato' : 'Scansiona QR Code'}</AlertDialogTitle>
                        <AlertDialogDescription>
                            {scannedOrderId ? 'Controlla i dettagli dell\'ordine.' : 'Inquadra il QR code sulla ricevuta per visualizzare o confermare un ordine.'}
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    
                    {!scannedOrderId ? (
                        <OrderQrScanner
                            showScanner={showScanner}
                            onShowScannerChange={setShowScanner}
                            onScanSuccess={setScannedOrderId}
                            forceShowScanner={true}
                        />
                    ) : (
                        <OrderConfirmationView
                            orderId={scannedOrderId}
                            onCancel={() => {
                                setScannedOrderId(null);
                                setIsScanDialogOpen(false);
                            }}
                            onSuccess={() => {
                                setScannedOrderId(null);
                                setIsScanDialogOpen(false);
                                // Optionally, you can redirect to the waiter page or just close
                            }}
                        />
                    )}

                    <AlertDialogFooter className="mt-4">
                         <AlertDialogCancel onClick={() => {
                            setScannedOrderId(null);
                            setIsScanDialogOpen(false);
                        }}>Chiudi</AlertDialogCancel>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </div>
    );
};

export default MobileTableOrderPage;
