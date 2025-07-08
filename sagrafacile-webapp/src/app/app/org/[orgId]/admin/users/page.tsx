'use client';

import React, { useState, useEffect, useCallback } from 'react';
import apiClient from '@/services/apiClient';
import { useAuth } from '@/contexts/AuthContext';
import { useOrganization } from '@/contexts/OrganizationContext'; // Importa useOrganization
import { useInstance } from '@/contexts/InstanceContext';
import { invitationService } from '@/services/invitationService';
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
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
    DialogClose,
} from '@/components/ui/dialog';
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
    // AlertDialogTrigger rimosso perché non utilizzato
} from "@/components/ui/alert-dialog";
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Checkbox } from "@/components/ui/checkbox"; // Aggiunto per i ruoli
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"; // Per gli errori
import { AlertCircle, Edit, Trash2, Users as UsersIcon, Loader2, Mail } from "lucide-react"; // Icona per alert di errore, pulsanti Modifica, Elimina e Ruoli, Loader
import PendingInvitationsSection from '@/components/admin/PendingInvitationsSection';

// Definisce la struttura di un oggetto User basato su UserDto
interface User {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    emailConfirmed: boolean;
    roles: string[];
}

// Definisce la struttura per il form di registrazione
interface RegisterFormState {
    email: string;
    firstName: string;
    lastName: string;
    password: string;
    confirmPassword: string;
}

interface RegisterPayload {
    email: string;
    firstName: string;
    lastName: string;
    password: string;
    confirmPassword: string;
    organizationId?: string; // Opzionale per SuperAdmin
}

// Definisce la struttura per il form di modifica
interface EditFormState {
    id: string; // Tiene traccia dell'utente in modifica
    email: string;
    firstName: string;
    lastName: string;
}

const initialRegisterFormState: RegisterFormState = {
    email: '',
    firstName: '',
    lastName: '',
    password: '',
    confirmPassword: '',
};

