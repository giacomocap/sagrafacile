import { useState, useCallback } from 'react';
import { toast } from 'sonner';
import apiClient from '@/services/apiClient';
import {
    AppCartItem,
    CreateOrderDto,
    CreateOrderItemDto,
    OrderDto,
    OrderStatus,
    AreaDto,
    UserDto, // For cashier details
    ConfirmPreOrderPaymentDto, // For CashierPage pre-order confirmation
    // MenuItemDto, // For menu item details in receipt - Not directly used here
    // MenuCategoryDto, // For menu category details in receipt - Not directly used here
} from '@/types';

// Define a type for the form data expected by this hook
// This should be generic enough for both TableOrder and Cashier forms
export interface OrderFormData {
    customerName: string;
    tableNumber?: string; // Optional, for table orders
    isTakeaway: boolean;
    numberOfGuests: number;
    // Cashier-specific fields, if any, can be handled by passing them directly
    // to submitOrderToServer if they are not part of a shared form structure.
    // For now, keep it simple.
    [key: string]: any; // Allow other properties
}


export interface UseOrderHandlerResult {
    isSubmitting: boolean;
    isReceiptOpen: boolean;
    pendingOrderForReceipt: OrderDto | null;
    openReceiptDialog: (
        paymentMethod: 'Contanti' | 'POS',
        formData: OrderFormData,
        cart: AppCartItem[],
        currentArea: AreaDto | null,
        currentUser: UserDto | null,
        orderTotals: { subtotal: number; guestChargeAmount: number; takeawayChargeAmount: number; finalTotal: number },
        scannedPreOrderId?: string | null, // For Cashier pre-order
        amountPaidInput?: string // For Cashier cash payment
    ) => void;
    submitOrderToServer: (
        paymentMethod: 'Contanti' | 'POS',
        finalAmountPaid: number | null,
        formData: OrderFormData,
        cart: AppCartItem[],
        currentArea: AreaDto | null,
        currentUser: UserDto | null, // For cashierId
        scannedPreOrderId?: string | null, // For Cashier pre-order
        cashierStationId?: number | null // For Cashier station
    ) => Promise<OrderDto | null>;
    closeReceiptDialog: (success?: boolean) => void;
    clearOrderRelatedState: () => void; // To be called by the component after successful order
}

interface UseOrderHandlerProps {
    onOrderSuccess?: (order: OrderDto) => void; // Callback for successful order
}

