import React, { useMemo } from 'react';
import { OrderDto, OrderStatus } from '@/types'; // Import OrderStatus
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table'; // Added TableFooter
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button'; // Import Button
import { Printer } from 'lucide-react'; // Import Printer icon

interface OrderTableProps {
    orders: OrderDto[];
    onReprintClick?: (order: OrderDto) => void; // Optional: for admin reprint functionality
}

const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' }).format(amount);
};

const formatDate = (dateString: string) => {
    try {
        return new Intl.DateTimeFormat('it-IT', {
            dateStyle: 'short', // e.g., 08/04/25
            timeStyle: 'short', // e.g., 12:35
        }).format(new Date(dateString));
    } catch {
        return dateString; // Fallback
    }
};

const OrderTable: React.FC<OrderTableProps> = ({ orders, onReprintClick }) => {
    // Calculate totals using useMemo for efficiency
    // Moved before the early return to comply with rules of hooks
    const totals = useMemo(() => {
        if (!orders) return { count: 0, sum: 0 }; // Handle null/undefined orders
        const count = orders.length;
        const sum = orders.reduce((acc, order) => acc + order.totalAmount, 0);
        return { count, sum };
    }, [orders]); // Recalculate only when orders array changes

    if (!orders || orders.length === 0) {
        return <p className="text-center text-muted-foreground py-10">Nessun ordine da visualizzare.</p>;
    }

    return (
        <Table>
            <TableHeader>
                <TableRow>
                    <TableHead>ID Ordine</TableHead> {/* Changed Header */}
                    <TableHead>Data/Ora</TableHead>
                    <TableHead>Area</TableHead>
                    <TableHead>Totale</TableHead>
                    <TableHead>Metodo Pagamento</TableHead>
                    <TableHead>Stato</TableHead>
                    <TableHead>Tavolo</TableHead> {/* Add Table Header */}
                    {onReprintClick && <TableHead className="text-right">Azioni</TableHead>}
                </TableRow>
            </TableHeader>
            <TableBody>{/* Ensure no leading whitespace */}
                {orders.map((order) => (
                    <TableRow key={order.id}>
                        <TableCell className="font-medium">{order.displayOrderNumber ?? order.id}</TableCell>{/* Use ID */}
                        <TableCell>{formatDate(order.orderDateTime)}</TableCell>
                        <TableCell>{order.areaName || 'N/A'}</TableCell>
                        <TableCell className="text-right">{formatCurrency(order.totalAmount)}</TableCell>
                        <TableCell>
                            <Badge variant={order.paymentMethod === 'Contanti' ? 'secondary' : 'default'}>
                                {order.paymentMethod || 'N/D'} {/* Handle null paymentMethod */}
                            </Badge>
                        </TableCell>
                        <TableCell>{OrderStatus[order.status] ?? 'Sconosciuto'}</TableCell>
                        <TableCell>{order.tableNumber || 'N/A'}</TableCell> {/* Display Table Number */}
                        {onReprintClick && (
                            <TableCell className="text-right">
                                <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() => onReprintClick(order)}
                                >
                                    <Printer className="mr-2 h-4 w-4" /> Ristampa
                                </Button>
                            </TableCell>
                        )}
                    </TableRow>
                ))}{/* Ensure no trailing whitespace */}
            </TableBody>
            <TableFooter>{/* Ensure no leading whitespace */}
                <TableRow>
                    {/* Span the first 2 columns */}
                    <TableCell colSpan={2} className="font-semibold">Totali:</TableCell>
                    {/* Display count in the 3rd column (under Area) */}
                    <TableCell className="font-semibold">{totals.count} ordini</TableCell>
                    {/* Display sum aligned to the right in the 4th column (under Totale) */}
                    <TableCell className="text-right font-semibold">{formatCurrency(totals.sum)}</TableCell>
                    {/* Empty cell for Payment Method */}
                    <TableCell></TableCell>
                    {/* Empty cell for Status */}
                    <TableCell></TableCell>
                    {/* Empty cell for Table */}
                    <TableCell></TableCell>
                    {onReprintClick && <TableCell></TableCell>} {/* Empty cell for Actions if column exists */}
                </TableRow>{/* Ensure no trailing whitespace */}
            </TableFooter>
        </Table>
    );
};

export default OrderTable;
