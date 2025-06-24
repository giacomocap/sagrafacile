'use client';

import React, { useEffect } from 'react';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ChevronsUpDown, ArrowUp, ArrowDown } from 'lucide-react';
import { PaginatedResult } from '@/types';

// A unique key to store table settings in localStorage
const LOCAL_STORAGE_KEY = 'tableSettings';

interface ColumnDefinition {
    key: string;
    label: string;
    sortable?: boolean;
}

interface BaseQueryParameters {
    page?: number;
    pageSize?: number;
    sortBy?: string;
    sortAscending?: boolean;
}

interface PaginatedTableProps<T> {
    storageKey: string; // Unique key for this table instance's settings
    columns: ColumnDefinition[];
    paginatedData: PaginatedResult<T> | null;
    isLoading: boolean;
    error: string | null;
    queryParams: BaseQueryParameters;
    onQueryChange: (newParams: Partial<BaseQueryParameters>) => void;
    renderCell: (item: T, columnKey: string) => React.ReactNode;
    renderActions?: (item: T) => React.ReactNode;
    itemKey: (item: T) => string;
}

export default function PaginatedTable<T>({
    storageKey,
    columns,
    paginatedData,
    isLoading,
    error,
    queryParams,
    onQueryChange,
    renderCell,
    renderActions,
    itemKey,
}: PaginatedTableProps<T>) {

    // Load page size from localStorage on initial render
    useEffect(() => {
        try {
            const savedSettings = localStorage.getItem(LOCAL_STORAGE_KEY);
            if (savedSettings) {
                const settings = JSON.parse(savedSettings);
                const pageSize = settings[storageKey]?.pageSize;
                if (pageSize && pageSize !== queryParams.pageSize) {
                    onQueryChange({ pageSize: Number(pageSize), page: 1 });
                }
            }
        } catch (e) {
            console.warn("Could not read table settings from localStorage", e);
        }
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [storageKey]); // Run only once on mount

    const handleSort = (column: string) => {
        onQueryChange({
            sortBy: column,
            sortAscending: queryParams.sortBy === column ? !queryParams.sortAscending : true,
            page: 1, // Reset to first page on sort
        });
    };

    const handlePageSizeChange = (value: string) => {
        const newPageSize = Number(value);
        onQueryChange({ pageSize: newPageSize, page: 1 });

        // Save the new page size to localStorage
        try {
            const savedSettings = localStorage.getItem(LOCAL_STORAGE_KEY);
            const settings = savedSettings ? JSON.parse(savedSettings) : {};
            settings[storageKey] = { ...settings[storageKey], pageSize: newPageSize };
            localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(settings));
        } catch (e) {
            console.warn("Could not save table settings to localStorage", e);
        }
    };

    const renderSortIcon = (column: string) => {
        if (queryParams.sortBy !== column) {
            return <ChevronsUpDown className="ml-2 h-4 w-4" />;
        }
        return queryParams.sortAscending ? <ArrowUp className="ml-2 h-4 w-4" /> : <ArrowDown className="ml-2 h-4 w-4" />;
    };

    if (isLoading && !paginatedData) {
        return <p className="text-center py-4">Caricamento...</p>;
    }

    if (error) {
        return <p className="text-red-500 text-center py-4">{error}</p>;
    }

    if (!paginatedData || paginatedData.items.length === 0) {
        return <p className="text-center text-muted-foreground py-4">Nessun dato trovato.</p>;
    }

    return (
        <>
            <Table>
                <TableHeader>
                    <TableRow>
                        {columns.map(col => (
                            <TableHead
                                key={col.key}
                                className={col.sortable !== false ? "cursor-pointer" : ""}
                                onClick={() => col.sortable !== false && handleSort(col.key)}
                            >
                                <div className="flex items-center">
                                    {col.label}
                                    {col.sortable !== false && renderSortIcon(col.key)}
                                </div>
                            </TableHead>
                        ))}
                        {renderActions && <TableHead className="text-right">Azioni</TableHead>}
                    </TableRow>
                </TableHeader>
                <TableBody>
                    {paginatedData.items.map((item) => (
                        <TableRow key={itemKey(item)}>
                            {columns.map(col => (
                                <TableCell key={col.key}>{renderCell(item, col.key)}</TableCell>
                            ))}
                            {renderActions && (
                                <TableCell className="text-right">
                                    {renderActions(item)}
                                </TableCell>
                            )}
                        </TableRow>
                    ))}
                </TableBody>
            </Table>
            <div className="flex items-center justify-between mt-4">
                <div className="flex items-center space-x-2">
                    <p className="text-sm text-muted-foreground">
                        Righe per pagina:
                    </p>
                    <Select value={String(queryParams.pageSize || 10)} onValueChange={handlePageSizeChange}>
                        <SelectTrigger className="w-[70px]">
                            <SelectValue placeholder={queryParams.pageSize || 10} />
                        </SelectTrigger>
                        <SelectContent>
                            {[10, 20, 50, 100].map(size => (
                                <SelectItem key={size} value={String(size)}>{size}</SelectItem>
                            ))}
                        </SelectContent>
                    </Select>
                </div>
                <p className="text-sm text-muted-foreground">
                    Pagina {paginatedData.page} di {paginatedData.totalPages}. Totale: {paginatedData.totalCount} risultati.
                </p>
                <div className="space-x-2">
                    <Button
                        variant="outline"
                        size="sm"
                        onClick={() => onQueryChange({ page: (queryParams.page || 1) - 1 })}
                        disabled={paginatedData.page <= 1}
                    >
                        Precedente
                    </Button>
                    <Button
                        variant="outline"
                        size="sm"
                        onClick={() => onQueryChange({ page: (queryParams.page || 1) + 1 })}
                        disabled={paginatedData.page >= paginatedData.totalPages}
                    >
                        Successivo
                    </Button>
                </div>
            </div>
        </>
    );
}
