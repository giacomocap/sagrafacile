"use client";

import { useEffect, useState, useCallback } from 'react'; // Added useCallback
import apiClient from '@/services/apiClient'; // Removed apiBaseUrl from import
import { useParams } from 'next/navigation';
// import Image from 'next/image'; // Removed Image import
import { AdMediaItemDto, AdAreaAssignmentDto } from '@/types';
import { getMediaUrl as getSharedMediaUrl } from '@/lib/imageUtils'; // Import shared getMediaUrl
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { PlusCircle, Edit, Trash2 } from 'lucide-react';
import AdUpsertDialog from '@/components/admin/AdUpsertDialog';
import AdAssignmentUpsertDialog from '@/components/admin/AdAssignmentUpsertDialog';
import AdminAreaSelector from '@/components/shared/AdminAreaSelector';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from "@/components/ui/table";
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';

export default function AdManagementPage() {
    const params = useParams();
    const orgId = params.orgId as string;
    const [ads, setAds] = useState<AdMediaItemDto[]>([]);
    const [assignments, setAssignments] = useState<AdAreaAssignmentDto[]>([]);
    const [loading, setLoading] = useState(false);
    const [loadingAssignments, setLoadingAssignments] = useState(false);
    const [isAdDialogOpen, setIsAdDialogOpen] = useState(false);
    const [isAssignmentDialogOpen, setIsAssignmentDialogOpen] = useState(false);
    const [adToEdit, setAdToEdit] = useState<AdMediaItemDto | null>(null);
    const [assignmentToEdit, setAssignmentToEdit] = useState<AdAreaAssignmentDto | null>(null);
    const [selectedAreaId, setSelectedAreaId] = useState<string | undefined>(undefined);

    const fetchAds = useCallback(() => { // Wrapped in useCallback
        if (orgId) {
            setLoading(true);
            setAds([]);
            apiClient.get<AdMediaItemDto[]>(`/admin/organizations/${orgId}/ads`)
                .then(response => {
                    setAds(response.data);
                })
                .catch(error => {
                    console.error("Errore nel recupero delle pubblicità:", error);
                    toast.error("Errore nel caricamento delle pubblicità");
                })
                .finally(() => {
                    setLoading(false);
                });
        } else {
            setAds([]);
        }
    }, [orgId]); // Dependency for useCallback

    const fetchAssignments = useCallback(() => { // Wrapped in useCallback
        if (selectedAreaId) {
            setLoadingAssignments(true);
            setAssignments([]);
            apiClient.get<AdAreaAssignmentDto[]>(`/admin/areas/${selectedAreaId}/ad-assignments`)
                .then(response => {
                    setAssignments(response.data);
                })
                .catch(error => {
                    console.error("Errore nel recupero delle assegnazioni:", error);
                    toast.error("Errore nel caricamento delle assegnazioni");
                })
                .finally(() => {
                    setLoadingAssignments(false);
                });
        } else {
            setAssignments([]);
        }
    }, [selectedAreaId]); // Dependency for useCallback

    useEffect(() => {
        fetchAds();
    }, [fetchAds]); // Added fetchAds to dependency array

    useEffect(() => {
        fetchAssignments();
    }, [fetchAssignments]); // Added fetchAssignments to dependency array

    const handleAddAdClick = () => {
        setAdToEdit(null);
        setIsAdDialogOpen(true);
    };

    const handleEditAdClick = (ad: AdMediaItemDto) => {
        setAdToEdit(ad);
        setIsAdDialogOpen(true);
    };

    const handleDeleteAdClick = async (adId: string) => {
        if (confirm("Sei sicuro di voler eliminare questo media? L'azione è irreversibile e lo rimuoverà da tutte le aree a cui è assegnato.")) {
            try {
                await apiClient.delete(`/admin/ads/${adId}`);
                toast.success("Media eliminato con successo.");
                fetchAds();
                fetchAssignments();
            } catch (error) {
                console.error("Errore durante l'eliminazione del media:", error);
                toast.error("Errore nell'eliminazione del media.");
            }
        }
    };

    const handleSaveAdSuccess = () => {
        fetchAds();
        setIsAdDialogOpen(false);
    };

    const handleSaveAssignmentSuccess = () => {
        fetchAssignments();
        setIsAssignmentDialogOpen(false);
    };

    const handleAddAssignmentClick = () => {
        setAssignmentToEdit(null);
        setIsAssignmentDialogOpen(true);
    };

    const handleEditAssignmentClick = (assignment: AdAreaAssignmentDto) => {
        setAssignmentToEdit(assignment);
        setIsAssignmentDialogOpen(true);
    };

    const handleDeleteAssignmentClick = async (assignmentId: string) => {
        if (confirm("Sei sicuro di voler rimuovere questa assegnazione?")) {
            try {
                await apiClient.delete(`/admin/ad-assignments/${assignmentId}`);
                toast.success("Assegnazione rimossa con successo.");
                fetchAssignments();
            } catch (error) {
                console.error("Errore durante la rimozione dell'assegnazione:", error);
                toast.error("Errore nella rimozione dell'assegnazione.");
            }
        }
    };

    // Removed local getMediaUrl, will use getSharedMediaUrl from @/lib/imageUtils for video tags

    return (
        <div className="space-y-6">
            <h1 className="text-2xl font-bold">Gestione Pubblicità</h1>

            <Card>
                <CardHeader className="flex flex-row items-center justify-between">
                    <div>
                        <CardTitle>Libreria Media</CardTitle>
                        <CardDescription>
                            Aggiungi, modifica o elimina i media per la tua organizzazione.
                        </CardDescription>
                    </div>
                    <Button onClick={handleAddAdClick}>
                        <PlusCircle className="mr-2 h-4 w-4" /> Aggiungi Media
                    </Button>
                </CardHeader>
                <CardContent>
                    {loading ? (
                        <p>Caricamento...</p>
                    ) : ads.length === 0 ? (
                        <p>Nessun media trovato per questa organizzazione.</p>
                    ) : (
                        <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Anteprima</TableHead>
                                <TableHead>Nome</TableHead>
                                <TableHead>Tipo</TableHead>
                                <TableHead>Azioni</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {ads.map((ad) => (
                                <TableRow key={ad.id}>
                                    <TableCell>
                                        {ad.mediaType === 'Image' ? (
                                            <img
                                                src={getSharedMediaUrl(ad.filePath)}
                                                alt="Anteprima pubblicità"
                                                width={100} // Example fixed width
                                                height={64} // h-16 is 64px
                                                className="rounded"
                                                style={{ objectFit: 'cover' }}
                                            />
                                        ) : (
                                            <video src={getSharedMediaUrl(ad.filePath)} className="h-16 w-auto object-cover rounded" muted playsInline />
                                        )}
                                    </TableCell>
                                    <TableCell>{ad.name}</TableCell>
                                            <TableCell>{ad.mediaType}</TableCell>
                                            <TableCell className="space-x-2">
                                                <Button variant="outline" size="icon" onClick={() => handleEditAdClick(ad)}>
                                                    <Edit className="h-4 w-4" />
                                                </Button>
                                                <Button variant="destructive" size="icon" onClick={() => handleDeleteAdClick(ad.id)}>
                                                    <Trash2 className="h-4 w-4" />
                                                </Button>
                                            </TableCell>
                                        </TableRow>
                            ))}
                        </TableBody>
                        </Table>
                    )}
                </CardContent>
            </Card>

            <AdminAreaSelector
                selectedAreaId={selectedAreaId}
                onAreaChange={setSelectedAreaId}
                title="Seleziona Area per Assegnazioni"
                description="Scegli un'area per gestire quali pubblicità mostrare, il loro ordine e la durata."
            />

            {selectedAreaId && (
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                        <div>
                            <CardTitle>Assegnazioni per l'Area Selezionata</CardTitle>
                            <CardDescription>
                                Gestisci le pubblicità attive per questa area.
                            </CardDescription>
                        </div>
                        <Button onClick={handleAddAssignmentClick}>
                            <PlusCircle className="mr-2 h-4 w-4" /> Assegna Media
                        </Button>
                    </CardHeader>
                    <CardContent>
                        {loadingAssignments ? (
                            <p>Caricamento assegnazioni...</p>
                        ) : assignments.length === 0 ? (
                            <p>Nessuna pubblicità assegnata a quest'area.</p>
                        ) : (
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>Anteprima</TableHead>
                                        <TableHead>Nome Media</TableHead>
                                        <TableHead>Ordine</TableHead>
                                        <TableHead>Durata (s)</TableHead>
                                        <TableHead>Stato</TableHead>
                                        <TableHead>Azioni</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {assignments.map((assignment) => (
                                        <TableRow key={assignment.id}>
                                            <TableCell>
                                                {assignment.adMediaItem.mediaType === 'Image' ? (
                                                    <img
                                                        src={getSharedMediaUrl(assignment.adMediaItem.filePath)}
                                                        alt="Anteprima pubblicità"
                                                        width={100} // Example fixed width
                                                        height={64} // h-16 is 64px
                                                        className="rounded"
                                                        style={{ objectFit: 'cover' }}
                                                    />
                                                ) : (
                                                    <video src={getSharedMediaUrl(assignment.adMediaItem.filePath)} className="h-16 w-auto object-cover rounded" muted playsInline />
                                                )}
                                            </TableCell>
                                            <TableCell>{assignment.adMediaItem.name}</TableCell>
                                            <TableCell>{assignment.displayOrder}</TableCell>
                                            <TableCell>{assignment.adMediaItem.mediaType === 'Image' ? assignment.durationSeconds : 'N/D'}</TableCell>
                                            <TableCell>
                                                <Badge variant={assignment.isActive ? 'default' : 'secondary'}>
                                                    {assignment.isActive ? 'Attiva' : 'Inattiva'}
                                                </Badge>
                                            </TableCell>
                                            <TableCell className="space-x-2">
                                                <Button variant="outline" size="icon" onClick={() => handleEditAssignmentClick(assignment)}>
                                                    <Edit className="h-4 w-4" />
                                                </Button>
                                                <Button variant="destructive" size="icon" onClick={() => handleDeleteAssignmentClick(assignment.id)}>
                                                    <Trash2 className="h-4 w-4" />
                                                </Button>
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        )}
                    </CardContent>
                </Card>
            )}

            {orgId && (
                <AdUpsertDialog
                    isOpen={isAdDialogOpen}
                    onOpenChange={setIsAdDialogOpen}
                    adToEdit={adToEdit}
                    onSaveSuccess={handleSaveAdSuccess}
                    organizationId={parseInt(orgId, 10)}
                />
            )}

            {selectedAreaId && (
                <AdAssignmentUpsertDialog
                    isOpen={isAssignmentDialogOpen}
                    onOpenChange={setIsAssignmentDialogOpen}
                    assignmentToEdit={assignmentToEdit}
                    onSaveSuccess={handleSaveAssignmentSuccess}
                    areaId={parseInt(selectedAreaId, 10)}
                    availableAds={ads}
                />
            )}
        </div>
    );
}
