'use client';

import React, { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useOrganization } from '@/contexts/OrganizationContext';
import { useInstance } from '@/contexts/InstanceContext';
import apiClient from '@/services/apiClient';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { AlertTriangle, Download, Trash2, Loader2, AlertCircle } from 'lucide-react';

export default function OrganizationSettingsPage() {
    const params = useParams();
    const router = useRouter();
    const { currentOrganization, isSuperAdminContext } = useOrganization();
    const { instanceInfo } = useInstance();
    const currentOrgId = params.orgId as string;

    // Organization deletion states
    const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
    const [deleteConfirmationText, setDeleteConfirmationText] = useState('');
    const [isDeleting, setIsDeleting] = useState(false);
    const [deleteError, setDeleteError] = useState<string | null>(null);

    // Data export states
    const [isExporting, setIsExporting] = useState(false);
    const [exportError, setExportError] = useState<string | null>(null);
    const [exportSuccess, setExportSuccess] = useState(false);

    const isSaaSMode = instanceInfo?.mode === 'saas';

    const handleDeleteOrganization = async () => {
        if (!currentOrganization) return;

        // Validate confirmation text
        if (deleteConfirmationText !== currentOrganization.name) {
            setDeleteError('Il nome dell\'organizzazione non corrisponde.');
            return;
        }

        setDeleteError(null);
        setIsDeleting(true);

        try {
            await apiClient.delete(`/Organizations/${currentOrgId}`);
            
            // Redirect to appropriate page after successful deletion
            if (isSuperAdminContext) {
                // SuperAdmin should be redirected to organization selection or main admin
                router.replace('/app/admin');
            } else {
                // Regular admin should be logged out or redirected to login
                router.replace('/app/login');
            }
        } catch (err: unknown) {
            console.error("Eliminazione organizzazione fallita:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setDeleteError(error.response?.data?.message || error.message || 'Eliminazione organizzazione fallita.');
        } finally {
            setIsDeleting(false);
        }
    };

    const handleExportData = async () => {
        if (!currentOrganization) return;

        setExportError(null);
        setExportSuccess(false);
        setIsExporting(true);

        try {
            await apiClient.post(`/Organizations/${currentOrgId}/export`);
            setExportSuccess(true);
        } catch (err: unknown) {
            console.error("Esportazione dati fallita:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setExportError(error.response?.data?.message || error.message || 'Esportazione dati fallita.');
        } finally {
            setIsExporting(false);
        }
    };

    const openDeleteDialog = () => {
        setDeleteConfirmationText('');
        setDeleteError(null);
        setIsDeleteDialogOpen(true);
    };

    if (!currentOrganization) {
        return (
            <div className="flex justify-center items-center h-full">
                <Loader2 className="h-8 w-8 animate-spin" />
                <span className="ml-2">Caricamento...</span>
            </div>
        );
    }

    return (
        <div className="container mx-auto p-4 space-y-6">
            <div className="space-y-2">
                <h1 className="text-2xl font-bold">Impostazioni Organizzazione</h1>
                <p className="text-muted-foreground">
                    Gestisci le impostazioni avanzate per {currentOrganization.name}
                </p>
            </div>

            {/* Organization Information */}
            <Card>
                <CardHeader>
                    <CardTitle>Informazioni Organizzazione</CardTitle>
                    <CardDescription>
                        Dettagli di base dell'organizzazione
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <div>
                            <Label>Nome Organizzazione</Label>
                            <Input value={currentOrganization.name} disabled />
                        </div>
                        <div>
                            <Label>Slug</Label>
                            <Input value={currentOrganization.slug || ''} disabled />
                        </div>
                    </div>
                    {isSaaSMode && (
                        <div>
                            <Label>Stato Sottoscrizione</Label>
                            <Input value={currentOrganization.subscriptionStatus || 'N/A'} disabled />
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* Data Export Section */}
            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <Download className="h-5 w-5" />
                        Esportazione Dati
                    </CardTitle>
                    <CardDescription>
                        Esporta tutti i dati dell'organizzazione per il backup o la migrazione
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    {exportError && (
                        <Alert variant="destructive">
                            <AlertCircle className="h-4 w-4" />
                            <AlertTitle>Errore di Esportazione</AlertTitle>
                            <AlertDescription>{exportError}</AlertDescription>
                        </Alert>
                    )}
                    {exportSuccess && (
                        <Alert>
                            <Download className="h-4 w-4" />
                            <AlertTitle>Esportazione Avviata</AlertTitle>
                            <AlertDescription>
                                L'esportazione dei dati è stata avviata. Riceverai un'email con il link per il download quando sarà completata.
                            </AlertDescription>
                        </Alert>
                    )}
                    <div className="flex flex-col sm:flex-row gap-4">
                        <Button 
                            onClick={handleExportData} 
                            disabled={isExporting}
                            variant="outline"
                        >
                            {isExporting ? (
                                <>
                                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                    Esportazione in corso...
                                </>
                            ) : (
                                <>
                                    <Download className="mr-2 h-4 w-4" />
                                    Esporta Tutti i Dati
                                </>
                            )}
                        </Button>
                    </div>
                    <p className="text-sm text-muted-foreground">
                        L'esportazione includerà tutti gli ordini, menu, utenti e configurazioni dell'organizzazione in formato JSON.
                    </p>
                </CardContent>
            </Card>

            {/* Danger Zone */}
            <Card className="border-destructive">
                <CardHeader>
                    <CardTitle className="flex items-center gap-2 text-destructive">
                        <AlertTriangle className="h-5 w-5" />
                        Zona Pericolosa
                    </CardTitle>
                    <CardDescription>
                        Azioni irreversibili che elimineranno permanentemente i dati
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <Alert variant="destructive">
                        <AlertTriangle className="h-4 w-4" />
                        <AlertTitle>Attenzione</AlertTitle>
                        <AlertDescription>
                            L'eliminazione dell'organizzazione è un'azione permanente e irreversibile. 
                            Tutti i dati associati (ordini, menu, utenti) verranno eliminati definitivamente.
                            {isSaaSMode && " In modalità SaaS, l'organizzazione sarà programmata per l'eliminazione dopo un periodo di grazia di 30 giorni."}
                        </AlertDescription>
                    </Alert>
                    
                    <div className="flex flex-col sm:flex-row gap-4">
                        <Button 
                            variant="destructive" 
                            onClick={openDeleteDialog}
                            className="w-full sm:w-auto"
                        >
                            <Trash2 className="mr-2 h-4 w-4" />
                            Elimina Organizzazione
                        </Button>
                    </div>
                </CardContent>
            </Card>

            {/* Delete Confirmation Dialog */}
            <AlertDialog open={isDeleteDialogOpen} onOpenChange={setIsDeleteDialogOpen}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle className="flex items-center gap-2 text-destructive">
                            <AlertTriangle className="h-5 w-5" />
                            Conferma Eliminazione Organizzazione
                        </AlertDialogTitle>
                        <AlertDialogDescription className="space-y-3">
                            <p>
                                Questa azione eliminerà permanentemente l'organizzazione <strong>{currentOrganization.name}</strong> e tutti i dati associati:
                            </p>
                            <ul className="list-disc list-inside space-y-1 text-sm">
                                <li>Tutti gli ordini e lo storico delle vendite</li>
                                <li>Menu, categorie e articoli</li>
                                <li>Utenti e configurazioni</li>
                                <li>Aree, stampanti e stazioni KDS</li>
                                <li>Tutte le altre configurazioni</li>
                            </ul>
                            {isSaaSMode ? (
                                <p className="text-sm font-medium">
                                    In modalità SaaS, l'organizzazione sarà programmata per l'eliminazione dopo 30 giorni. 
                                    Durante questo periodo, l'organizzazione sarà inaccessibile ma i dati potranno essere recuperati contattando il supporto.
                                </p>
                            ) : (
                                <p className="text-sm font-medium text-destructive">
                                    In modalità self-hosted, l'eliminazione è immediata e irreversibile.
                                </p>
                            )}
                            <div className="space-y-2">
                                <Label htmlFor="confirmText">
                                    Per confermare, digita il nome dell'organizzazione: <strong>{currentOrganization.name}</strong>
                                </Label>
                                <Input
                                    id="confirmText"
                                    value={deleteConfirmationText}
                                    onChange={(e) => setDeleteConfirmationText(e.target.value)}
                                    placeholder="Nome organizzazione"
                                />
                            </div>
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    {deleteError && (
                        <Alert variant="destructive">
                            <AlertCircle className="h-4 w-4" />
                            <AlertTitle>Errore di Eliminazione</AlertTitle>
                            <AlertDescription>{deleteError}</AlertDescription>
                        </Alert>
                    )}
                    <AlertDialogFooter>
                        <AlertDialogCancel 
                            onClick={() => setIsDeleteDialogOpen(false)} 
                            disabled={isDeleting}
                        >
                            Annulla
                        </AlertDialogCancel>
                        <AlertDialogAction 
                            onClick={handleDeleteOrganization} 
                            disabled={isDeleting || deleteConfirmationText !== currentOrganization.name}
                            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                        >
                            {isDeleting ? (
                                <>
                                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                    Eliminazione...
                                </>
                            ) : (
                                <>
                                    <Trash2 className="mr-2 h-4 w-4" />
                                    Elimina Definitivamente
                                </>
                            )}
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </div>
    );
}