const useOrderHandler = (props?: UseOrderHandlerProps): UseOrderHandlerResult => {
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [isReceiptOpen, setIsReceiptOpen] = useState(false);
    const [pendingOrderForReceipt, setPendingOrderForReceipt] = useState<OrderDto | null>(null);
    // pendingPaymentDetails is now managed within openReceiptDialog and passed to submitOrderToServer
    // const [pendingPaymentDetails, setPendingPaymentDetails] = useState<{ method: 'Contanti' | 'POS', amount: number | null } | null>(null);


    const openReceiptDialog = useCallback((
        paymentMethod: 'Contanti' | 'POS',
        formData: OrderFormData,
        cart: AppCartItem[],
        currentArea: AreaDto | null,
        currentUser: UserDto | null,
        orderTotals: { subtotal: number; guestChargeAmount: number; takeawayChargeAmount: number; finalTotal: number },
        scannedPreOrderId?: string | null,
        amountPaidInput?: string // Specific to CashierPage for cash
    ) => {
        if (!currentArea || !currentUser) {
            toast.error("Dati area o utente mancanti per preparare la ricevuta.");
            return;
        }
        if (cart.length === 0) {
            toast.error("Il carrello è vuoto.");
            return;
        }

        const itemsMissingNotes = cart.filter(item => item.isNoteRequired && !item.note?.trim());
        if (itemsMissingNotes.length > 0) {
            toast.error(`Aggiungere note per: ${itemsMissingNotes.map(i => i.name).join(', ')}`);
            return;
        }

        let finalAmountPaid: number | null = null;
        if (paymentMethod === 'Contanti') {
            if (amountPaidInput && amountPaidInput.trim() !== '') {
                const parsedAmount = parseFloat(amountPaidInput.replace(',', '.'));
                if (isNaN(parsedAmount) || parsedAmount < orderTotals.finalTotal) {
                    toast.error("Importo contanti non valido o insufficiente.");
                    return;
                }
                finalAmountPaid = parsedAmount;
            } else {
                finalAmountPaid = orderTotals.finalTotal; // Assume exact payment if input is empty
            }
        } else { // POS
            finalAmountPaid = orderTotals.finalTotal;
        }

        const previewOrderData: OrderDto = {
            id: scannedPreOrderId || 'PENDING_PREVIEW_ID',
            orderDateTime: new Date().toISOString(),
            areaId: currentArea.id,
            areaName: currentArea.name,
            cashierId: currentUser.id,
            cashierName: `${currentUser.firstName} ${currentUser.lastName}`,
            status: scannedPreOrderId ? OrderStatus.PreOrder : OrderStatus.Pending,
            totalAmount: orderTotals.finalTotal,
            guestCharge: orderTotals.guestChargeAmount,
            takeawayCharge: orderTotals.takeawayChargeAmount,
            paymentMethod: paymentMethod,
            amountPaid: finalAmountPaid,
            customerName: formData.customerName.trim(),
            items: cart.map(item => ({
                menuItemId: item.menuItemId,
                menuItemName: item.name,
                quantity: item.quantity,
                unitPrice: item.unitPrice,
                note: item.note || undefined,
            })),
            numberOfGuests: formData.isTakeaway ? (formData.numberOfGuests > 0 ? formData.numberOfGuests : 1) : formData.numberOfGuests, // Ensure takeaway has at least 1 if not 0
            isTakeaway: formData.isTakeaway,
            tableNumber: formData.tableNumber?.trim() || null,
            qrCodeBase64: null, dayId: null, waiterId: null, waiterName: null,
        };
        setPendingOrderForReceipt(previewOrderData);
        // setPendingPaymentDetails({ method: paymentMethod, amount: finalAmountPaid }); // Store for submitOrderToServer
        setIsReceiptOpen(true);
    }, []);


    const submitOrderToServer = useCallback(async (
        paymentMethod: 'Contanti' | 'POS',
        finalAmountPaid: number | null, // This is now passed directly
        formData: OrderFormData,
        cart: AppCartItem[],
        currentArea: AreaDto | null,
        currentUser: UserDto | null,
        scannedPreOrderId?: string | null,
        cashierStationId?: number | null
    ): Promise<OrderDto | null> => {
        if (!currentArea || !currentUser) {
            toast.error("Dati area o utente mancanti per l'invio.");
            return null;
        }
        if (cart.length === 0) {
            toast.error("Il carrello è vuoto.");
            return null;
        }
        // Note check should ideally happen before calling this, e.g., in openReceiptDialog
        // or by the component before initiating payment.

        setIsSubmitting(true);
        const orderItemsPayload: CreateOrderItemDto[] = cart.map(item => ({
            menuItemId: item.menuItemId,
            quantity: item.quantity,
            note: item.note || null,
        }));

        try {
            let createdOrder: OrderDto | null = null;

            if (scannedPreOrderId) { // Logic for Cashier Page pre-order confirmation
                const confirmPreOrderPayload: ConfirmPreOrderPaymentDto = {
                    customerName: formData.customerName.trim() || undefined,
                    paymentMethod,
                    amountPaid: finalAmountPaid,
                    items: orderItemsPayload,
                    numberOfGuests: formData.isTakeaway ? 1 : formData.numberOfGuests,
                    isTakeaway: formData.isTakeaway,
                    cashierStationId: cashierStationId,
                };
                const response = await apiClient.put<OrderDto>(`/orders/${scannedPreOrderId}/confirm-payment`, confirmPreOrderPayload);
                createdOrder = response.data;
                toast.success(`Pre-ordine ${createdOrder?.displayOrderNumber || createdOrder?.id} confermato!`);
            } else { // Logic for new order (Table Order or new Cashier order)
                const createOrderPayload: CreateOrderDto = {
                    areaId: currentArea.id,
                    customerName: formData.customerName.trim(),
                    items: orderItemsPayload,
                    paymentMethod,
                    amountPaid: finalAmountPaid,
                    numberOfGuests: formData.isTakeaway ? (formData.numberOfGuests > 0 ? formData.numberOfGuests : 1) : formData.numberOfGuests,
                    isTakeaway: formData.isTakeaway,
                    tableNumber: formData.tableNumber?.trim() || undefined,
                    cashierStationId: cashierStationId, // Will be null for table orders unless explicitly passed
                };
                const response = await apiClient.post<OrderDto>('/orders', createOrderPayload);
                createdOrder = response.data;
                toast.success(`Ordine ${createdOrder?.displayOrderNumber || createdOrder?.id} creato!`);
            }
            
            if (createdOrder) {
                setPendingOrderForReceipt(createdOrder); // Update with the actual created order for the receipt
                if (props?.onOrderSuccess) {
                    props.onOrderSuccess(createdOrder);
                }
                return createdOrder;
            }
            return null;
        } catch (error: any) {
            console.error("Error submitting order:", error);
            const errorData = error.response?.data;
            const errorMsg =
                errorData?.detail ||
                (typeof errorData === 'string' ? errorData : null) ||
                errorData?.message ||
                errorData?.title ||
                "Errore durante l'invio dell'ordine.";
            toast.error(errorMsg, { duration: 8000 });
            return null;
        } finally {
            setIsSubmitting(false);
        }
    }, [props]);

    const closeReceiptDialog = useCallback((success?: boolean) => {
        setIsReceiptOpen(false);
        if (success) {
            // The calling component should handle clearing its own cart/form state
            // by using the clearOrderRelatedState or its own logic.
        }
        // Clear pending data only after the dialog is fully closed or submission is done.
        // If not success, we might want to keep pendingOrderForReceipt for re-display or retry.
        // For simplicity now, clear on any close.
        setPendingOrderForReceipt(null);
        // setPendingPaymentDetails(null);
    }, []);

    const clearOrderRelatedState = useCallback(() => {
        setPendingOrderForReceipt(null);
        // setPendingPaymentDetails(null);
        setIsSubmitting(false);
        // setIsReceiptOpen(false); // This should be handled by closeReceiptDialog
    }, []);


    return {
        isSubmitting,
        isReceiptOpen,
        pendingOrderForReceipt,
        openReceiptDialog,
        submitOrderToServer,
        closeReceiptDialog,
        clearOrderRelatedState,
    };
};

export default useOrderHandler;
