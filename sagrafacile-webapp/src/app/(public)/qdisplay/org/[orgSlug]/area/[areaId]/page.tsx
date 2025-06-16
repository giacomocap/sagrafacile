'use client';

import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useParams } from 'next/navigation';
import apiClient from '@/services/apiClient';
import { getActiveCashierStationsForArea } from '@/services/apiClient'; // Import new function
import {
    QueueStateDto,
    CalledNumberBroadcastDto,
    QueueResetBroadcastDto,
    QueueStateUpdateBroadcastDto,
    CashierStationDto
} from '@/types';
import { toast } from 'sonner';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'; // Removed CardDescription
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, ServerCrash, Volume2, VolumeX } from 'lucide-react'; // Added ServerCrash, Removed Loader2, Added Volume icons
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import useSignalRHub from '@/hooks/useSignalRHub';
import useAnnouncements from '@/hooks/useAnnouncements'; // Import useAnnouncements
import { Button } from '@/components/ui/button';
import AdCarousel from '@/components/public/AdCarousel'; // Import AdCarousel
import { useAds } from '@/hooks/useAds'; // Import the new useAds hook

// Default state if needed
const defaultQueueState: QueueStateDto = {
    areaId: 0,
    isQueueSystemEnabled: false,
    lastCalledNumber: null,
    nextSequentialNumber: 1,
};

const NOTIFICATION_SOUND_URL = '/sounds/queue-chime.mp3';

interface StationCallInfo {
    ticketNumber: number | null;
    cashierStationName: string; // Store name for display consistency
    timestamp: string | null;
}

