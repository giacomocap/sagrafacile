'use client';

import React, { useState, useEffect } from 'react';
import { invitationService } from '@/services/invitationService';
import { PendingInvitationDto } from '@/types';
import { Button } from '@/components/ui/button';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';
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
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { AlertCircle, Loader2, Trash2, Mail } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export default function PendingInvitationsSection() {
    const [pendingInvitations, setPendingInvitations] = useState<PendingInvitationDto[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [invitationToRevoke, setInvitationToRevoke] = useState<PendingInvitationDto | null>(null);
    const [isRevoking, setIsRevoking] = useState(false);
    const [revokeError, setRevokeError] = useState<string | null>(null);

    const fetchPendingInvitations = async () => {
        setIsLoading(true);
        setError(null);
        try {
            const invitations = await invitationService.getPendingInvitations();
            setPendingInvitations(invitations);
        } catch (err: unknown) {
            console.error("Recupero inviti pendenti fallito:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setError(error.response?.data?.message || error.message || 'Recupero inviti pendenti fallito.');
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        fetchPendingInvitations();
    }, []);

    const openRevokeDialog = (invitation: PendingInvitationDto) => {
        setInvitationToRevoke(invitation);
        setRevokeError(null);
    };

    const handleRevokeConfirm = async () => {
        if (!invitationToRevoke) return;

        setRevokeError(null);
        setIsRevoking(true);

        try {
            await invitationService.revokeInvitation(invitationToRevoke.id);
            setInvitationToRevoke(null);
            await fetchPendingInvitations(); // Refresh the list
        } catch (err: unknown) {
            console.error("Revoca invito fallita:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setRevokeError(error.response?.data?.message || error.message || 'Revoca invito fallita.');
        } finally {
            setIsRevoking(false);
        }
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('it-IT', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    };

    const formatRoles = (rolesString: string) => {
        return rolesString.split(',').map(role => role.trim());
    };

    return (
        <Card className="mt-6">
            <CardHeader>
                <CardTitle className="flex items-center gap-2">
                    <Mail className="h-5 w-5" />
                    Inviti Pendenti
                </CardTitle>
                <CardDescription>
                    Gestisci gli inviti inviati che non sono ancora stati accettati.
                </CardDescription>
            </CardHeader>
            <CardContent>
                {error && (
                    <Alert variant="destructive" className="mb-4">
                        <AlertCircle className="h-4 w-4" />
                        <AlertTitle>Errore</AlertTitle>
                        <AlertDescription>{error}</AlertDescription>
                    </Alert>
                )}

                {isLoading ? (
                    <div className="flex justify-center items-center py-8">
                        <Loader2 className="h-8 w-8 animate-spin" />
                        <span className="ml-2">Caricamento inviti...</span>
                    </div>
                ) : pendingInvitations.length === 0 ? (
                    <div className="text-center py-8 text-muted-foreground">
                        <Mail className="h-12 w-12 mx-auto mb-4 opacity-50" />
                        <p>Nessun invito pendente</p>
                        <p className="text-sm">Gli inviti inviati appariranno qui finché non vengono accettati o scadono.</p>
                    </div>
                ) : (
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Email</TableHead>
                                <TableHead>Ruoli</TableHead>
                                <TableHead>Inviato</TableHead>
                                <TableHead>Scade</TableHead>
                                <TableHead className="text-right">Azioni</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {pendingInvitations.map((invitation) => (
                                <TableRow key={invitation.id}>
                                    <TableCell className="font-medium">{invitation.email}</TableCell>
                                    <TableCell>
                                        <div className="flex flex-wrap gap-1">
                                            {formatRoles(invitation.roles).map(role => (
                                                <span key={role} className="px-2 py-1 text-xs font-semibold bg-secondary text-secondary-foreground rounded-full">
                                                    {role}
                                                </span>
                                            ))}
                                        </div>
                                    </TableCell>
                                    <TableCell>{formatDate(invitation.invitedAt)}</TableCell>
                                    <TableCell>{formatDate(invitation.expiryDate)}</TableCell>
                                    <TableCell className="text-right">
                                        <Button 
                                            variant="destructive" 
                                            size="icon" 
                                            onClick={() => openRevokeDialog(invitation)}
                                        >
                                            <Trash2 className="h-4 w-4" />
                                            <span className="sr-only">Revoca Invito</span>
                                        </Button>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                )}

                {/* Revoke Confirmation Dialog */}
                <AlertDialog open={!!invitationToRevoke} onOpenChange={(open) => !open && setInvitationToRevoke(null)}>
                    <AlertDialogContent>
                        <AlertDialogHeader>
                            <AlertDialogTitle>Sei sicuro?</AlertDialogTitle>
                            <AlertDialogDescription>
                                Questa azione non può essere annullata. L'invito per <span className="font-semibold">{invitationToRevoke?.email}</span> verrà revocato e il link nell'email non funzionerà più.
                            </AlertDialogDescription>
                        </AlertDialogHeader>
                        {revokeError && (
                            <Alert variant="destructive">
                                <AlertCircle className="h-4 w-4" />
                                <AlertTitle>Errore di Revoca</AlertTitle>
                                <AlertDescription>{revokeError}</AlertDescription>
                            </Alert>
                        )}
                        <AlertDialogFooter>
                            <AlertDialogCancel onClick={() => setInvitationToRevoke(null)} disabled={isRevoking}>
                                Annulla
                            </AlertDialogCancel>
                            <AlertDialogAction 
                                onClick={handleRevokeConfirm} 
                                disabled={isRevoking} 
                                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                            >
                                {isRevoking ? (
                                    <>
                                        <Loader2 className="mr-2 h-4 w-4 animate-spin" /> 
                                        Revoca...
                                    </>
                                ) : (
                                    "Sì, revoca invito"
                                )}
                            </AlertDialogAction>
                        </AlertDialogFooter>
                    </AlertDialogContent>
                </AlertDialog>
            </CardContent>
        </Card>
    );
}
