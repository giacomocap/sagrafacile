import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartConfig, ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { Pie, PieChart } from "recharts";
import { analyticsService } from '@/services/analyticsService';
import { OrderStatusDistributionDto } from '@/types';
import { LoadingChart } from '../shared/LoadingChart';
import { EmptyChart } from '../shared/EmptyChart';

interface OrderStatusChartProps {
    organizationId: string;
    dayId?: number;
    refreshInterval?: number;
}

const statusColors: Record<string, string> = {
    'PreOrder': '#8884d8',
    'Pending': '#82ca9d',
    'Paid': '#ffc658',
    'Preparing': '#ff7300',
    'ReadyForPickup': '#00ff00',
    'Completed': '#0088fe',
    'Cancelled': '#ff0000',
};

const statusLabels: Record<string, string> = {
    'PreOrder': 'Pre-Ordini',
    'Pending': 'In Attesa',
    'Paid': 'Pagati',
    'Preparing': 'In Preparazione',
    'ReadyForPickup': 'Pronti',
    'Completed': 'Completati',
    'Cancelled': 'Annullati',
};

export function OrderStatusChart({ organizationId, dayId, refreshInterval = 300000 }: OrderStatusChartProps) {
    const [data, setData] = useState<OrderStatusDistributionDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const fetchData = async () => {
        try {
            setError(null);
            const result = await analyticsService.getOrderStatusDistribution(organizationId, dayId);
            setData(result);
        } catch (err) {
            console.error('Error fetching order status distribution:', err);
            setError('Errore nel caricamento dei dati');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
        
        const interval = setInterval(fetchData, refreshInterval);
        return () => clearInterval(interval);
    }, [organizationId, dayId, refreshInterval]);

    if (loading) {
        return <LoadingChart height={400} />;
    }

    if (error) {
        return (
            <EmptyChart
                title="Distribuzione Ordini per Stato"
                message={error}
                isError={true}
                height={400}
            />
        );
    }

    if (data.length === 0) {
        return (
            <EmptyChart
                title="Distribuzione Ordini per Stato"
                message="Nessun ordine trovato per il periodo selezionato"
                height={400}
            />
        );
    }

    // Prepare chart data
    const chartData = data.map(item => ({
        status: item.status,
        count: item.count,
        percentage: item.percentage,
        label: statusLabels[item.status] || item.status,
        fill: statusColors[item.status] || 'hsl(var(--muted))',
    }));

    const chartConfig = data.reduce((config, item) => {
        config[item.status] = {
            label: statusLabels[item.status] || item.status,
            color: statusColors[item.status] || 'hsl(var(--muted))',
        };
        return config;
    }, {} as ChartConfig);

    const totalOrders = data.reduce((sum, item) => sum + item.count, 0);

    return (
        <Card>
            <CardHeader>
                <CardTitle>Distribuzione Ordini per Stato</CardTitle>
                <CardDescription>
                    Stato degli ordini nella giornata corrente
                </CardDescription>
            </CardHeader>
            <CardContent>
                <div className="mb-4">
                    <div className="text-center">
                        <div className="text-2xl font-bold">{totalOrders}</div>
                        <div className="text-sm text-muted-foreground">Ordini Totali</div>
                    </div>
                </div>
                
                <ChartContainer
                    config={chartConfig}
                    className="mx-auto aspect-square max-h-[300px]"
                >
                    <PieChart>
                        <ChartTooltip
                            cursor={false}
                            content={
                                <ChartTooltipContent 
                                    hideLabel
                                    formatter={(value, name, props) => [
                                        `${value} ordini (${props.payload?.percentage?.toFixed(1)}%)`,
                                        props.payload?.label || name
                                    ]}
                                />
                            }
                        />
                        <Pie
                            data={chartData}
                            dataKey="count"
                            nameKey="status"
                            cx="50%"
                            cy="50%"
                            outerRadius={80}
                            innerRadius={40}
                            paddingAngle={2}
                        />
                    </PieChart>
                </ChartContainer>

                {/* Legend */}
                <div className="mt-4 grid grid-cols-2 gap-2 text-sm">
                    {chartData.map((item) => (
                        <div key={item.status} className="flex items-center gap-2">
                            <div 
                                className="h-3 w-3 rounded-full" 
                                style={{ backgroundColor: item.fill }}
                            />
                            <span className="flex-1">{item.label}</span>
                            <span className="font-medium">{item.count}</span>
                        </div>
                    ))}
                </div>
            </CardContent>
        </Card>
    );
}
