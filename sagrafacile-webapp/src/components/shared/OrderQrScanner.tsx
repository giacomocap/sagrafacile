"use client";

import React from 'react';
import { Button } from '@/components/ui/button';
import { XCircle, ScanLine } from 'lucide-react';
import { Scanner, IDetectedBarcode } from '@yudiel/react-qr-scanner';
import { toast } from 'sonner';

interface OrderQrScannerProps {
    showScanner: boolean;
    onShowScannerChange: (show: boolean) => void;
    onScanSuccess: (orderId: string) => void;
    forceShowScanner?: boolean;
}

const OrderQrScanner: React.FC<OrderQrScannerProps> = ({
    showScanner,
    onShowScannerChange,
    onScanSuccess,
    forceShowScanner = false,
}) => {

    const handleScanResult = (result: IDetectedBarcode[]) => {
        if (result && result.length > 0) {
            const rawValue = result[0].rawValue;
            console.log(`Raw scan result: ${rawValue}`);

            const parts = rawValue.split('_');
            const orderId = parts.pop();

            if (orderId && orderId.length === 36) {
                console.log(`Extracted Order ID: ${orderId}`);
                onScanSuccess(orderId);
                onShowScannerChange(false);
            } else {
                console.error(`Failed to extract valid Order ID from: ${rawValue}. Extracted: "${orderId}"`);
                toast.error(`Formato QR Code non valido. Impossibile estrarre ID Ordine.`);
                onShowScannerChange(false);
            }
        }
    };

    const handleScanError = (error: unknown) => {
        console.error("QR Scanner Error:", error);
        let message = 'Errore durante la scansione.';
        if (error instanceof Error) {
            if (error.name === 'NotAllowedError') {
                message = 'Accesso alla fotocamera negato. Per favore concedi i permessi.';
            } else {
                message = `Errore scanner: ${error.message}`;
            }
        }
        toast.error(message);
    onShowScannerChange(false);
};

if (!showScanner && !forceShowScanner) {
    return (
        <div className="flex flex-col items-center space-y-4">
                <p className="text-muted-foreground text-center">Scansiona il QR code sulla ricevuta del cliente.</p>
                <Button onClick={() => onShowScannerChange(true)} size="lg">
                    <ScanLine className="mr-2 h-6 w-6" />
                    Avvia Scansione
                </Button>
            </div>
        );
    }

    return (
        <div className="flex flex-col items-center space-y-4">
            <p className="text-muted-foreground text-center">Inquadra il QR code con la fotocamera.</p>
            <div className="w-full max-w-md relative">
                <Scanner
                    onScan={handleScanResult}
                    onError={handleScanError}
                    components={{ finder: true, torch: true }}
                    styles={{
                        container: { width: '100%', paddingTop: '100%', position: 'relative' },
                        video: { position: 'absolute', top: 0, left: 0, width: '100%', height: '100%' }
                    }}
                    scanDelay={500}
                />
                <Button
                    variant="destructive"
                    size="sm"
                    onClick={() => onShowScannerChange(false)}
                    className="absolute top-2 right-2 z-10"
                >
                    <XCircle className="h-4 w-4 mr-1" /> Annulla
                </Button>
            </div>
        </div>
    );
};

export default OrderQrScanner;
