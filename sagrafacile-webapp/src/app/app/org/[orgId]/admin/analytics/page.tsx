'use client';

import React, { useState, useEffect } from 'react';
import { useParams } from 'next/navigation';
import { useOrganization } from '@/contexts/OrganizationContext';
import { DashboardKPIs } from '@/components/charts/dashboard/DashboardKPIs';
import { SalesTrendChart } from '@/components/charts/dashboard/SalesTrendChart';
import { OrderStatusChart } from '@/components/charts/dashboard/OrderStatusChart';
import { TopMenuItemsChart } from '@/components/charts/dashboard/TopMenuItemsChart';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { CalendarDays } from "lucide-react";
import { getDays } from '@/services/dayService';
import { DayDto } from '@/types';

export default function AnalyticsPage() {
    const { currentDay } = useOrganization();
    const params = useParams();
    const currentOrgId = params.orgId as string;
    
    const [availableDays, setAvailableDays] = useState<DayDto[]>([]);
    const [selectedDayId, setSelectedDayId] = useState<number | undefined>(currentDay?.id);
    const [loadingDays, setLoadingDays] = useState(true);

    // Fetch available days
    useEffect(() => {
        const fetchDays = async () => {
            try {
                const days = await getDays();
                setAvailableDays(days);
            } catch (error) {
                console.error('Error fetching days:', error);
            } finally {
                setLoadingDays(false);
            }
        };

        fetchDays();
    }, []);

    // Update selected day when current day changes
    useEffect(() => {
        if (currentDay?.id && !selectedDayId) {
            setSelectedDayId(currentDay.id);
        }
    }, [currentDay, selectedDayId]);

    const handleDayChange = (value: string) => {
        if (value === 'current') {
            setSelectedDayId(currentDay?.id);
        } else {
            setSelectedDayId(parseInt(value, 10));
        }
    };

    const formatDayLabel = (day: DayDto) => {
        const date = new Date(day.startTime).toLocaleDateString('it-IT');
        const status = day.status === 0 ? 'Aperta' : 'Chiusa';
        return `${date} (${status})`;
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="space-y-2">
                <h1 className="text-2xl sm:text-3xl font-bold">Analytics Dashboard</h1>
                <p className="text-muted-foreground">
                    Analisi dettagliate delle vendite e delle performance operative
                </p>
            </div>

            {/* Day Selection */}
            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <CalendarDays className="h-5 w-5" />
                        Selezione Giornata
                    </CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="flex items-center gap-4">
                        <label htmlFor="day-select" className="text-sm font-medium">
                            Visualizza dati per:
                        </label>
                        <Select
                            value={selectedDayId ? selectedDayId.toString() : 'current'}
                            onValueChange={handleDayChange}
                            disabled={loadingDays}
                        >
                            <SelectTrigger className="w-[300px]">
                                <SelectValue placeholder="Seleziona una giornata" />
                            </SelectTrigger>
                            <SelectContent>
                                {currentDay && (
                                    <SelectItem value="current">
                                        Giornata Corrente ({formatDayLabel(currentDay)})
                                    </SelectItem>
                                )}
                                {availableDays.map((day) => (
                                    <SelectItem key={day.id} value={day.id.toString()}>
                                        {formatDayLabel(day)}
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>
                </CardContent>
            </Card>

            {/* KPIs Section */}
            <div className="space-y-4">
                <h2 className="text-xl font-semibold">Indicatori Chiave di Performance</h2>
                <DashboardKPIs 
                    organizationId={currentOrgId} 
                    dayId={selectedDayId}
                />
            </div>

            {/* Charts Section */}
            <div className="space-y-6">
                <h2 className="text-xl font-semibold">Grafici e Tendenze</h2>
                
                {/* First Row - Sales Trend and Order Status */}
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                    <SalesTrendChart organizationId={currentOrgId} />
                    <OrderStatusChart 
                        organizationId={currentOrgId} 
                        dayId={selectedDayId}
                    />
                </div>

                {/* Second Row - Top Menu Items (full width) */}
                <div className="grid grid-cols-1 gap-6">
                    <TopMenuItemsChart organizationId={currentOrgId} />
                </div>
            </div>

            {/* Future Enhancement Placeholder */}
            <div className="space-y-4">
                <h2 className="text-xl font-semibold">Report e Esportazioni</h2>
                <div className="bg-muted/30 border border-dashed border-muted-foreground/25 rounded-lg p-8 text-center">
                    <p className="text-muted-foreground">
                        Funzionalità di generazione report in arrivo...
                    </p>
                    <p className="text-sm text-muted-foreground mt-2">
                        Presto sarà possibile esportare report giornalieri e di performance delle aree
                    </p>
                </div>
            </div>
        </div>
    );
}
