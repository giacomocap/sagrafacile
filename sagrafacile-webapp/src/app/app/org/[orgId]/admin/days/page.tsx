'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { useOrganization } from '@/contexts/OrganizationContext';
import { useAuth } from '@/contexts/AuthContext';
import * as dayService from '@/services/dayService'; // Import specific functions
import { DayDto, DayStatus } from '@/types';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Loader2, CalendarPlus, CalendarOff } from 'lucide-react';
import { toast } from 'sonner';
// Removed date-fns import

const AdminDaysPage = () => {
    const { selectedOrganizationId, currentDay, refreshCurrentDay, isLoadingDay } = useOrganization();
    const { user, isLoading: isAuthLoading } = useAuth();
    const [days, setDays] = useState<DayDto[]>([]);
    const [isLoadingDays, setIsLoadingDays] = useState(false);
    const [isOperating, setIsOperating] = useState(false); // For open/close actions

    // const orgId = params.orgId as string; // orgId from params is not used
    const isAdmin = user?.roles?.includes('Admin') || user?.roles?.includes('SuperAdmin');

    const fetchDays = useCallback(async () => {
        if (!selectedOrganizationId || isAuthLoading) return;
        setIsLoadingDays(true);
        try {
            const fetchedDays = await dayService.getDays();
            setDays(fetchedDays || []);
        } catch (error) {
            console.error("Errore nel recupero delle giornate:", error);
            const errorMessage = (error instanceof Error) ? error.message : 'Errore sconosciuto';
            toast.error(`Impossibile caricare le giornate: ${errorMessage}`);
            setDays([]);
        } finally {
            setIsLoadingDays(false);
        }
    }, [selectedOrganizationId, isAuthLoading]);

    useEffect(() => {
        fetchDays();
    }, [fetchDays]);

    // Refetch days and current day status after an operation
    const refreshData = () => {
        fetchDays();
        refreshCurrentDay(); // Refresh context state
    };

    const handleOpenDay = async () => {
        if (!isAdmin || currentDay) return; // Prevent opening if already open or not admin
        setIsOperating(true);
        try {
            await dayService.openDay();
            toast.success("Giornata aperta con successo!");
            refreshData();
        } catch (error) {
            console.error("Errore nell'apertura della giornata:", error);
            const errorMessage = (error instanceof Error) ? error.message : 'Errore sconosciuto';
            toast.error(`Impossibile aprire la giornata: ${errorMessage}`);
        } finally {
            setIsOperating(false);
        }
    };

    const handleCloseDay = async () => {
        if (!isAdmin || !currentDay) return; // Prevent closing if not open or not admin
        setIsOperating(true);
        try {
            await dayService.closeDay(currentDay.id);
            toast.success("Giornata chiusa con successo!");
            refreshData();
        } catch (error) {
            console.error("Errore nella chiusura della giornata:", error);
            const errorMessage = (error instanceof Error) ? error.message : 'Errore sconosciuto';
            toast.error(`Impossibile chiudere la giornata: ${errorMessage}`);
        } finally {
            setIsOperating(false);
        }
    };

    const isLoading = isLoadingDays || isLoadingDay || isAuthLoading || isOperating;

    return (
        <>
            <Card>
                <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-4">
                    <CardTitle>Gestione Giornate Operative</CardTitle>
                    {isAdmin && (
                        <div className="flex space-x-2">
                            <Button
                                onClick={handleOpenDay}
                                disabled={isLoading || !!currentDay} // Disable if loading or a day is already open
                                size="sm"
                            >
                                {isOperating && !currentDay ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CalendarPlus className="mr-2 h-4 w-4" />}
                                Apri Giornata
                            </Button>
                            <Button
                                variant="destructive"
                                onClick={handleCloseDay}
                                disabled={isLoading || !currentDay} // Disable if loading or no day is open
                                size="sm"
                            >
                                {isOperating && currentDay ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CalendarOff className="mr-2 h-4 w-4" />}
                                Chiudi Giornata Corrente
                            </Button>
                        </div>
                    )}
                </CardHeader>
                <CardContent>
                    {isLoadingDays ? (
                        <div className="flex justify-center items-center py-10">
                            <Loader2 className="h-8 w-8 animate-spin" />
                            <span className="ml-2">Caricamento giornate...</span>
                        </div>
                    ) : days.length === 0 ? (
                        <div className="text-center py-10 text-muted-foreground">
                            Nessuna giornata operativa trovata per questa organizzazione.
                        </div>
                    ) : (
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>ID</TableHead>
                                    <TableHead>Stato</TableHead>
                                    <TableHead>Inizio</TableHead>
                                    <TableHead>Fine</TableHead>
                                    <TableHead>Aperta Da (ID)</TableHead>
                                    <TableHead>Chiusa Da (ID)</TableHead>
                                    <TableHead className="text-right">Incasso Totale</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {days.map((day) => (
                                    <TableRow key={day.id}>
                                        <TableCell>{day.id}</TableCell>
                                        <TableCell>
                                            <Badge variant={day.status === DayStatus.Open ? 'default' : 'secondary'}>
                                                {day.status === DayStatus.Open ? 'Aperta' : 'Chiusa'}
                                            </Badge>
                                        </TableCell>
                                        <TableCell>{new Date(day.startTime).toLocaleString()}</TableCell>
                                        <TableCell>{day.endTime ? new Date(day.endTime).toLocaleString() : '-'}</TableCell>
                                        <TableCell>{day.openedByUserId || '-'}</TableCell>
                                        <TableCell>{day.closedByUserId || '-'}</TableCell>
                                        <TableCell className="text-right">
                                            {day.totalSales != null ? `€ ${day.totalSales.toFixed(2)}` : '-'}
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    )}
                </CardContent>
            </Card>
        </>
    );
};

export default AdminDaysPage;
