'use client';

import React, { useState, useMemo, useRef, useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { OrganizationDto, AreaDto, MenuCategoryDto, MenuItemDto, PreOrderDto, OrderDto } from '@/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { toast } from 'sonner';
import apiClient from '@/services/apiClient';
import { Plus, Minus, Trash2, StickyNote, ShoppingCart, ChevronRight, X, Loader2, AlertCircle } from 'lucide-react'; // Added Loader2, AlertCircle
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
import { Badge } from "@/components/ui/badge";

interface CartItem extends MenuItemDto {
    quantity: number;
    note?: string | null;
    cartItemId: string;
}

export default function PreOrderPage() {
    const params = useParams();
    const orgSlug = params.orgSlug as string; // Extract slug
    const areaSlug = params.areaSlug as string; // Extract slug

    // State for fetched data
    const [organization, setOrganization] = useState<OrganizationDto | null>(null);
    const [area, setArea] = useState<AreaDto | null>(null);
    const [menuCategories, setMenuCategories] = useState<MenuCategoryDto[]>([]);
    const [menuItems, setMenuItems] = useState<MenuItemDto[]>([]);

    // State for loading and errors during data fetch
    const [isLoadingData, setIsLoadingData] = useState(true);
    const [errorData, setErrorData] = useState<string | null>(null);

    // State for UI interaction
    const [cart, setCart] = useState<CartItem[]>([]);
    const [customerName, setCustomerName] = useState('');
    const [customerEmail, setCustomerEmail] = useState('');
    const [isSubmitting, setIsSubmitting] = useState(false); // Renamed from isLoading
    const [isNoteDialogOpen, setIsNoteDialogOpen] = useState(false);
    const [currentItemForNote, setCurrentItemForNote] = useState<CartItem | null>(null);
    const [currentNote, setCurrentNote] = useState('');
    const [activeCategory, setActiveCategory] = useState<string | null>(null);
    const [isCartSheetOpen, setIsCartSheetOpen] = useState(false);
    const [sheetPosition, setSheetPosition] = useState(0);
    const cartSheetRef = useRef<HTMLDivElement>(null);
    const startY = useRef(0);
    const currentY = useRef(0);
    const isDragging = useRef(false);
    const router = useRouter();

    // --- Data Fetching Effect ---
    useEffect(() => {
        const fetchData = async () => {
            setIsLoadingData(true);
            setIsLoadingData(true); // Ensure loading starts
            setErrorData(null);
            setOrganization(null);
            setArea(null);
            setMenuCategories([]);
            setMenuItems([]);

            // Ensure slugs are available before fetching
            if (!orgSlug || !areaSlug) {
                setErrorData("Slug dell'Organizzazione o dell'Area mancante dall'URL.");
                setIsLoadingData(false);
                return;
            }

            try {
                // 1. Fetch Organization
                const orgRes = await apiClient.get<OrganizationDto>(`/public/organizations/${orgSlug}`);
                if (!orgRes.data) throw new Error("Organizzazione non trovata.");
                setOrganization(orgRes.data);

                // 2. Fetch Area
                const areaRes = await apiClient.get<AreaDto>(`/public/organizations/${orgSlug}/areas/${areaSlug}`);
                if (!areaRes.data || areaRes.data.organizationId !== orgRes.data.id) {
                    throw new Error("Area non trovata o non appartiene all'organizzazione.");
                }
                setArea(areaRes.data);
                const currentAreaId = areaRes.data.id;

                // 3. Fetch Menu Categories for the Area
                const catRes = await apiClient.get<MenuCategoryDto[]>(`/public/areas/${currentAreaId}/menucategories`);
                const fetchedCategories = catRes.data || [];
                setMenuCategories(fetchedCategories);

                if (fetchedCategories.length === 0) {
                    console.log(`No menu categories found for area ${currentAreaId}.`);
                    setIsLoadingData(false);
                    return; // No items to fetch if no categories
                }

                // 4. Fetch Menu Items for all Categories
                const categoryIds = fetchedCategories.map(cat => cat.id);
                const itemPromises = categoryIds.map(catId =>
                    apiClient.get<MenuItemDto[]>(`/public/menucategories/${catId}/menuitems`)
                );
                const itemResponses = await Promise.all(itemPromises);
                const allItems = itemResponses.flatMap(res => res.data || []);
                setMenuItems(allItems);

            } catch (error: unknown) {
                console.error("Error fetching pre-order data:", error);
                let errorMsg = "Impossibile caricare i dati della pagina di pre-ordine.";
                if (typeof error === 'object' && error !== null) {
                    if ('message' in error) {
                        errorMsg = String((error as { message: string }).message);
                    }
                    if ('response' in error && typeof (error as { response?: { data?: { message?: string } } }).response?.data?.message === 'string') {
                        errorMsg = String((error as { response: { data: { message: string } } }).response.data.message);
                    }
                }
                setErrorData(errorMsg);
                // Optionally redirect or show a more prominent error
                // if (!organization) router.push('/not-found'); // Example redirect
            } finally {
                setIsLoadingData(false);
            }
        };

        // Call fetchData directly since slugs are derived from params
        fetchData();

    }, [orgSlug, areaSlug, router]); // Depend on extracted slugs

    // --- Cart Logic ---
    const handleAddToCart = (item: MenuItemDto) => {
        // Find the index of the *first* item in the cart with the same menu item ID.
        const existingItemIndex = cart.findIndex(cartItem => cartItem.id === item.id);

        // If an item with the same ID exists, increment its quantity.
        if (existingItemIndex > -1) {
            const updatedCart = [...cart];
            updatedCart[existingItemIndex].quantity += 1;
            // If the newly added item requires a note and the existing one doesn't have one yet,
            // add the default suggestion. This handles adding a note-required item to an existing non-noted one.
            if (item.isNoteRequired && !updatedCart[existingItemIndex].note) {
                 updatedCart[existingItemIndex].note = item.noteSuggestion || '';
            }
            setCart(updatedCart);
        // Otherwise (item not in cart yet), add it as a new entry.
        } else {
            setCart([...cart, {
                ...item,
                quantity: 1,
                // Use Date.now() + random number for a simple unique ID
                cartItemId: `cart-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`,
                note: item.isNoteRequired ? item.noteSuggestion || '' : null
            }]);
        }
        toast.success(`${item.name} aggiunto al carrello`);

        // Removed the logic that automatically flashed the cart sheet on first item add.
    };

    const handleIncreaseQuantity = (cartItemId: string) => {
        setCart(cart.map(item =>
            item.cartItemId === cartItemId ? { ...item, quantity: item.quantity + 1 } : item
        ));
    };

    const handleDecreaseQuantity = (cartItemId: string) => {
        setCart(cart.map(item =>
            item.cartItemId === cartItemId ? { ...item, quantity: Math.max(1, item.quantity - 1) } : item
        ));
    };

    const handleRemoveFromCart = (cartItemId: string) => {
        setCart(cart.filter(item => item.cartItemId !== cartItemId));
        
        // Close sheet if cart becomes empty
        if (cart.length === 1) {
            setIsCartSheetOpen(false);
        }
    };

    const handleOpenNoteDialog = (item: CartItem) => {
        setCurrentItemForNote(item);
        setCurrentNote(item.note || item.noteSuggestion || '');
        setIsNoteDialogOpen(true);
    };

    const handleSaveNote = () => {
        if (currentItemForNote) {
            setCart(cart.map(item =>
                item.cartItemId === currentItemForNote.cartItemId ? { ...item, note: currentNote || null } : item
            ));
        }
        setIsNoteDialogOpen(false);
    };

    const cartTotal = useMemo(() => {
        return cart.reduce((sum, item) => sum + item.price * item.quantity, 0);
    }, [cart]);

    // Sheet drag behavior
    useEffect(() => {
        const handleTouchStart = (e: TouchEvent) => {
            if (!cartSheetRef.current || !isCartSheetOpen) return;
            const touchArea = cartSheetRef.current.querySelector('.drag-handle');
            if (touchArea && touchArea.contains(e.target as Node)) {
                startY.current = e.touches[0].clientY;
                isDragging.current = true;
            }
        };

        const handleTouchMove = (e: TouchEvent) => {
            if (!isDragging.current || !cartSheetRef.current) return;
            currentY.current = e.touches[0].clientY;
            const deltaY = currentY.current - startY.current;
            
            if (deltaY > 0) { // Only allow dragging down
                setSheetPosition(deltaY);
            }
        };

        const handleTouchEnd = () => {
            if (!isDragging.current) return;
            isDragging.current = false;
            
            // If dragged more than 150px down, close the sheet
            if (sheetPosition > 150) {
                setIsCartSheetOpen(false);
            }
            
            setSheetPosition(0);
        };

        document.addEventListener('touchstart', handleTouchStart);
        document.addEventListener('touchmove', handleTouchMove);
        document.addEventListener('touchend', handleTouchEnd);

        return () => {
            document.removeEventListener('touchstart', handleTouchStart);
            document.removeEventListener('touchmove', handleTouchMove);
            document.removeEventListener('touchend', handleTouchEnd);
        };
    }, [isCartSheetOpen, sheetPosition]);

    // Effect to disable body scroll when sheet is open
    useEffect(() => {
        if (isCartSheetOpen) {
            document.body.style.overflow = 'hidden';
        } else {
            document.body.style.overflow = ''; // Revert to default
        }

        // Cleanup function to ensure scroll is re-enabled if component unmounts while sheet is open
        return () => {
            document.body.style.overflow = '';
        };
    }, [isCartSheetOpen]);

    // Submission Logic
    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();
        // Ensure data is loaded before submitting
        if (!organization || !area || isLoadingData) {
            toast.error("I dati sono ancora in caricamento o mancanti. Impossibile inviare l'ordine.");
            return;
        }

        setIsSubmitting(true); // Use isSubmitting state

        if (cart.length === 0) {
            toast.error("Il tuo carrello è vuoto.");
            setIsSubmitting(false);
            return;
        }

        if (!customerName.trim() || !customerEmail.trim()) {
            toast.error("Inserisci il tuo nome e la tua email.");
            setIsSubmitting(false);
            return;
        }

        if (!/\S+@\S+\.\S+/.test(customerEmail)) {
            toast.error("Inserisci un indirizzo email valido.");
            setIsSubmitting(false);
            return;
        }

        const preOrderData: PreOrderDto = {
            organizationId: organization.id, // Use state variable
            areaId: area.id, // Use state variable
            customerName: customerName.trim(),
            customerEmail: customerEmail.trim(),
            items: cart.map(item => ({
                menuItemId: item.id,
                quantity: item.quantity,
                note: item.note
            })),
            numberOfGuests: 0,
            isTakeaway: false
        };

        try {
            const response = await apiClient.post<OrderDto>('/public/preorders', preOrderData);
            const result = response.data;

            // Use order ID (string) instead of orderNumber
            toast.success(`Ordine #${result?.id || ''} inviato! Riceverai un'email di conferma.`);
            // Use orderId and qrCodeBase64 as query params
            const successUrlParams = new URLSearchParams({
                orderId: result?.id || '',
                orgName: organization.name,
                areaName: area.name
            });
            const successUrl = `/preorder/success?${successUrlParams.toString()}`;

            // Store QR code in sessionStorage before redirecting
            if (result?.qrCodeBase64) {
                try {
                    sessionStorage.setItem(`preorderQrCode_${result.id}`, result.qrCodeBase64);
                } catch (storageError) {
                    console.error("Failed to save QR code to sessionStorage:", storageError);
                    // Optionally inform the user or log this error
                    toast.warning("Impossibile memorizzare il codice QR per la visualizzazione immediata, controlla la tua email.");
                }
            }

            router.push(successUrl);

            setCart([]);
            setCustomerName('');
            setCustomerEmail('');
            setIsCartSheetOpen(false);
        } catch (error: unknown) {
            console.error("Order submission error:", error);
            let errorMsg = "Errore durante l'invio dell'ordine.";
            if (typeof error === 'object' && error !== null) {
                if ('message' in error) {
                    errorMsg = String((error as { message: string }).message);
                }
                if ('response' in error && typeof (error as { response?: { data?: { title?: string, message?: string } } }).response?.data === 'object' && (error as { response: { data: object } }).response.data !== null) {
                    const responseData = (error as { response: { data: { title?: string, message?: string } } }).response.data;
                    if (responseData.title) {
                        errorMsg = responseData.title;
                    } else if (responseData.message) {
                        errorMsg = responseData.message;
                    }
                }
            }
            toast.error(`Errore: ${errorMsg}`);
        } finally {
            setIsSubmitting(false); // Use isSubmitting state
        }
    };

    const groupedMenuItems = useMemo(() => {
        const groups: { [key: number]: MenuItemDto[] } = {};
        menuItems.forEach(item => {
            if (!groups[item.menuCategoryId]) {
                groups[item.menuCategoryId] = [];
            }
            groups[item.menuCategoryId].push(item);
        });
        return groups;
    }, [menuItems]);

    // --- Render Logic ---

    if (isLoadingData) {
        return (
            <div className="flex justify-center items-center h-screen">
                <Loader2 className="h-8 w-8 animate-spin mr-2" />
                <span>Caricamento Pre-Ordine...</span>
            </div>
        );
    }

    if (errorData) {
        return (
            <div className="flex flex-col justify-center items-center h-screen p-4 text-center">
                <AlertCircle className="h-12 w-12 text-red-500 mb-4" />
                <h2 className="text-xl font-semibold text-red-600 mb-2">Errore Caricamento Pre-Ordine</h2>
                <p className="text-muted-foreground mb-4">{errorData}</p>
                <Button onClick={() => window.location.reload()}>Riprova</Button>
            </div>
        );
    }

    if (!organization || !area) {
         // This case should ideally be covered by errorData, but as a fallback
         return <div className="flex justify-center items-center h-screen">Errore: Dati Organizzazione o Area mancanti.</div>;
    }

    // Main component render if data loaded successfully
    return (
        // Use h-full and overflow-hidden on the root to contain height
        <div className="flex flex-col h-full bg-gray-50 overflow-hidden">
            {/* Header */}
            <header className="bg-white shadow-sm p-4 sticky top-0 z-10 flex-shrink-0">
                <h1 className="text-xl font-bold text-center">{organization.name}</h1>
                <p className="text-sm text-center text-gray-500">{area.name}</p>
            </header>

            {/* Main Content - Make this scrollable */}
            <div className="flex-1 overflow-y-auto pb-24"> {/* Added padding-bottom */}
                {activeCategory === null ? (
                    // Main Menu View
                    <div className="p-4">
                        {/* Category Navigation */}
                        <div className="flex overflow-x-auto pb-2 mb-4 gap-2">
                            {menuCategories.map(category => (
                                <button
                                    key={category.id}
                                    className={`px-4 py-2 rounded-full whitespace-nowrap ${
                                        activeCategory === category.id.toString() 
                                            ? 'bg-primary text-white' 
                                            : 'bg-white border'
                                    }`}
                                    onClick={() => setActiveCategory(category.id.toString())}
                                >
                                    {category.name}
                                </button>
                            ))}
                        </div>

                        {/* Featured Items or All Items */}
                        <div className="space-y-6">
                            {menuCategories.map(category => (
                                <div key={category.id} className="space-y-2">
                                    <h2 className="text-lg font-semibold flex justify-between items-center">
                                        {category.name}
                                        <button 
                                            className="text-sm text-primary flex items-center"
                                            onClick={() => setActiveCategory(category.id.toString())}
                                        >
                                            View all <ChevronRight size={16} />
                                        </button>
                                    </h2>
                                    <div className="grid grid-cols-1 gap-3">
                                        {(groupedMenuItems[category.id] || [])
                                            .slice(0, 3) // Show just first 3 items per category in main view
                                            .map(item => (
                                                <div 
                                                    key={item.id} 
                                                    className="bg-white rounded-lg shadow-sm p-4 flex justify-between items-center"
                                                    onClick={() => handleAddToCart(item)}
                                                >
                                                    <div className="flex-1">
                                                        <div className="flex justify-between items-start">
                                                            <h3 className="font-medium">{item.name}</h3>
                                                            <span className="font-semibold ml-2">€{item.price.toFixed(2)}</span>
                                                        </div>
                                                        {item.description && (
                                                            <p className="text-sm text-gray-500 mt-1">{item.description}</p>
                                                        )}
                                                        {item.isNoteRequired && (
                                                            <p className="text-xs text-orange-500 mt-1">Note required</p>
                                                        )}
                                                    </div>
                                                    <button 
                                                        className="ml-3 p-2 rounded-full bg-primary text-white"
                                                        onClick={(e) => {
                                                            e.stopPropagation();
                                                            handleAddToCart(item);
                                                        }}
                                                    >
                                                        <Plus size={16} />
                                                    </button>
                                                </div>
                                            ))}
                                        {(groupedMenuItems[category.id] || []).length > 3 && (
                                            <button 
                                                className="text-center py-2 text-primary font-medium border border-primary rounded-lg"
                                                onClick={() => setActiveCategory(category.id.toString())}
                                            >
                                                View all {(groupedMenuItems[category.id] || []).length} items
                                            </button>
                                        )}
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>
                ) : (
                    // Category Detail View
                    <div className="p-4">
                        <button 
                            className="flex items-center mb-4 text-gray-500"
                            onClick={() => setActiveCategory(null)}
                        >
                            <ChevronRight className="rotate-180 mr-1" size={16} />
                            Back to menu
                        </button>
                        
                        <h2 className="text-xl font-bold mb-4">
                            {menuCategories.find(c => c.id.toString() === activeCategory)?.name}
                        </h2>
                        
                        <div className="space-y-3">
                            {(groupedMenuItems[parseInt(activeCategory)] || []).map(item => (
                                <div 
                                    key={item.id} 
                                    className="bg-white rounded-lg shadow-sm p-4 flex justify-between items-center"
                                    onClick={() => handleAddToCart(item)}
                                >
                                    <div className="flex-1">
                                        <div className="flex justify-between items-start">
                                            <h3 className="font-medium">{item.name}</h3>
                                            <span className="font-semibold ml-2">€{item.price.toFixed(2)}</span>
                                        </div>
                                        {item.description && (
                                            <p className="text-sm text-gray-500 mt-1">{item.description}</p>
                                        )}
                                        {item.isNoteRequired && (
                                            <p className="text-xs text-orange-500 mt-1">Note required</p>
                                        )}
                                    </div>
                                    <button 
                                        className="ml-3 p-2 rounded-full bg-primary text-white"
                                        onClick={(e) => {
                                            e.stopPropagation();
                                            handleAddToCart(item);
                                        }}
                                    >
                                        <Plus size={16} />
                                    </button>
                                </div>
                            ))}
                        </div>
                    </div>
                )}
            </div>

            {/* Floating Cart Button */}
            {cart.length > 0 && (
                <div className="fixed bottom-6 inset-x-0 flex justify-center z-20">
                    <button 
                        className="bg-primary text-white px-6 py-3 rounded-full shadow-lg flex items-center"
                        onClick={() => setIsCartSheetOpen(true)}
                    >
                        <ShoppingCart size={20} className="mr-2" />
                        <span className="font-medium">View Order</span>
                        <Badge className="ml-2 bg-white text-primary">
                            {cart.reduce((sum, item) => sum + item.quantity, 0)}
                        </Badge>
                        <span className="ml-3 font-medium">€{cartTotal.toFixed(2)}</span>
                    </button>
                </div>
            )}

            {/* Cart Sheet */}
            {isCartSheetOpen && (
                <>
                    {/* Overlay - Changed background */}
                    <div
                        className="fixed inset-0 bg-black/60 z-30" // Use black with opacity
                        onClick={() => setIsCartSheetOpen(false)}
                    />

                    {/* Sheet - Apply flex column layout */}
                    <div
                        ref={cartSheetRef}
                        className="fixed bottom-0 inset-x-0 bg-white rounded-t-xl z-40 shadow-xl transition-transform flex flex-col" // Added flex flex-col
                        style={{
                            transform: sheetPosition > 0 ? `translateY(${sheetPosition}px)` : 'translateY(0)',
                            maxHeight: '85vh', // Keep max height constraint
                        }}
                    >
                        {/* Drag Handle */}
                        <div className="drag-handle flex justify-center p-2 cursor-grab active:cursor-grabbing">
                            <div className="w-12 h-1 bg-gray-300 rounded-full" />
                        </div>
                        
                        {/* Header (No change needed here) */}
                        <div className="px-4 py-2 border-b flex justify-between items-center flex-shrink-0"> {/* Added flex-shrink-0 */}
                            <h2 className="text-lg font-semibold">Your Order</h2>
                            <button
                                className="p-1 rounded-full hover:bg-gray-100"
                                onClick={() => setIsCartSheetOpen(false)}
                            >
                                <X size={20} />
                            </button>
                        </div>

                        {/* Content Area - Make this scrollable, ensure it takes remaining space */}
                        <div className="flex-1 overflow-y-auto"> {/* Keep flex-1 and overflow-y-auto */}
                            {/* Cart Items */}
                            <div className="p-4">
                                {cart.length === 0 ? (
                                    <div className="text-center py-8">
                                        <ShoppingCart size={48} className="mx-auto text-gray-300 mb-4" />
                                        <p className="text-gray-500">Your cart is empty</p>
                                        <button 
                                            className="mt-4 text-primary font-medium"
                                            onClick={() => {
                                                setIsCartSheetOpen(false);
                                                setActiveCategory(null);
                                            }}
                                        >
                                            Browse menu
                                        </button>
                                    </div>
                                ) : (
                                    <div className="space-y-4">
                                        {cart.map(item => (
                                            <div key={item.cartItemId} className="border-b pb-4 last:border-b-0">
                                                <div className="flex justify-between">
                                                    <div className="flex-1">
                                                        <div className="flex items-center">
                                                            <h3 className="font-medium">{item.name}</h3>
                                                            {item.isNoteRequired && !item.note && (
                                                                <span className="ml-2 text-xs text-red-500">Note required</span>
                                                            )}
                                                        </div>
                                                        <p className="text-sm text-gray-500">€{item.price.toFixed(2)}</p>
                                                    </div>
                                                    <div className="flex items-center gap-2">
                                                        <button 
                                                            className="p-1 rounded-full bg-gray-100 text-gray-600"
                                                            onClick={() => handleDecreaseQuantity(item.cartItemId)}
                                                        >
                                                            <Minus size={16} />
                                                        </button>
                                                        <span className="w-6 text-center">{item.quantity}</span>
                                                        <button 
                                                            className="p-1 rounded-full bg-gray-100 text-gray-600"
                                                            onClick={() => handleIncreaseQuantity(item.cartItemId)}
                                                        >
                                                            <Plus size={16} />
                                                        </button>
                                                    </div>
                                                </div>
                                                
                                                {item.note && (
                                                    <p className="text-sm mt-2 bg-blue-50 p-2 rounded">
                                                        <span className="font-medium">Note:</span> {item.note}
                                                    </p>
                                                )}
                                                
                                                <div className="flex justify-end gap-2 mt-2">
                                                    <button 
                                                        className="text-sm flex items-center text-primary"
                                                        onClick={() => handleOpenNoteDialog(item)}
                                                    >
                                                        <StickyNote size={14} className="mr-1" />
                                                        {item.note ? 'Edit note' : 'Add note'}
                                                    </button>
                                                    <button 
                                                        className="text-sm flex items-center text-red-500"
                                                        onClick={() => handleRemoveFromCart(item.cartItemId)}
                                                    >
                                                        <Trash2 size={14} className="mr-1" />
                                                        Remove
                                                    </button>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>

                        {/* Fixed Footer Area within Sheet */}
                        {cart.length > 0 && (
                            <div className="flex-shrink-0 p-4 border-t bg-gray-50 shadow-inner">
                                {/* Total */}
                                <div className="flex justify-between font-semibold text-lg mb-4">
                                    <span>Total:</span>
                                    <span>€{cartTotal.toFixed(2)}</span>
                                </div>
                                {/* Checkout Form */}
                                <h2 className="text-lg font-semibold mb-4">Checkout</h2>
                                <form onSubmit={handleSubmit} className="space-y-4">
                                    <div>
                                            <Label htmlFor="customerName">Your Name</Label>
                                            <Input
                                                id="customerName"
                                                type="text"
                                                value={customerName}
                                                onChange={(e) => setCustomerName(e.target.value)}
                                                required
                                                disabled={isSubmitting} // Use isSubmitting
                                                className="mt-1"
                                            />
                                        </div>
                                        <div>
                                            <Label htmlFor="customerEmail">Email Address</Label>
                                            <Input
                                                id="customerEmail"
                                                type="email"
                                                value={customerEmail}
                                                onChange={(e) => setCustomerEmail(e.target.value)}
                                                required
                                                disabled={isSubmitting} // Use isSubmitting
                                                className="mt-1"
                                            />
                                        </div>
                                        <Button
                                            type="submit"
                                            className="w-full py-6 text-lg font-medium"
                                            disabled={isSubmitting || cart.some(item => item.isNoteRequired && !item.note)} // Use isSubmitting
                                        >
                                            {isSubmitting ? ( // Use isSubmitting
                                                <span className="flex items-center justify-center"> {/* Added justify-center */}
                                                    <Loader2 className="animate-spin -ml-1 mr-3 h-5 w-5 text-white" /> {/* Use Loader2 */}
                                                    Processing...
                                                </span>
                                            ) : (
                                                'Place Order'
                                            )}
                                        </Button>
                                    </form>
                            </div>
                        )}
                    </div>
                </>
            )}

            {/* Note Dialog */}
            <AlertDialog open={isNoteDialogOpen} onOpenChange={setIsNoteDialogOpen}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Note for {currentItemForNote?.name}</AlertDialogTitle>
                        <AlertDialogDescription>
                            {currentItemForNote?.isNoteRequired ?
                                'This item requires a note (e.g., cooking preference, allergies).' :
                                'Add an optional note for preparation.'}
                            {currentItemForNote?.noteSuggestion && (
                                <div className="mt-2 text-sm italic bg-yellow-50 p-2 rounded">
                                    Suggestion: {currentItemForNote.noteSuggestion}
                                </div>
                            )}
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <Textarea
                        value={currentNote}
                        onChange={(e) => setCurrentNote(e.target.value)}
                        placeholder="Type your note here..."
                        rows={3}
                        className="mt-2"
                    />
                    <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <AlertDialogAction
                            onClick={handleSaveNote}
                            disabled={currentItemForNote?.isNoteRequired && !currentNote.trim()}
                        >
                            Save Note
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </div>
    );
}
