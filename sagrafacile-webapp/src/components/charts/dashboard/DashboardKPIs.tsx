import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { TrendingUp, ShoppingCart, Euro, Trophy, Users } from "lucide-react";
import { analyticsService } from '@/services/analyticsService';
import { DashboardKPIsDto } from '@/types';
import { LoadingChart } from '../shared/LoadingChart';
import { EmptyChart } from '../shared/EmptyChart';

interface DashboardKPIsProps {
    organizationId: number;
    dayId?: number;
    refreshInterval?: number; // in milliseconds
}

export function DashboardKPIs({ organizationId, dayId, refreshInterval = 300000 }: DashboardKPIsProps) {
    const [data, setData] = useState<DashboardKPIsDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const fetchData = async () => {
        try {
            setError(null);
            const result = await analyticsService.getDashboardKPIs(organizationId, dayId);
            setData(result);
        } catch (err) {
            console.error('Error fetching dashboard KPIs:', err);
            setError('Errore nel caricamento dei dati');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
        
        // Set up periodic refresh
        const interval = setInterval(fetchData, refreshInterval);
        return () => clearInterval(interval);
    }, [organizationId, dayId, refreshInterval]);

    if (loading) {
        return <LoadingChart height={200} />;
    }

    if (error || !data) {
        return (
            <EmptyChart
                title="KPI Dashboard"
                message={error || "Nessun dato disponibile"}
                isError={!!error}
                height={200}
            />
        );
    }

    const formatCurrency = (amount: number) => {
        return new Intl.NumberFormat('it-IT', {
            style: 'currency',
            currency: 'EUR'
        }).format(amount);
    };

    const formatDate = (dateString: string | null) => {
        if (!dateString) return 'N/A';
        return new Date(dateString).toLocaleDateString('it-IT');
    };

    return (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
            {/* Total Sales */}
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle className="text-sm font-medium">
                        Vendite Totali
                    </CardTitle>
                    <Euro className="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                    <div className="text-2xl font-bold">
                        {formatCurrency(data.todayTotalSales)}
                    </div>
                    <p className="text-xs text-muted-foreground">
                        {data.dayDate ? `Giornata del ${formatDate(data.dayDate)}` : 'Oggi'}
                    </p>
                </CardContent>
            </Card>

            {/* Order Count */}
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle className="text-sm font-medium">
                        Ordini Totali
                    </CardTitle>
                    <ShoppingCart className="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                    <div className="text-2xl font-bold">
                        {data.todayOrderCount}
                    </div>
                    <p className="text-xs text-muted-foreground">
                        {data.dayDate ? `Giornata del ${formatDate(data.dayDate)}` : 'Oggi'}
                    </p>
                </CardContent>
            </Card>

            {/* Average Order Value */}
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle className="text-sm font-medium">
                        Valore Medio Ordine
                    </CardTitle>
                    <TrendingUp className="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                    <div className="text-2xl font-bold">
                        {formatCurrency(data.averageOrderValue)}
                    </div>
                    <p className="text-xs text-muted-foreground">
                        Media per ordine
                    </p>
                </CardContent>
            </Card>

            {/* Total Coperti */}
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle className="text-sm font-medium">
                        Coperti Totali
                    </CardTitle>
                    <Users className="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                    <div className="text-2xl font-bold">
                        {data.totalCoperti}
                    </div>
                    <p className="text-xs text-muted-foreground">
                        Ospiti serviti
                    </p>
                </CardContent>
            </Card>

            {/* Most Popular Category */}
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle className="text-sm font-medium">
                        Categoria Top
                    </CardTitle>
                    <Trophy className="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                    <div className="text-2xl font-bold">
                        {data.mostPopularCategory || 'N/A'}
                    </div>
                    <p className="text-xs text-muted-foreground">
                        Più venduta oggi
                    </p>
                </CardContent>
            </Card>
        </div>
    );
}