export default function UserManagementPage() {
    const { isLoading: isAuthLoading } = useAuth(); // Ottiene lo stato di caricamento dell'autenticazione, utente rimosso
    // Usa il contesto dell'organizzazione
    const { selectedOrganizationId, isSuperAdminContext, isLoadingOrgs } = useOrganization(); // Ottiene lo stato di caricamento dell'organizzazione
    const { instanceInfo, loading: instanceLoading } = useInstance();
    const [users, setUsers] = useState<User[]>([]);
    const [isLoadingUsers, setIsLoadingUsers] = useState(true); // Stato rinominato per chiarezza
    const [error, setError] = useState<string | null>(null);

    // Stato per il Dialog Aggiungi Utente
    const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
    const [registerForm, setRegisterForm] = useState<RegisterFormState>(initialRegisterFormState);
    const [registerError, setRegisterError] = useState<string | null>(null);
    const [isRegistering, setIsRegistering] = useState(false);

    // Stato per il Dialog Modifica Utente
    const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
    const [editForm, setEditForm] = useState<EditFormState | null>(null); // Memorizza i dati dell'utente in modifica
    const [editError, setEditError] = useState<string | null>(null);
    const [isEditing, setIsEditing] = useState(false);

    // Stato per la Conferma Eliminazione Utente
    const [userToDelete, setUserToDelete] = useState<User | null>(null); // Memorizza l'utente da eliminare
    const [isDeleting, setIsDeleting] = useState(false);
    const [deleteError, setDeleteError] = useState<string | null>(null);

    // Stato per la Gestione Ruoli
    const [availableRoles, setAvailableRoles] = useState<string[]>([]);
    const [isRolesDialogOpen, setIsRolesDialogOpen] = useState(false);
    const [userToManageRoles, setUserToManageRoles] = useState<User | null>(null);
    const [selectedRoles, setSelectedRoles] = useState<Record<string, boolean>>({}); // { nomeRuolo: true/false }
    const [isManagingRoles, setIsManagingRoles] = useState(false);
    const [rolesError, setRolesError] = useState<string | null>(null);

    // Stato per l'Invito Utente (solo SaaS)
    const [isInviteDialogOpen, setIsInviteDialogOpen] = useState(false);
    const [inviteEmail, setInviteEmail] = useState('');
    const [inviteRoles, setInviteRoles] = useState<Record<string, boolean>>({});
    const [isInviting, setIsInviting] = useState(false);
    const [inviteError, setInviteError] = useState<string | null>(null);


    const fetchUsers = useCallback(async () => {
        setIsLoadingUsers(true); // Usa stato rinominato
        setError(null);
        try {
            const response = await apiClient.get<User[]>('/Accounts'); // Usa il nuovo endpoint GET
            setUsers(response.data);
        } catch (err: unknown) {
            console.error("Recupero utenti fallito:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setError(error.response?.data?.message || error.message || 'Recupero utenti fallito.');
        } finally {
            setIsLoadingUsers(false); // Usa stato rinominato
        }
    }, []);

    // Recupera i ruoli disponibili una volta al montaggio del componente
    useEffect(() => {
        const fetchRoles = async () => {
            try {
                const response = await apiClient.get<string[]>('/Accounts/roles');
                setAvailableRoles(response.data);
            } catch (err) {
                console.error("Recupero ruoli fallito:", err);
                // Gestire l'errore in modo appropriato, magari mostrando un messaggio di errore globale
            }
        };
        fetchRoles();
    }, []);


    useEffect(() => {
        fetchUsers();
    }, [fetchUsers]);

    const handleRegisterInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const { name, value } = e.target;
        setRegisterForm(prev => ({ ...prev, [name]: value }));
    };

    const handleRegisterSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setRegisterError(null);

        if (registerForm.password !== registerForm.confirmPassword) {
            setRegisterError("Le password non coincidono.");
            return;
        }
        if (!registerForm.email || !registerForm.firstName || !registerForm.lastName || !registerForm.password) {
             setRegisterError("Tutti i campi tranne Conferma Password sono obbligatori.");
              return;
         }

        // Validazione specifica per SuperAdmin usando il contesto
        if (isSuperAdminContext && !selectedOrganizationId) {
            setRegisterError("Il SuperAdmin deve prima selezionare un'organizzazione nell'intestazione.");
            return;
        }

        setIsRegistering(true);

        // Costruisce il payload, aggiungendo condizionalmente organizationId per i SuperAdmin
        const payload: RegisterPayload = {
            email: registerForm.email,
            firstName: registerForm.firstName,
            lastName: registerForm.lastName,
            password: registerForm.password,
            confirmPassword: registerForm.confirmPassword,
        };

        if (isSuperAdminContext && selectedOrganizationId) {
            payload.organizationId = selectedOrganizationId;
        }
        // L'OrganizationId degli OrgAdmins sarà gestito dal backend in base al loro token

        try {
            await apiClient.post('/Accounts/register', payload);
            setIsAddDialogOpen(false); // Chiude il dialog in caso di successo
            setRegisterForm(initialRegisterFormState); // Resetta il form
            await fetchUsers(); // Aggiorna la lista degli utenti
        } catch (err: unknown) {
            console.error("Registrazione fallita:", err);
            const error = err as { response?: { data?: any }, message?: string };
            // Gestisce il potenziale array di errori dallo stato del modello
             const errorData = error.response?.data;
             let errorMessage = "Registrazione fallita.";
             if (typeof errorData === 'object' && errorData !== null) {
                 const modelErrors = Object.values(errorData).flat(); // Appiattisce i potenziali array di errori
                 if (modelErrors.length > 0 && typeof modelErrors[0] === 'string') {
                     errorMessage = modelErrors.join(' ');
                 } else if (errorData.message) {
                     errorMessage = errorData.message;
                 }
             } else if (typeof errorData === 'string') {
                 errorMessage = errorData;
             }
            setRegisterError(errorMessage);
        } finally {
            setIsRegistering(false);
        }
    };

    const handleEditInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        if (!editForm) return;
        const { name, value } = e.target;
        setEditForm(prev => prev ? { ...prev, [name]: value } : null);
    };

    const openEditDialog = (user: User) => {
        setEditForm({
            id: user.id,
            email: user.email,
            firstName: user.firstName,
            lastName: user.lastName,
        });
        setEditError(null); // Pulisce gli errori precedenti
        setIsEditDialogOpen(true);
    };

    const handleEditSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!editForm) return;

        setEditError(null);
        setIsEditing(true);

        try {
            // Usa l'endpoint PUT per l'aggiornamento
            await apiClient.put(`/Accounts/${editForm.id}`, {
                // Il corpo deve corrispondere a UpdateUserDto
                firstName: editForm.firstName,
                lastName: editForm.lastName,
            });
            setIsEditDialogOpen(false); // Chiude il dialog in caso di successo
            await fetchUsers(); // Aggiorna la lista
        } catch (err: unknown) {
            console.error("Aggiornamento utente fallito:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setEditError(error.response?.data?.message || error.message || 'Aggiornamento utente fallito.');
        } finally {
            setIsEditing(false);
        }
    };

    const openDeleteDialog = (user: User) => {
        setUserToDelete(user);
        setDeleteError(null); // Pulisce errori precedenti
    };
    const handleDeleteConfirm = async () => {
        if (!userToDelete) return;

        setDeleteError(null);
        setIsDeleting(true);

        try {
            await apiClient.delete(`/Accounts/${userToDelete.id}`);
            setUserToDelete(null); // Chiude il dialog di conferma
            await fetchUsers(); // Aggiorna la lista
        } catch (err: unknown) {
            console.error("Eliminazione utente fallita:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setDeleteError(error.response?.data?.message || error.message || 'Eliminazione utente fallita.');
        } finally {
            setIsDeleting(false);
        }
    };

    const openRolesDialog = (user: User) => {
        setUserToManageRoles(user);
        // Inizializza lo stato delle checkbox in base ai ruoli attuali dell'utente
        const currentRolesState = availableRoles.reduce((acc, role) => {
            acc[role] = user.roles.includes(role);
            return acc;
        }, {} as Record<string, boolean>);
        setSelectedRoles(currentRolesState);
        setRolesError(null);
        setIsRolesDialogOpen(true);
    };

     const handleRoleCheckboxChange = (roleName: string, checked: boolean) => {
        setSelectedRoles(prev => ({ ...prev, [roleName]: checked }));
    };

    const handleRolesSubmit = async () => {
        if (!userToManageRoles) return;

        setRolesError(null);
        setIsManagingRoles(true);

        const rolesToAssign = Object.entries(selectedRoles)
            .filter(([, isSelected]) => isSelected)
            .map(([roleName]) => roleName);

        try {
            await apiClient.post(`/Accounts/assign-roles`, {
                userId: userToManageRoles.id,
                roles: rolesToAssign,
            });
            setIsRolesDialogOpen(false); // Chiude il dialog
            await fetchUsers(); // Aggiorna per visualizzare i nuovi ruoli
        } catch (err: unknown) {
            console.error("Assegnazione ruoli fallita:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setRolesError(error.response?.data?.message || error.message || 'Assegnazione ruoli fallita.');
        } finally {
            setIsManagingRoles(false);
        }
    };

    // Gestione Invito Utente (solo SaaS)
    const openInviteDialog = () => {
        setInviteEmail('');
        setInviteRoles({});
        setInviteError(null);
        setIsInviteDialogOpen(true);
    };

    const handleInviteRoleCheckboxChange = (roleName: string, checked: boolean) => {
        setInviteRoles(prev => ({ ...prev, [roleName]: checked }));
    };

    const handleInviteSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setInviteError(null);

        if (!inviteEmail) {
            setInviteError("L'email è obbligatoria.");
            return;
        }

        const selectedRolesList = Object.entries(inviteRoles)
            .filter(([, isSelected]) => isSelected)
            .map(([roleName]) => roleName);

        if (selectedRolesList.length === 0) {
            setInviteError("Seleziona almeno un ruolo.");
            return;
        }

        setIsInviting(true);

        try {
            await invitationService.inviteUser({
                email: inviteEmail,
                roles: selectedRolesList,
            });
            setIsInviteDialogOpen(false);
            // Non aggiorniamo la lista utenti perché l'invitato non è ancora un utente registrato
        } catch (err: unknown) {
            console.error("Invito fallito:", err);
            const error = err as { response?: { data?: { message?: string } }, message?: string };
            setInviteError(error.response?.data?.message || error.message || 'Invito fallito.');
        } finally {
            setIsInviting(false);
        }
    };

    // Renderizza un loader se i dati essenziali non sono ancora pronti
    if (isAuthLoading || isLoadingOrgs || instanceLoading) {
        return <div className="flex justify-center items-center h-full"><Loader2 className="h-8 w-8 animate-spin" /> <span className="ml-2">Caricamento...</span></div>;
    }

    const isSaaSMode = instanceInfo?.mode === 'saas';

    return (
        <div className="container mx-auto p-4">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-2xl font-bold">Gestione Utenti</h1>
                <div className="flex gap-2">
                    {isSaaSMode && (
                        <Dialog open={isInviteDialogOpen} onOpenChange={setIsInviteDialogOpen}>
                            <DialogTrigger asChild>
                                <Button variant="outline" onClick={openInviteDialog}>
                                    <Mail className="mr-2 h-4 w-4" />
                                    Invita Utente
                                </Button>
                            </DialogTrigger>
                            <DialogContent className="sm:max-w-[425px] overflow-y-scroll max-h-screen">
                                <DialogHeader>
                                    <DialogTitle>Invita Nuovo Utente</DialogTitle>
                                    <DialogDescription>
                                        Invia un invito via email. L'utente riceverà un link per completare la registrazione.
                                    </DialogDescription>
                                </DialogHeader>
                                <form onSubmit={handleInviteSubmit}>
                                    <div className="grid gap-4 py-4">
                                        {inviteError && (
                                            <Alert variant="destructive">
                                                <AlertCircle className="h-4 w-4" />
                                                <AlertTitle>Errore di Invito</AlertTitle>
                                                <AlertDescription>{inviteError}</AlertDescription>
                                            </Alert>
                                        )}
                                        <div className="grid grid-cols-4 items-center gap-4">
                                            <Label htmlFor="inviteEmail" className="text-right">Email</Label>
                                            <Input 
                                                id="inviteEmail" 
                                                type="email" 
                                                value={inviteEmail} 
                                                onChange={(e) => setInviteEmail(e.target.value)} 
                                                className="col-span-3" 
                                                required 
                                            />
                                        </div>
                                        <div className="grid grid-cols-4 items-start gap-4">
                                            <Label className="text-right pt-2">Ruoli</Label>
                                            <div className="col-span-3 space-y-2">
                                                {availableRoles.map(role => (
                                                    <div key={role} className="flex items-center space-x-2">
                                                        <Checkbox
                                                            id={`invite-role-${role}`}
                                                            checked={inviteRoles[role] || false}
                                                            onCheckedChange={(checked) => handleInviteRoleCheckboxChange(role, checked as boolean)}
                                                        />
                                                        <Label htmlFor={`invite-role-${role}`} className="font-normal">{role}</Label>
                                                    </div>
                                                ))}
                                            </div>
                                        </div>
                                    </div>
                                    <DialogFooter>
                                        <DialogClose asChild>
                                            <Button type="button" variant="secondary" onClick={() => setIsInviteDialogOpen(false)}>Annulla</Button>
                                        </DialogClose>
                                        <Button type="submit" disabled={isInviting}>
                                            {isInviting ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Invio...</> : "Invia Invito"}
                                        </Button>
                                    </DialogFooter>
                                </form>
                            </DialogContent>
                        </Dialog>
                    )}
                    <Dialog open={isAddDialogOpen} onOpenChange={setIsAddDialogOpen}>
                        <DialogTrigger asChild>
                            <Button onClick={() => setRegisterError(null)}>Aggiungi Utente</Button>
                        </DialogTrigger>
                        <DialogContent className="sm:max-w-[425px] overflow-y-scroll max-h-screen">
                        <DialogHeader>
                            <DialogTitle>Aggiungi Nuovo Utente</DialogTitle>
                            <DialogDescription>
                                Compila i dettagli per registrare un nuovo utente. Verrà inviata un'email di conferma.
                            </DialogDescription>
                        </DialogHeader>
                        <form onSubmit={handleRegisterSubmit}>
                            <div className="grid gap-4 py-4">
                                {registerError && (
                                    <Alert variant="destructive">
                                        <AlertCircle className="h-4 w-4" />
                                        <AlertTitle>Errore di Registrazione</AlertTitle>
                                        <AlertDescription>{registerError}</AlertDescription>
                                    </Alert>
                                )}
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="email" className="text-right">Email</Label>
                                    <Input id="email" name="email" type="email" value={registerForm.email} onChange={handleRegisterInputChange} className="col-span-3" required />
                                </div>
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="firstName" className="text-right">Nome</Label>
                                    <Input id="firstName" name="firstName" value={registerForm.firstName} onChange={handleRegisterInputChange} className="col-span-3" required />
                                </div>
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="lastName" className="text-right">Cognome</Label>
                                    <Input id="lastName" name="lastName" value={registerForm.lastName} onChange={handleRegisterInputChange} className="col-span-3" required />
                                </div>
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="password" className="text-right">Password</Label>
                                    <Input id="password" name="password" type="password" value={registerForm.password} onChange={handleRegisterInputChange} className="col-span-3" required />
                                </div>
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="confirmPassword" className="text-right">Conferma Password</Label>
                                    <Input id="confirmPassword" name="confirmPassword" type="password" value={registerForm.confirmPassword} onChange={handleRegisterInputChange} className="col-span-3" required />
                                </div>
                            </div>
                            <DialogFooter>
                                <DialogClose asChild>
                                    <Button type="button" variant="secondary" onClick={() => setIsAddDialogOpen(false)}>Annulla</Button>
                                </DialogClose>
                                <Button type="submit" disabled={isRegistering}>
                                    {isRegistering ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> In Registrazione...</> : "Registra Utente"}
                                </Button>
                            </DialogFooter>
                        </form>
                    </DialogContent>
                </Dialog>
                </div>
            </div>

            {error && (
                <Alert variant="destructive" className="mb-4">
                    <AlertCircle className="h-4 w-4" />
                    <AlertTitle>Errore</AlertTitle>
                    <AlertDescription>{error}</AlertDescription>
                </Alert>
            )}

            {isLoadingUsers ? (
                <div className="flex justify-center items-center">
                    <Loader2 className="h-8 w-8 animate-spin" />
                    <span className="ml-2">Caricamento utenti...</span>
                </div>
            ) : (
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Nome</TableHead>
                            <TableHead>Email</TableHead>
                            <TableHead>Email Verificata</TableHead>
                            <TableHead>Ruoli</TableHead>
                            <TableHead className="text-right">Azioni</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {users.map((user) => (
                            <TableRow key={user.id}>
                                <TableCell>{user.firstName} {user.lastName}</TableCell>
                                <TableCell>{user.email}</TableCell>
                                <TableCell>{user.emailConfirmed ? "Sì" : "No"}</TableCell>
                                <TableCell>
                                     <div className="flex flex-wrap gap-1">
                                        {user.roles.map(role => (
                                            <span key={role} className="px-2 py-1 text-xs font-semibold bg-secondary text-secondary-foreground rounded-full">
                                                {role}
                                            </span>
                                        ))}
                                    </div>
                                </TableCell>
                                <TableCell className="text-right">
                                    <div className="flex justify-end gap-2">
                                        <Button variant="outline" size="icon" onClick={() => openRolesDialog(user)}>
                                            <UsersIcon className="h-4 w-4" />
                                            <span className="sr-only">Gestisci Ruoli</span>
                                        </Button>
                                        <Button variant="outline" size="icon" onClick={() => openEditDialog(user)}>
                                            <Edit className="h-4 w-4" />
                                            <span className="sr-only">Modifica Utente</span>
                                        </Button>
                                        <Button variant="destructive" size="icon" onClick={() => openDeleteDialog(user)}>
                                            <Trash2 className="h-4 w-4" />
                                            <span className="sr-only">Elimina Utente</span>
                                        </Button>
                                    </div>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            )}

            {/* Edit User Dialog */}
            {editForm && (
                <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
                    <DialogContent className="sm:max-w-[425px] overflow-y-scroll max-h-screen">
                        <DialogHeader>
                            <DialogTitle>Modifica Utente</DialogTitle>
                            <DialogDescription>
                                Aggiorna i dettagli dell'utente. Le modifiche qui non influenzeranno la password.
                            </DialogDescription>
                        </DialogHeader>
                        <form onSubmit={handleEditSubmit}>
                            <div className="grid gap-4 py-4">
                                {editError && (
                                    <Alert variant="destructive">
                                        <AlertCircle className="h-4 w-4" />
                                        <AlertTitle>Errore di Modifica</AlertTitle>
                                        <AlertDescription>{editError}</AlertDescription>
                                    </Alert>
                                )}
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label className="text-right">Email</Label>
                                    <Input value={editForm.email} className="col-span-3" disabled />
                                </div>
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="editFirstName" className="text-right">Nome</Label>
                                    <Input id="editFirstName" name="firstName" value={editForm.firstName} onChange={handleEditInputChange} className="col-span-3" required />
                                </div>
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="editLastName" className="text-right">Cognome</Label>
                                    <Input id="editLastName" name="lastName" value={editForm.lastName} onChange={handleEditInputChange} className="col-span-3" required />
                                </div>
                            </div>
                            <DialogFooter>
                                <DialogClose asChild>
                                    <Button type="button" variant="secondary" onClick={() => setIsEditDialogOpen(false)}>Annulla</Button>
                                </DialogClose>
                                <Button type="submit" disabled={isEditing}>
                                    {isEditing ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Salvataggio...</> : "Salva Modifiche"}
                                </Button>
                            </DialogFooter>
                        </form>
                    </DialogContent>
                </Dialog>
            )}

            {/* Delete Confirmation Dialog */}
            <AlertDialog open={!!userToDelete} onOpenChange={(open) => !open && setUserToDelete(null)}>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Sei sicuro?</AlertDialogTitle>
                        <AlertDialogDescription>
                            Questa azione non può essere annullata. Questo eliminerà permanentemente l'utente <span className="font-semibold">{userToDelete?.firstName} {userToDelete?.lastName} ({userToDelete?.email})</span>.
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
                        <AlertDialogCancel onClick={() => setUserToDelete(null)} disabled={isDeleting}>Annulla</AlertDialogCancel>
                        <AlertDialogAction onClick={handleDeleteConfirm} disabled={isDeleting} className="bg-destructive text-destructive-foreground hover:bg-destructive/90">
                            {isDeleting ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Eliminazione...</> : "Sì, elimina utente"}
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>

             {/* Manage Roles Dialog */}
            {userToManageRoles && (
                <Dialog open={isRolesDialogOpen} onOpenChange={setIsRolesDialogOpen}>
                    <DialogContent className="overflow-y-scroll max-h-screen">
                        <DialogHeader>
                            <DialogTitle>Gestisci Ruoli per {userToManageRoles.firstName}</DialogTitle>
                            <DialogDescription>
                                Assegna o rimuovi ruoli per questo utente. Le modifiche avranno effetto immediato.
                            </DialogDescription>
                        </DialogHeader>
                        <div className="space-y-4 py-4">
                             {rolesError && (
                                <Alert variant="destructive">
                                    <AlertCircle className="h-4 w-4" />
                                    <AlertTitle>Errore Assegnazione Ruoli</AlertTitle>
                                    <AlertDescription>{rolesError}</AlertDescription>
                                </Alert>
                            )}
                            <div className="space-y-2">
                                {availableRoles.map(role => (
                                    <div key={role} className="flex items-center space-x-2">
                                        <Checkbox
                                            id={`role-${role}`}
                                            checked={selectedRoles[role] || false}
                                            onCheckedChange={(checked) => handleRoleCheckboxChange(role, checked as boolean)}
                                        />
                                        <Label htmlFor={`role-${role}`} className="font-normal">{role}</Label>
                                    </div>
                                ))}
                            </div>
                        </div>
                        <DialogFooter>
                            <DialogClose asChild>
                                <Button variant="secondary" onClick={() => setIsRolesDialogOpen(false)}>Annulla</Button>
                            </DialogClose>
                            <Button onClick={handleRolesSubmit} disabled={isManagingRoles}>
                                 {isManagingRoles ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Salvataggio Ruoli...</> : "Salva Ruoli"}
                            </Button>
                        </DialogFooter>
                    </DialogContent>
                </Dialog>
            )}

            {/* Pending Invitations Section - Only show in SaaS mode */}
            {isSaaSMode && <PendingInvitationsSection />}
        </div>
    );
}
