"use client";

import * as React from "react";
import { Sheet, SheetContent } from "../ui/sheet";

interface ResponsiveDialogProps {
    isOpen: boolean;
    onOpenChange: (isOpen: boolean) => void;
    children: React.ReactNode;
    className?: string;
}

export function ResponsiveDialog({
    isOpen,
    onOpenChange,
    children,
    className,
}: ResponsiveDialogProps) {
    return (
        <Sheet open={isOpen} onOpenChange={onOpenChange}>
            <SheetContent className={`flex flex-col h-full ${className || ''}`}>{children}</SheetContent>
        </Sheet>
    );
}
