import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartConfig, ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from "recharts";
import { analyticsService } from '@/services/analyticsService';
import { SalesTrendDataDto } from '@/types';
import { LoadingChart } from '../shared/LoadingChart';
import { EmptyChart } from '../shared/EmptyChart';

interface SalesTrendChartProps {
    organizationId: string;
    days?: number;
    refreshInterval?: number;
}

const chartConfig = {
    sales: {
        label: "Vendite",
        color: "hsl(var(--chart-1))",
    },
    orderCount: {
        label: "Ordini",
        color: "hsl(var(--chart-2))",
    },
} satisfies ChartConfig;

export function SalesTrendChart({ organizationId, days = 7, refreshInterval = 300000 }: SalesTrendChartProps) {
    const [data, setData] = useState<SalesTrendDataDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const fetchData = async () => {
        try {
            setError(null);
            const result = await analyticsService.getSalesTrend(organizationId, days);
            setData(result);
        } catch (err) {
            console.error('Error fetching sales trend:', err);
            setError('Errore nel caricamento dei dati');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
        
        const interval = setInterval(fetchData, refreshInterval);
        return () => clearInterval(interval);
    }, [organizationId, days, refreshInterval]);

    if (loading) {
        return <LoadingChart height={350} />;
    }

    if (error) {
        return (
            <EmptyChart
                title="Trend Vendite"
                message={error}
                isError={true}
                height={350}
            />
        );
    }

    if (data.length === 0) {
        return (
            <EmptyChart
                title="Trend Vendite"
                message="Nessun dato disponibile per il periodo selezionato"
                height={350}
            />
        );
    }

    // Format data for the chart
    const chartData = data.map(item => ({
        date: new Date(item.date).toLocaleDateString('it-IT', { 
            month: 'short', 
            day: 'numeric' 
        }),
        sales: item.sales,
        orderCount: item.orderCount,
        fullDate: item.date
    }));

    const formatCurrency = (value: number) => {
        return new Intl.NumberFormat('it-IT', {
            style: 'currency',
            currency: 'EUR',
            minimumFractionDigits: 0,
            maximumFractionDigits: 0,
        }).format(value);
    };

    const totalSales = data.reduce((sum, item) => sum + item.sales, 0);
    const totalOrders = data.reduce((sum, item) => sum + item.orderCount, 0);

    return (
        <Card>
            <CardHeader>
                <CardTitle>Trend Vendite</CardTitle>
                <CardDescription>
                    Vendite e ordini degli ultimi {days} giorni
                </CardDescription>
            </CardHeader>
            <CardContent>
                <div className="mb-4 grid grid-cols-2 gap-4 text-sm">
                    <div>
                        <div className="font-medium">Vendite Totali</div>
                        <div className="text-2xl font-bold text-chart-1">
                            {formatCurrency(totalSales)}
                        </div>
                    </div>
                    <div>
                        <div className="font-medium">Ordini Totali</div>
                        <div className="text-2xl font-bold text-chart-2">
                            {totalOrders}
                        </div>
                    </div>
                </div>
                <ChartContainer config={chartConfig}>
                    <AreaChart
                        accessibilityLayer
                        data={chartData}
                        margin={{
                            left: 12,
                            right: 12,
                            top: 12,
                            bottom: 12,
                        }}
                    >
                        <CartesianGrid vertical={false} />
                        <XAxis
                            dataKey="date"
                            tickLine={false}
                            axisLine={false}
                            tickMargin={8}
                        />
                        <YAxis
                            yAxisId="sales"
                            orientation="left"
                            tickLine={false}
                            axisLine={false}
                            tickMargin={8}
                            tickFormatter={formatCurrency}
                        />
                        <YAxis
                            yAxisId="orders"
                            orientation="right"
                            tickLine={false}
                            axisLine={false}
                            tickMargin={8}
                        />
                        <ChartTooltip
                            cursor={false}
                            content={<ChartTooltipContent 
                                labelFormatter={(value, payload) => {
                                    if (payload && payload[0]) {
                                        const fullDate = payload[0].payload?.fullDate;
                                        if (fullDate) {
                                            return new Date(fullDate).toLocaleDateString('it-IT', {
                                                weekday: 'long',
                                                year: 'numeric',
                                                month: 'long',
                                                day: 'numeric'
                                            });
                                        }
                                    }
                                    return value;
                                }}
                                formatter={(value, name) => {
                                    if (name === 'sales') {
                                        return [formatCurrency(Number(value)), 'Vendite'];
                                    }
                                    return [value, 'Ordini'];
                                }}
                            />}
                        />
                        <Area
                            yAxisId="sales"
                            dataKey="sales"
                            type="natural"
                            fill="var(--color-sales)"
                            fillOpacity={0.4}
                            stroke="var(--color-sales)"
                            stackId="a"
                        />
                    </AreaChart>
                </ChartContainer>
            </CardContent>
        </Card>
    );
}
