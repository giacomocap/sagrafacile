import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartConfig, ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from "recharts";
import { analyticsService } from '@/services/analyticsService';
import { TopMenuItemDto } from '@/types';
import { LoadingChart } from '../shared/LoadingChart';
import { EmptyChart } from '../shared/EmptyChart';

interface TopMenuItemsChartProps {
    organizationId: number;
    days?: number;
    limit?: number;
    refreshInterval?: number;
}

const chartConfig = {
    quantity: {
        label: "Quantità",
        color: "hsl(var(--chart-1))",
    },
    revenue: {
        label: "Ricavi",
        color: "hsl(var(--chart-2))",
    },
} satisfies ChartConfig;

export function TopMenuItemsChart({ 
    organizationId, 
    days = 7, 
    limit = 5, 
    refreshInterval = 300000 
}: TopMenuItemsChartProps) {
    const [data, setData] = useState<TopMenuItemDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const fetchData = async () => {
        try {
            setError(null);
            const result = await analyticsService.getTopMenuItems(organizationId, days, limit);
            setData(result);
        } catch (err) {
            console.error('Error fetching top menu items:', err);
            setError('Errore nel caricamento dei dati');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
        
        const interval = setInterval(fetchData, refreshInterval);
        return () => clearInterval(interval);
    }, [organizationId, days, limit, refreshInterval]);

    if (loading) {
        return <LoadingChart height={400} />;
    }

    if (error) {
        return (
            <EmptyChart
                title="Articoli Più Venduti"
                message={error}
                isError={true}
                height={400}
            />
        );
    }

    if (data.length === 0) {
        return (
            <EmptyChart
                title="Articoli Più Venduti"
                message="Nessun articolo venduto nel periodo selezionato"
                height={400}
            />
        );
    }

    // Prepare chart data
    const chartData = data.map((item, index) => ({
        itemName: item.itemName.length > 20 ? 
            item.itemName.substring(0, 20) + '...' : 
            item.itemName,
        fullItemName: item.itemName,
        categoryName: item.categoryName,
        quantity: item.quantity,
        revenue: item.revenue,
        rank: index + 1,
    }));

    const formatCurrency = (value: number) => {
        return new Intl.NumberFormat('it-IT', {
            style: 'currency',
            currency: 'EUR',
            minimumFractionDigits: 0,
            maximumFractionDigits: 0,
        }).format(value);
    };

    const totalQuantity = data.reduce((sum, item) => sum + item.quantity, 0);
    const totalRevenue = data.reduce((sum, item) => sum + item.revenue, 0);

    return (
        <Card>
            <CardHeader>
                <CardTitle>Articoli Più Venduti</CardTitle>
                <CardDescription>
                    Top {limit} articoli degli ultimi {days} giorni
                </CardDescription>
            </CardHeader>
            <CardContent>
                <div className="mb-4 grid grid-cols-2 gap-4 text-sm">
                    <div>
                        <div className="font-medium">Quantità Totale</div>
                        <div className="text-2xl font-bold text-chart-1">
                            {totalQuantity}
                        </div>
                    </div>
                    <div>
                        <div className="font-medium">Ricavi Totali</div>
                        <div className="text-2xl font-bold text-chart-2">
                            {formatCurrency(totalRevenue)}
                        </div>
                    </div>
                </div>

                <ChartContainer config={chartConfig}>
                    <BarChart
                        accessibilityLayer
                        data={chartData}
                        layout="horizontal"
                        margin={{
                            left: 80,
                            right: 12,
                            top: 12,
                            bottom: 12,
                        }}
                    >
                        <CartesianGrid horizontal={false} />
                        <YAxis
                            dataKey="itemName"
                            type="category"
                            tickLine={false}
                            axisLine={false}
                            tickMargin={8}
                            width={75}
                        />
                        <XAxis
                            type="number"
                            tickLine={false}
                            axisLine={false}
                            tickMargin={8}
                        />
                        <ChartTooltip
                            cursor={false}
                            content={
                                <ChartTooltipContent
                                    labelFormatter={(value, payload) => {
                                        if (payload && payload[0]) {
                                            const item = payload[0].payload;
                                            return `${item.fullItemName} (${item.categoryName})`;
                                        }
                                        return value;
                                    }}
                                    formatter={(value, name) => {
                                        if (name === 'quantity') {
                                            return [`${value} pz`, 'Quantità'];
                                        }
                                        if (name === 'revenue') {
                                            return [formatCurrency(Number(value)), 'Ricavi'];
                                        }
                                        return [value, name];
                                    }}
                                />
                            }
                        />
                        <Bar
                            dataKey="quantity"
                            fill="hsl(var(--chart-1))"
                            radius={[0, 4, 4, 0]}
                        />
                    </BarChart>
                </ChartContainer>

                {/* Detailed list */}
                <div className="mt-4 space-y-2">
                    <h4 className="text-sm font-medium">Dettaglio Vendite</h4>
                    {data.map((item, index) => (
                        <div key={item.itemName} className="flex items-center justify-between text-sm">
                            <div className="flex items-center gap-2">
                                <span className="flex h-6 w-6 items-center justify-center rounded-full bg-muted text-xs font-medium">
                                    {index + 1}
                                </span>
                                <div>
                                    <div className="font-medium">{item.itemName}</div>
                                    <div className="text-xs text-muted-foreground">{item.categoryName}</div>
                                </div>
                            </div>
                            <div className="text-right">
                                <div className="font-medium">{item.quantity} pz</div>
                                <div className="text-xs text-muted-foreground">
                                    {formatCurrency(item.revenue)}
                                </div>
                            </div>
                        </div>
                    ))}
                </div>
            </CardContent>
        </Card>
    );
}
