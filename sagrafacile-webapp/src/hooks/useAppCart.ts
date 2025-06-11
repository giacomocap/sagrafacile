import { useState, useMemo, useCallback } from 'react';
import { toast } from 'sonner';
import { MenuItemDto, AppCartItem } from '@/types';

export interface UseAppCartResult {
    cart: AppCartItem[];
    addToCart: (item: MenuItemDto, categoryName: string, options?: { isPreOrderLoad?: boolean }) => void;
    increaseQuantity: (cartItemId: string) => void;
    decreaseQuantity: (cartItemId: string) => void;
    removeFromCart: (cartItemId: string) => void;
    openNoteDialog: (item: AppCartItem) => void;
    saveNote: (note: string) => void;
    closeNoteDialog: () => void;
    clearCart: () => void;
    cartTotal: number;
    isNoteDialogOpen: boolean;
    currentItemForNote: AppCartItem | null;
    currentNoteValue: string; // Expose currentNoteValue for direct binding if needed
    setCurrentNoteValue: (value: string) => void; // Allow external update to note value
    setCart: React.Dispatch<React.SetStateAction<AppCartItem[]>>; // Allow direct cart manipulation if needed (e.g., loading pre-order)
}

const useAppCart = (initialCart: AppCartItem[] = []): UseAppCartResult => {
    const [cart, setCart] = useState<AppCartItem[]>(initialCart);
    const [isNoteDialogOpen, setIsNoteDialogOpen] = useState(false);
    const [currentItemForNote, setCurrentItemForNote] = useState<AppCartItem | null>(null);
    const [currentNoteValue, setCurrentNoteValue] = useState(''); // Renamed from currentNote

    const addToCart = useCallback((item: MenuItemDto, categoryName: string, options?: { isPreOrderLoad?: boolean }) => {
        // For non-pre-order additions, or if it's a pre-order load but the item is not note-required,
        // try to increment quantity if a similar non-noted item exists.
        const existingItemIndex = cart.findIndex(
            cartItem => cartItem.menuItemId === item.id && !cartItem.note && !item.isNoteRequired
        );

        if (existingItemIndex > -1 && !options?.isPreOrderLoad && !item.isNoteRequired) {
            const updatedCart = [...cart];
            updatedCart[existingItemIndex].quantity += 1;
            updatedCart[existingItemIndex].totalPrice = updatedCart[existingItemIndex].quantity * updatedCart[existingItemIndex].unitPrice;
            setCart(updatedCart);
        } else {
            // Add as new item if:
            // - It's a pre-order load (each pre-order item is distinct)
            // - The item requires a note (each noted item is distinct unless notes are identical, which is complex to merge)
            // - No existing non-noted item was found
            const newCartItem: AppCartItem = {
                cartItemId: `cart-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`,
                menuItemId: item.id,
                name: item.name,
                quantity: 1,
                unitPrice: item.price,
                totalPrice: item.price,
                categoryName,
                isNoteRequired: item.isNoteRequired,
                noteSuggestion: item.noteSuggestion,
                note: null, // Default to null, will be set if required or by user
            };
            setCart(prevCart => [...prevCart, newCartItem]);
        }
        if (!options?.isPreOrderLoad) {
            toast.success(`${item.name} aggiunto al carrello`);
        }
    }, [cart]); // Added cart to dependency array

    const increaseQuantity = useCallback((cartItemId: string) => {
        setCart(prevCart =>
            prevCart.map(item =>
                item.cartItemId === cartItemId
                    ? { ...item, quantity: item.quantity + 1, totalPrice: (item.quantity + 1) * item.unitPrice }
                    : item
            )
        );
    }, []);

    const decreaseQuantity = useCallback((cartItemId: string) => {
        setCart(prevCart =>
            prevCart
                .map(item =>
                    item.cartItemId === cartItemId && item.quantity > 1
                        ? { ...item, quantity: item.quantity - 1, totalPrice: (item.quantity - 1) * item.unitPrice }
                        : item
                )
                .filter(item => item.quantity > 0 || item.cartItemId !== cartItemId) // Keep item if quantity > 0 OR it's not the target item
        );
    }, []);


    const removeFromCart = useCallback((cartItemId: string) => {
        setCart(prevCart => prevCart.filter(item => item.cartItemId !== cartItemId));
        // Consider if setIsCartSheetOpen(false) logic from TableOrderPage needs to be handled by the component using the hook
    }, []);

    const openNoteDialog = useCallback((item: AppCartItem) => {
        setCurrentItemForNote(item);
        setCurrentNoteValue(item.note || '');
        setIsNoteDialogOpen(true);
    }, []);

    const saveNote = useCallback((noteToSave: string) => { // Accept note as parameter
        if (currentItemForNote) {
            setCart(prevCart =>
                prevCart.map(item =>
                    item.cartItemId === currentItemForNote.cartItemId ? { ...item, note: noteToSave.trim() || null } : item
                )
            );
        }
        setIsNoteDialogOpen(false);
        setCurrentItemForNote(null);
        // setCurrentNoteValue(''); // Clear after saving
    }, [currentItemForNote]);

    const closeNoteDialog = useCallback(() => {
        setIsNoteDialogOpen(false);
        setCurrentItemForNote(null);
        // setCurrentNoteValue(''); // Optionally clear note value on cancel
    }, []);

    const clearCart = useCallback(() => {
        setCart([]);
        toast.success("Carrello svuotato.");
    }, []);

    const cartTotal = useMemo(() => {
        return cart.reduce((sum, item) => sum + item.totalPrice, 0);
    }, [cart]);

    return {
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
        setCart, // Expose setCart
    };
};

export default useAppCart;
