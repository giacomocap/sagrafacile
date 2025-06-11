'use client';

import React, { Suspense, useEffect, useState } from 'react';
import Image from 'next/image'; // Import next/image
import { useSearchParams } from 'next/navigation';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, Download } from 'lucide-react'; // Added Download icon
import { Button } from '@/components/ui/button'; // Added Button import

function SuccessContent() {
    const searchParams = useSearchParams();
    const orderId = searchParams.get('orderId');
    const orgName = searchParams.get('orgName') || 'la sagra';
    const areaName = searchParams.get('areaName') || 'la cassa';
    const [qrCodeBase64, setQrCodeBase64] = useState<string | null>(null);
    const [isLoadingQr, setIsLoadingQr] = useState(true);

    useEffect(() => {
        if (orderId) {
            const storageKey = `preorderQrCode_${orderId}`;
            try {
                const storedQr = sessionStorage.getItem(storageKey);
                if (storedQr) {
                    setQrCodeBase64(storedQr);
                    // DO NOT remove from sessionStorage here to allow refresh
                } else {
                    // Attempt to retrieve only if not already loaded (e.g., after refresh)
                    console.warn(`QR Code not found in sessionStorage for order ${orderId}. User might have cleared session or navigated away.`);
                }
            } catch (error) {
                console.error("Error accessing sessionStorage:", error);
            } finally {
                setIsLoadingQr(false);
            }
        } else {
            setIsLoadingQr(false); // No orderId, so nothing to load
        }
    }, [orderId]); // Depend on orderId

    const handleDownloadQr = () => {
        if (!qrCodeBase64 || !orderId) return;

        const link = document.createElement('a');
        link.href = `data:image/png;base64,${qrCodeBase64}`;
        link.download = `preorder-qrcode-${orderId}.png`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    };

    return (
        <div className="flex justify-center items-center min-h-screen bg-background p-4">
            <Card className="w-full max-w-md text-center">
                <CardHeader>
                    <CardTitle className="text-2xl font-bold text-green-600">Grazie per il tuo Pre-Ordine!</CardTitle>
                    {orderId && (
                        <CardDescription className="text-lg font-semibold">
                            Il tuo ID ordine è: #{orderId}
                        </CardDescription>
                    )}
                </CardHeader>
                <CardContent className="space-y-6">
                    {/* Display QR Code */}
                    <div className="flex flex-col items-center space-y-2">
                        {isLoadingQr ? (
                            <>
                                <Skeleton className="h-6 w-48" />
                                <Skeleton className="w-48 h-48 rounded-md" />
                            </>
                        ) : qrCodeBase64 ? (
                            <>
                                <p className="font-semibold">Scansiona questo QR Code alla cassa:</p>
                                <Image
                                    src={`data:image/png;base64,${qrCodeBase64}`}
                                    alt={`QR Code for Order ${orderId}`}
                                    width={192} // w-48 is 12rem = 192px
                                    height={192} // h-48 is 12rem = 192px
                                    className="border rounded-md"
                                />
                                <Button variant="outline" size="sm" onClick={handleDownloadQr} className="mt-2">
                                    <Download className="mr-2 h-4 w-4" />
                                    Download QR Code
                                </Button>
                            </>
                        ) : (
                            <div className="text-center text-orange-600 bg-orange-100 p-3 rounded-md border border-orange-300 flex items-center gap-2">
                                <AlertCircle className="h-5 w-5" />
                                <span>QR Code non disponibile qui. Controlla la tua email.</span>
                            </div>
                        )}
                    </div>

                    <div className="bg-blue-100 border-l-4 border-blue-500 text-blue-700 p-4 rounded-md text-left">
                         <p className="font-bold">Email di Conferma Inviata!</p>
                         <p className="text-sm mt-1">
                             Controlla la tua casella di posta elettronica ({searchParams.get('email') || 'indirizzo fornito'}). Troverai una email con il riepilogo dell'ordine e lo stesso QR Code (se disponibile).
                         </p>
                    </div>

                    <div className="bg-yellow-100 border-l-4 border-yellow-500 text-yellow-700 p-4 rounded-md text-left">
                        <p className="font-bold">Istruzioni Importanti:</p>
                        <ul className="list-disc list-inside mt-2 text-sm space-y-1">
                            <li>Recati presso la cassa dell'area "{areaName}" di "{orgName}".</li>
                            <li>Tieni pronto l'importo esatto o la carta per il pagamento.</li>
                            <li>Mostra il QR Code qui sopra (o quello ricevuto via email) o comunica il tuo ID ordine (#{orderId || '...'}).</li>
                            <li>Riceverai lo scontrino per il ritiro dei prodotti.</li>
                        </ul>
                    </div>

                    {/* Removed redundant email confirmation text */}
                </CardContent>
            </Card>
        </div>
    );
}

// Using Suspense for useSearchParams as recommended by Next.js
export default function PreOrderSuccessPage() {
    return (
        <Suspense fallback={<div className="flex justify-center items-center min-h-screen"><Skeleton className="w-full max-w-md h-96" /></div>}>
            <SuccessContent />
        </Suspense>
    );
}
