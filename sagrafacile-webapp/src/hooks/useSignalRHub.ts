import { useState, useEffect, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { toast } from 'sonner'; // Assuming sonner is used for toasts

// Define connection states
export enum SignalRConnectionStatus {
    Connecting = 'Connecting',
    Connected = 'Connected',
    Disconnected = 'Disconnected',
    Reconnecting = 'Reconnecting',
    Error = 'Error',
}

// Define the hook's return type
interface UseSignalRHubResult {
    connection: signalR.HubConnection | null;
    connectionStatus: SignalRConnectionStatus;
    error: Error | null;
    startConnection: () => Promise<void>;
    stopConnection: () => Promise<void>;
}

const useSignalRHub = (hubUrl: string): UseSignalRHubResult => {
    const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
    const [connectionStatus, setConnectionStatus] = useState<SignalRConnectionStatus>(SignalRConnectionStatus.Disconnected);
    const [error, setError] = useState<Error | null>(null);
    // Use a ref to track if the component is mounted to avoid state updates after unmount
    const isMounted = useRef(true);

    // Ensure hubUrl is stable or memoized if passed dynamically
    const hubUrlRef = useRef(hubUrl);
    useEffect(() => {
        hubUrlRef.current = hubUrl;
    }, [hubUrl]);

    const createConnection = useCallback(() => {
        console.log(`Attempting to build SignalR connection to: ${hubUrlRef.current}`);
        const newConnection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrlRef.current, {
                // Provide the access token factory
                accessTokenFactory: () => {
                    // Retrieve the token from local storage (adjust key if necessary)
                    // Retrieve the token from local storage (adjust key if necessary)
                    const token = localStorage.getItem('authToken');
                    // console.log("SignalR accessTokenFactory - Token:", token ? "Token Found" : "Token Not Found"); // Optional: Add for debugging
                    // Return the token or an empty string if not found, to satisfy the type requirement.
                    return token || '';
                },
                // Optional: Configure transport types or other options
                // skipNegotiation: true, // Example: Use if negotiation is handled differently
                // transport: signalR.HttpTransportType.WebSockets // Example: Force WebSockets
            })
            .withAutomaticReconnect({
                // Configure reconnection attempts and delays
                nextRetryDelayInMilliseconds: retryContext => {
                    // Example: 0, 2, 4, 8, 10 seconds, then stop
                    if (retryContext.previousRetryCount < 5) {
                        return Math.pow(2, retryContext.previousRetryCount) * 1000;
                    }
                    return null; // Stop retrying after 5 attempts
                }
            })
            .configureLogging(signalR.LogLevel.Information) // Adjust log level as needed (Warning, Error, None)
            .build();

        // --- Connection Event Handlers ---

        newConnection.onreconnecting((error) => {
            console.warn(`SignalR connection reconnecting due to: ${error}`);
            if (isMounted.current) {
                setConnectionStatus(SignalRConnectionStatus.Reconnecting);
                setError(error ?? new Error("SignalR reconnecting"));
            }
        });

        newConnection.onreconnected((connectionId) => {
            console.log(`SignalR connection reconnected with ID: ${connectionId}`);
            if (isMounted.current) {
                setConnectionStatus(SignalRConnectionStatus.Connected);
                setError(null);
                toast.success("Real-time connection restored.");
            }
        });

        newConnection.onclose((error) => {
            console.error(`SignalR connection closed. Error: ${error}`);
            if (isMounted.current) {
                // If it closed after failing to reconnect, it's an error state
                // If it was closed manually (stopConnection), it's Disconnected
                // We might need more logic here if stopConnection doesn't set state first
                setConnectionStatus(SignalRConnectionStatus.Disconnected);
                setError(error ?? new Error("SignalR connection closed unexpectedly."));
                // Don't show toast if closed manually
                if (error) {
                     toast.error("Real-time connection lost. Attempting to reconnect...");
                }
            }
        });

        setConnection(newConnection);
        return newConnection;
    }, []); // Dependency array includes only stable refs/functions

    const startConnection = useCallback(async () => {
        // Prevent starting if already connected or connecting
        if (connection?.state === signalR.HubConnectionState.Connected || connection?.state === signalR.HubConnectionState.Connecting) {
             console.log(`SignalR startConnection called but state is already ${connection.state}.`);
             return;
         }
        // Also check our internal status as a backup / for initial state
        if (connectionStatus === SignalRConnectionStatus.Connecting || connectionStatus === SignalRConnectionStatus.Connected) {
            console.log(`SignalR startConnection called but status is already ${connectionStatus}.`);
            return;
        }

        let currentConnection = connection;
        if (!currentConnection) {
            currentConnection = createConnection();
        }

        if (!currentConnection) {
            console.error("Failed to create SignalR connection.");
            setError(new Error("Failed to create SignalR connection."));
            setConnectionStatus(SignalRConnectionStatus.Error);
            return;
        }

        setError(null);
        setConnectionStatus(SignalRConnectionStatus.Connecting);
        console.log("Starting SignalR connection...");

        try {
            // Double-check the actual state right before starting
            if (currentConnection.state === signalR.HubConnectionState.Disconnected) {
                await currentConnection.start();
                if (isMounted.current) {
                    setConnectionStatus(SignalRConnectionStatus.Connected);
                    console.log("SignalR connection established successfully.");
                    toast.success("Real-time connection established.");
                }
            } else {
                 console.warn(`SignalR startConnection aborted; connection state was ${currentConnection.state} before start.`);
                 // If it's somehow connected now, update status
                 if (currentConnection.state === signalR.HubConnectionState.Connected && isMounted.current) {
                    setConnectionStatus(SignalRConnectionStatus.Connected);
                 }
            }
        } catch (err) {
            console.error("SignalR connection failed:", err);
            if (isMounted.current) {
                setConnectionStatus(SignalRConnectionStatus.Error);
                setError(err instanceof Error ? err : new Error(String(err)));
                toast.error("Failed to establish real-time connection.");
            }
            // Attempt cleanup even on failed start
            try {
                await currentConnection.stop();
            } catch (stopErr) {
                console.error("Error stopping connection after failed start:", stopErr);
            }
            if (isMounted.current) {
                 setConnection(null); // Reset connection if start failed completely
            }
        }
    }, [connection, connectionStatus, createConnection]); // Keep dependencies

    const stopConnection = useCallback(async () => {
        // Prevent stopping if already disconnected
        if (connectionStatus === SignalRConnectionStatus.Disconnected && (!connection || connection.state === signalR.HubConnectionState.Disconnected)) {
            console.log("SignalR stopConnection called but connection is already disconnected.");
            return;
        }

        if (connection) {
            console.log("Stopping SignalR connection...");
            // Set status optimistically, but rely on onclose for final state if stop fails
            // setConnectionStatus(SignalRConnectionStatus.Disconnected); // Let onclose handle final state? Maybe safer. Let's keep it for now.
            setConnectionStatus(SignalRConnectionStatus.Disconnected); // Keep setting status first
            setError(null); // Clear errors on manual stop attempt
            try {
                // Only call stop if actually connected
                if (connection.state === signalR.HubConnectionState.Connected || connection.state === signalR.HubConnectionState.Connecting) {
                    await connection.stop();
                    console.log("SignalR connection stopped successfully via stopConnection.");
                    // setConnection(null); // Let onclose handle this? No, safer to clear here on explicit stop.
                    if (isMounted.current) {
                         setConnection(null); // Clear connection object after stopping
                    }
                } else {
                    console.warn(`SignalR stopConnection called but connection state was ${connection.state}.`);
                     // If already disconnected, ensure state reflects this
                     if (connection.state === signalR.HubConnectionState.Disconnected && isMounted.current) {
                         setConnectionStatus(SignalRConnectionStatus.Disconnected);
                         setConnection(null);
                     }
                }
            } catch (err) {
                console.error("Failed to stop SignalR connection:", err);
                // Even if stop fails, we probably want to consider it disconnected.
                if (isMounted.current) {
                    setConnectionStatus(SignalRConnectionStatus.Disconnected); // Ensure status is Disconnected
                    setError(err instanceof Error ? err : new Error(String(err)));
                    setConnection(null); // Attempt to clear connection ref even on error
                }
            }
        } else {
             // If connection is null, ensure status is Disconnected
             if (isMounted.current && connectionStatus !== SignalRConnectionStatus.Disconnected) {
                 setConnectionStatus(SignalRConnectionStatus.Disconnected);
             }
        }
    }, [connection, connectionStatus]); // Added connectionStatus dependency

    // Effect to manage component mount status
    useEffect(() => {
        isMounted.current = true;
        return () => {
            isMounted.current = false;
        };
    }, []);

    // Effect for cleanup: Stop connection when the component unmounts
    useEffect(() => {
        // This function will be called when the component unmounts
        return () => {
            console.log("useSignalRHub unmounting. Stopping connection...");
            // Use the connection state directly, not the callback ref
            connection?.stop().catch(err => console.error("Error stopping SignalR connection on unmount:", err));
        };
    }, [connection]); // Depend on connection state

    return { connection, connectionStatus, error, startConnection, stopConnection };
};

export default useSignalRHub;