export default function QueueDisplayPage() {
    const params = useParams();
    // const orgSlug = typeof params.orgSlug === 'string' ? params.orgSlug : ''; // Not used currently
    const areaId = typeof params.areaId === 'string' ? params.areaId : '';

    const [overallQueueState, setOverallQueueState] = useState<QueueStateDto | null>(null);
    const [isLoadingOverallState, setIsLoadingOverallState] = useState(true);
    const [overallStateError, setOverallStateError] = useState<string | null>(null);

    // New state for per-station display
    const [activeStations, setActiveStations] = useState<CashierStationDto[] | null>(null);
    // Use string keys for stationCalls for simpler object manipulation
    const [stationCalls, setStationCalls] = useState<Record<string, StationCallInfo>>({});
    const [isLoadingStations, setIsLoadingStations] = useState(true);
    const [stationsError, setStationsError] = useState<string | null>(null);

    // const audioRef = useRef<HTMLAudioElement | null>(null); // Handled by useAnnouncements
    const connectionSucceededRef = useRef(false);

    const hubUrl = `${process.env.NEXT_PUBLIC_API_BASE_URL}/orderHub`;
    const { connection, connectionStatus, startConnection, stopConnection } = useSignalRHub(hubUrl);
    const { playNotificationSound, speakRawText, unlockAudio } = useAnnouncements({ soundUrl: NOTIFICATION_SOUND_URL, speechRate: 0.5 }); // Instantiate hook
    
    // Use the new useAds hook
    const { adMediaItems } = useAds(areaId);

    const [isAudioUnlocked, setIsAudioUnlocked] = useState(false);

    const handleUnlockAudio = () => {
        unlockAudio();
        setIsAudioUnlocked(true);
        toast.success("Audio abilitato.", {
            description: "Sentirai una notifica sonora e vocale per ogni numero chiamato.",
        });
    };

    // Fetch initial overall queue state
    const fetchInitialOverallState = useCallback(async () => {
        if (!areaId) {
            setOverallStateError("Area ID is missing from the URL.");
            setIsLoadingOverallState(false);
            return;
        }
        setIsLoadingOverallState(true);
        setOverallStateError(null);
        try {
            const response = await apiClient.get<QueueStateDto>(`/public/areas/${areaId}/queue/state`);
            setOverallQueueState(response.data || defaultQueueState);
        } catch (err: unknown) {
            console.error("Error fetching initial overall queue state:", err);
            let errorMsg = 'Failed to fetch overall queue state.';
            if (typeof err === 'object' && err !== null && 'message' in err) {
                errorMsg = String((err as { message: string }).message);
                if ('response' in err && typeof (err as { response?: { data?: { message?: string } } }).response?.data?.message === 'string') {
                    errorMsg = String((err as { response: { data: { message: string } } }).response.data.message);
                }
            }
            setOverallStateError(errorMsg);
            setOverallQueueState(null);
        } finally {
            setIsLoadingOverallState(false);
        }
    }, [areaId]);

    // Fetch active cashier stations
    const fetchActiveStations = useCallback(async () => {
        if (!areaId) {
            setStationsError("Area ID is missing for fetching stations.");
            setIsLoadingStations(false);
            return;
        }
        setIsLoadingStations(true);
        setStationsError(null);
        try {
            const response = await getActiveCashierStationsForArea(areaId);
            const stations = response.data || [];
            setActiveStations(stations);
            const initialStationCalls: Record<string, StationCallInfo> = {};
            stations.forEach(station => {
                initialStationCalls[String(station.id)] = { // Use String(station.id) as key
                    ticketNumber: null,
                    cashierStationName: station.name,
                    timestamp: null
                };
            });
            setStationCalls(initialStationCalls);
        } catch (err: unknown) {
            console.error("Error fetching active cashier stations:", err);
            let errorMsg = 'Failed to fetch cashier stations.';
            if (typeof err === 'object' && err !== null && 'message' in err) {
                errorMsg = String((err as { message: string }).message);
                if ('response' in err && typeof (err as { response?: { data?: { message?: string } } }).response?.data?.message === 'string') {
                    errorMsg = String((err as { response: { data: { message: string } } }).response.data.message);
                }
            }
            setStationsError(errorMsg);
            setActiveStations([]); // Set to empty array on error to allow rendering logic to proceed
        } finally {
            setIsLoadingStations(false);
        }
    }, [areaId]);

    useEffect(() => {
        fetchInitialOverallState();
        fetchActiveStations();
    }, [fetchInitialOverallState, fetchActiveStations]);

    // SignalR Connection Management
    useEffect(() => {
        connectionSucceededRef.current = false;
        startConnection();
        return () => {
            if (connectionSucceededRef.current) {
                stopConnection();
            }
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []); // Run only on mount; assumes start/stop are stable to prevent connection loop.

    // SignalR Group Management
    useEffect(() => {
        if (connectionStatus === 'Connected' && connection && areaId) {
            connectionSucceededRef.current = true;
            connection.invoke('JoinAreaQueueGroup', areaId.toString())
                .then(() => console.log(`QDisplay: Joined AreaQueueGroup for Area ${areaId}`))
                .catch(err => console.error("QDisplay: Failed to join AreaQueueGroup", err));

            return () => {
                connection.invoke('LeaveAreaQueueGroup', areaId.toString())
                    .then(() => console.log(`QDisplay: Left AreaQueueGroup for Area ${areaId}`))
                    .catch(err => console.error("QDisplay: Failed to leave AreaQueueGroup", err));
            };
        }
    }, [connectionStatus, connection, areaId]);

    // SignalR Event Handlers
    useEffect(() => {
        if (!connection || connectionStatus !== 'Connected' || !areaId) return;

        const handleQueueNumberCalled = (data: CalledNumberBroadcastDto) => {
            console.log("QDisplay SignalR: QueueNumberCalled received:", data);
            if (String(data.areaId) === areaId) {
                const stationIdStr = String(data.cashierStationId);
                const stationNameFromBroadcast = data.cashierStationName;

                // Find the station in activeStations to get a reliable name if broadcast name is null
                const stationFromState = activeStations?.find(s => s.id === data.cashierStationId);
                const reliableStationName = stationNameFromBroadcast || stationFromState?.name || "Stazione Sconosciuta";

                setStationCalls(prevCalls => ({
                    ...prevCalls,
                    [stationIdStr]: {
                        ticketNumber: data.ticketNumber,
                        cashierStationName: reliableStationName, // Ensure this is always a string
                        timestamp: data.timestamp,
                    }
                }));

                setOverallQueueState(prevState => {
                    if (!prevState) return null;
                    return {
                        ...prevState,
                        lastCalledNumber: data.ticketNumber,
                        lastCalledCashierStationId: data.cashierStationId,
                        lastCalledCashierStationName: data.cashierStationName,
                        lastCallTimestamp: data.timestamp,
                    };
                });
                if (isAudioUnlocked) {
                    playNotificationSound();
                    speakRawText(`Numero ${data.ticketNumber}, ${reliableStationName.toLowerCase().startsWith("cassa") ? reliableStationName : "Cassa " + reliableStationName}`); // Use speakRawText
                }
                toast.info(`Stazione ${reliableStationName}: Numero ${data.ticketNumber} chiamato.`);
            }
        };

        const handleQueueReset = (data: QueueResetBroadcastDto) => {
            console.log("QDisplay SignalR: QueueReset received:", data);
            if (String(data.areaId) === areaId) {
                toast.info("La coda è stata resettata.");
                // Reset per-station display
                setStationCalls(prevCalls => {
                    const resetCalls: Record<string, StationCallInfo> = {};
                    for (const stationIdKey in prevCalls) { // stationIdKey is already a string
                        if (Object.prototype.hasOwnProperty.call(prevCalls, stationIdKey)) {
                            const existingCallInfo = prevCalls[stationIdKey];
                            resetCalls[stationIdKey] = {
                                ticketNumber: null,
                                cashierStationName: existingCallInfo.cashierStationName, // Preserve existing name
                                timestamp: new Date().toISOString() // Or use data.timestamp if available from QueueResetBroadcastDto
                            };
                        }
                    }
                    return resetCalls;
                });
                fetchInitialOverallState(); // Refetch overall state after reset
            }
        };

        const handleQueueStateUpdated = (data: QueueStateUpdateBroadcastDto) => {
            console.log("QDisplay SignalR: QueueStateUpdated received:", data);
            if (String(data.areaId) === areaId) {
                setOverallQueueState(data.newState);
                // If newState contains per-station info in future, update stationCalls here too.
                // For now, it primarily updates the overallQueueState.
                // If the queue system is disabled via this message, reflect it.
                if (!data.newState.isQueueSystemEnabled) {
                    // Optionally clear station calls or show a disabled message per station
                    setStationCalls(prevCalls => {
                        const updatedCalls: Record<string, StationCallInfo> = {};
                        for (const stationIdKey in prevCalls) {
                            if (Object.prototype.hasOwnProperty.call(prevCalls, stationIdKey)) {
                                updatedCalls[stationIdKey] = { ...prevCalls[stationIdKey], ticketNumber: null };
                            }
                        }
                        return updatedCalls;
                    });
                }
            }
        };

        connection.on('QueueNumberCalled', handleQueueNumberCalled);
        connection.on('QueueReset', handleQueueReset);
        connection.on('QueueStateUpdated', handleQueueStateUpdated);

        return () => {
            connection?.off('QueueNumberCalled', handleQueueNumberCalled);
            connection?.off('QueueReset', handleQueueReset);
            connection?.off('QueueStateUpdated', handleQueueStateUpdated);
        };
    }, [connection, connectionStatus, areaId, fetchInitialOverallState, playNotificationSound, speakRawText, activeStations, isAudioUnlocked]); // Updated dependencies, added activeStations

    // Render Logic
    const renderContent = () => {
        if (isLoadingOverallState || isLoadingStations) {
            return (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 w-full max-w-6xl mx-auto">
                    {[1, 2, 3].map(i => (
                        <Card key={i} className="w-full">
                            <CardHeader>
                                <Skeleton className="h-7 w-3/4" />
                                <Skeleton className="h-4 w-1/2" />
                            </CardHeader>
                            <CardContent className="text-center py-10">
                                <Skeleton className="h-20 w-20 rounded-full mx-auto" />
                                <Skeleton className="h-6 w-1/4 mx-auto mt-2" />
                            </CardContent>
                        </Card>
                    ))}
                </div>
            );
        }

        if (overallStateError || stationsError) {
            return (
                <Alert variant="destructive" className="w-full max-w-md mx-auto">
                    <ServerCrash className="h-5 w-5" />
                    <AlertTitle>Errore di Caricamento</AlertTitle>
                    <AlertDescription>
                        {overallStateError && <p>Errore stato coda: {overallStateError}</p>}
                        {stationsError && <p>Errore stazioni cassa: {stationsError}</p>}
                        <p className="mt-2">Impossibile caricare i dati della coda. Riprova più tardi.</p>
                    </AlertDescription>
                </Alert>
            );
        }

        if (!overallQueueState || !overallQueueState.isQueueSystemEnabled) {
            return (
                <Card className="w-full max-w-lg mx-auto">
                    <CardHeader>
                        <CardTitle className="text-center text-2xl font-bold">Sistema Coda Clienti</CardTitle>
                    </CardHeader>
                    <CardContent className="text-center py-8">
                        <AlertCircle className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                        <p className="text-muted-foreground text-lg">Il sistema di coda clienti non è attivo per quest'area.</p>
                    </CardContent>
                </Card>
            );
        }

        if (!activeStations || activeStations.length === 0) {
            return (
                <Card className="w-full max-w-lg mx-auto">
                    <CardHeader>
                        <CardTitle className="text-center text-2xl font-bold">Sistema Coda Clienti</CardTitle>
                    </CardHeader>
                    <CardContent className="text-center py-8">
                        <AlertCircle className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                        <p className="text-muted-foreground text-lg">Nessuna postazione cassa attiva trovata per quest'area.</p>
                        <p className="text-sm text-muted-foreground mt-1">Attualmente non è possibile visualizzare i numeri chiamati.</p>
                    </CardContent>
                </Card>
            );
        }

        // Main display: Grid of stations with calculated dimensions
        const numStations = activeStations.length;
        // Determine a pleasant layout, trying to keep it squarish
        const numCols = numStations <= 3 ? numStations : Math.ceil(Math.sqrt(numStations));
        const numRows = Math.ceil(numStations / numCols);

        // Use flexbox to create the grid
        return (
            <div className="w-full h-full flex flex-wrap items-stretch justify-center p-2 gap-2">
                {activeStations.map(station => {
                    const callInfo = stationCalls[String(station.id)];
                    const displayTicketNumber = callInfo?.ticketNumber ?? '---';
                    const stationName = callInfo?.cashierStationName || station.name;

                    const cardWidth = `calc(${100 / numCols}% - ${numCols > 1 ? '0.5rem' : '0px'})`;
                    const cardHeight = `calc(${100 / numRows}% - ${numRows > 1 ? '0.5rem' : '0px'})`;

                    return (
                        <div
                            key={station.id}
                            style={{ width: cardWidth, height: cardHeight }}
                            className="bg-card rounded-lg shadow-2xl border-2 border-primary flex flex-col"
                        >
                            <div className="bg-primary text-primary-foreground rounded-t-md p-2 md:p-3">
                                <h3 className="text-center text-lg md:text-2xl xl:text-3xl font-bold truncate" title={stationName}>
                                    {stationName}
                                </h3>
                            </div>
                            <div className="flex-grow flex flex-col items-center justify-center text-center p-2 overflow-hidden">
                                <p
                                    className={`font-extrabold leading-none ${callInfo?.ticketNumber ? 'text-primary animate-pulse-slow' : 'text-muted-foreground/80'}`}
                                    // Adjust font size dynamically based on container size
                                    style={{ fontSize: 'clamp(3rem, 20vh, 12rem)' }}
                                >
                                    {displayTicketNumber}
                                </p>
                                <p className="text-base md:text-xl font-medium text-muted-foreground mt-2">
                                    {callInfo?.ticketNumber ? 'NUMERO SERVITO' : 'IN ATTESA'}
                                </p>
                                {callInfo?.timestamp && callInfo?.ticketNumber && (
                                    <p className="text-xs md:text-base text-muted-foreground/70 mt-2">
                                        Chiamato alle: {new Date(callInfo.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                                    </p>
                                )}
                            </div>
                        </div>
                    );
                })}
            </div>
        );
    };

    return (
        <div className="h-screen w-screen bg-gradient-to-br from-background to-muted flex flex-col">
            {!isAudioUnlocked && (
                <div className="absolute inset-0 z-50 bg-black/80 backdrop-blur-sm flex flex-col items-center justify-center">
                    <Card className="max-w-md text-center p-8">
                        <CardHeader>
                            <VolumeX className="w-16 h-16 mx-auto text-muted-foreground" />
                            <CardTitle className="text-2xl font-bold mt-4">Audio Disabilitato</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <p className="text-muted-foreground mb-6">
                                Per ricevere le notifiche sonore e vocali, è necessario abilitare l'audio cliccando il pulsante qui sotto.
                            </p>
                            <Button size="lg" onClick={handleUnlockAudio}>
                                <Volume2 className="mr-2 h-5 w-5" />
                                Abilita Audio
                            </Button>
                        </CardContent>
                    </Card>
                </div>
            )}

            {/* Main content area for the queue display */}
            <main className={`h-[65vh] flex flex-col items-center justify-center overflow-hidden ${!isAudioUnlocked ? 'blur-sm' : ''}`}>
                {renderContent()}
                {/* Footer for overall last called number (optional, can be removed) */}
                {overallQueueState?.isQueueSystemEnabled && activeStations && activeStations.length > 0 && overallQueueState.lastCalledNumber && (
                    <div className="mt-8 md:mt-12 p-4 bg-card/80 backdrop-blur-sm rounded-lg shadow-lg text-center w-full max-w-lg md:max-w-xl mx-auto">
                        <p className="text-base text-card-foreground">
                            Ultima Chiamata Globale:
                            <span className="font-bold text-primary"> N° {overallQueueState.lastCalledNumber}</span>
                            {overallQueueState.lastCalledCashierStationName && ` da ${overallQueueState.lastCalledCashierStationName}`}
                            {overallQueueState.lastCallTimestamp && ` (${new Date(overallQueueState.lastCallTimestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })})`}
                        </p>
                    </div>
                )}
            </main>

            {/* Footer area for the ad carousel */}
            <footer className={`flex-shrink-0 h-[35vh] bg-transparent ${!isAudioUnlocked ? 'blur-sm' : ''}`}>
                <AdCarousel mediaItems={adMediaItems} />
            </footer>
        </div>
    );
}

// Styles (no change)
const styles = `
@keyframes pulse-slow {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.85; transform: scale(1.02); }
}
.animate-pulse-slow { animation: pulse-slow 2.5s cubic-bezier(0.4, 0, 0.6, 1) infinite; }
`;

if (typeof window !== 'undefined') {
    const styleSheet = document.createElement("style");
    styleSheet.type = "text/css";
    styleSheet.innerText = styles;
    document.head.appendChild(styleSheet);
}
