import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"
import { OrderStatus } from "@/types"; // Added import for OrderStatus

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export const getOrderStatusBadgeVariant = (status: OrderStatus): "default" | "secondary" | "destructive" | "outline" => {
    switch (status) {
        case OrderStatus.Paid: return "secondary";
        case OrderStatus.Preparing: return "default"; // Often primary color
        case OrderStatus.ReadyForPickup: return "default"; // Often primary, consider a specific "success" or "go" color if available
        case OrderStatus.Completed: return "outline"; // Or a muted success
        case OrderStatus.Cancelled: return "destructive";
        case OrderStatus.PreOrder: return "secondary"; // Similar to Paid or a distinct "info" color
        default: return "outline";
    }
};

// Add other shared utility functions here as needed.
