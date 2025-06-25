"use client";

import { useState, useEffect, useCallback, useMemo } from 'react';
import { MoreHorizontal, Eye, History } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";

import { DocumentType, PaginatedResult, PrintTemplateDto, TemplateType } from '@/types';
import { useOrganization } from '@/contexts/OrganizationContext';
import printTemplateService, { PrintTemplateQueryParameters } from '@/services/printTemplateService';
import PaginatedTable from '@/components/common/PaginatedTable';
import { PrintTemplateFormDialog } from '@/components/admin/PrintTemplateFormDialog';
import { PrintTemplatePreviewDialog } from '@/components/admin/PrintTemplatePreviewDialog';

const renderTemplateType = (type: TemplateType) => {
    switch (type) {
        case TemplateType.Receipt:
            return "Scontrino";
        case TemplateType.Comanda:
            return "Comanda";
        default:
            return "Sconosciuto";
    }
};

const renderDocumentType = (type: DocumentType) => {
    switch (type) {
        case DocumentType.EscPos:
            return "ESC/POS";
        case DocumentType.HtmlPdf:
            return "HTML/PDF";
        default:
            return "Sconosciuto";
    }
};

export default function PrintTemplatesPage() {
    const { organizations, selectedOrganizationId } = useOrganization();
    const currentOrganization = organizations.find(org => org.id === selectedOrganizationId);
    const [paginatedTemplates, setPaginatedTemplates] = useState<PaginatedResult<PrintTemplateDto> | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [isFormOpen, setIsFormOpen] = useState(false);
    const [selectedTemplate, setSelectedTemplate] = useState<PrintTemplateDto | null>(null);
    const [isPreviewOpen, setIsPreviewOpen] = useState(false);
    const [previewContent, setPreviewContent] = useState<{ content: string; type: TemplateType } | null>(null);

    const [queryParams, setQueryParams] = useState<PrintTemplateQueryParameters>({
        page: 1,
        pageSize: 10,
        sortBy: 'name',
        sortAscending: true,
    });

    const fetchTemplates = useCallback(async () => {
        if (!currentOrganization) {
            setPaginatedTemplates(null);
            return;
        };
        setIsLoading(true);
        setError(null);
        try {
            const result = await printTemplateService.getPrintTemplates(currentOrganization.id.toString(), queryParams);
            setPaginatedTemplates(result);
        } catch (error) {
            toast.error("Errore nel caricamento dei template.");
            setError("Impossibile caricare i template.");
            console.error("Failed to fetch templates", error);
        } finally {
            setIsLoading(false);
        }
    }, [currentOrganization, queryParams]);

    useEffect(() => {
        fetchTemplates();
    }, [fetchTemplates]);

    const handleOpenForm = (template?: PrintTemplateDto) => {
        setSelectedTemplate(template || null);
        setIsFormOpen(true);
    };

    const handleCloseForm = () => {
        setIsFormOpen(false);
        setSelectedTemplate(null);
    };

    const handlePreview = (template: PrintTemplateDto) => {
        if (template.documentType === DocumentType.HtmlPdf && template.htmlContent) {
            setPreviewContent({ content: template.htmlContent, type: template.templateType });
            setIsPreviewOpen(true);
        } else {
            toast.info("L'anteprima è disponibile solo per i template di tipo HTML/PDF con contenuto.");
        }
    };

    const handleRestoreDefaults = async () => {
        if (!currentOrganization) return;
        if (!confirm("Sei sicuro di voler ripristinare i template HTML di default? Questa azione sovrascriverà i template di default esistenti.")) return;

        try {
            await printTemplateService.restoreDefaultTemplates(currentOrganization.id.toString());
            toast.success("Template di default ripristinati con successo!");
            fetchTemplates();
        } catch (error) {
            toast.error("Errore durante il ripristino dei template.");
            console.error("Failed to restore default templates", error);
        }
    };

    const handleSave = () => {
        fetchTemplates(); // Re-fetch data after save
    };

    const handleDelete = async (templateId: number) => {
        if (!currentOrganization) return;
        if (!confirm("Sei sicuro di voler eliminare questo template?")) return;

        try {
            await printTemplateService.deletePrintTemplate(currentOrganization.id.toString(), templateId);
            toast.success("Template eliminato con successo!");
            fetchTemplates();
        } catch (error) {
            toast.error("Errore durante l'eliminazione del template.");
            console.error("Failed to delete template", error);
        }
    };

    const columns = useMemo(() => [
        { key: 'name', label: 'Nome', sortable: true },
        { key: 'templateType', label: 'Tipo Template', sortable: true },
        { key: 'documentType', label: 'Tipo Documento', sortable: true },
        { key: 'isDefault', label: 'Default', sortable: true },
    ], []);

    const renderCell = (template: PrintTemplateDto, columnKey: string) => {
        switch (columnKey) {
            case 'name':
                return <span className="font-medium">{template.name}</span>;
            case 'templateType':
                return renderTemplateType(template.templateType);
            case 'documentType':
                return renderDocumentType(template.documentType);
            case 'isDefault':
                return template.isDefault ? <Badge>Sì</Badge> : <Badge variant="secondary">No</Badge>;
            default:
                return null;
        }
    };

    const renderActions = (template: PrintTemplateDto) => (
        <DropdownMenu>
            <DropdownMenuTrigger asChild>
                <Button variant="ghost" className="h-8 w-8 p-0">
                    <span className="sr-only">Apri menu</span>
                    <MoreHorizontal className="h-4 w-4" />
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
                <DropdownMenuLabel>Azioni</DropdownMenuLabel>
                <DropdownMenuItem onClick={() => handleOpenForm(template)}>
                    Modifica
                </DropdownMenuItem>
                {template.documentType === DocumentType.HtmlPdf && (
                    <DropdownMenuItem onClick={() => handlePreview(template)}>
                        <Eye className="mr-2 h-4 w-4" /> Anteprima
                    </DropdownMenuItem>
                )}
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={() => handleDelete(template.id)} className="text-red-600">
                    Elimina
                </DropdownMenuItem>
            </DropdownMenuContent>
        </DropdownMenu>
    );

    return (
        <div className="container mx-auto py-10">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-3xl font-bold">Gestione Template di Stampa</h1>
                <div className="flex gap-2">
                    <Button onClick={handleRestoreDefaults} variant="outline">
                        <History className="mr-2 h-4 w-4" /> Ripristina Default
                    </Button>
                    <Button onClick={() => handleOpenForm()}>Crea Template</Button>
                </div>
            </div>

            <PaginatedTable
                storageKey={`print_templates_${currentOrganization?.id}`}
                columns={columns}
                paginatedData={paginatedTemplates}
                isLoading={isLoading}
                error={error}
                queryParams={queryParams}
                onQueryChange={(newParams) => setQueryParams(prev => ({ ...prev, ...newParams }))}
                renderCell={renderCell}
                renderActions={renderActions}
                itemKey={(template) => template.id.toString()}
            />

            <PrintTemplateFormDialog
                isOpen={isFormOpen}
                onClose={handleCloseForm}
                onSave={handleSave}
                template={selectedTemplate}
            />

            {previewContent && (
                <PrintTemplatePreviewDialog
                    isOpen={isPreviewOpen}
                    onClose={() => setIsPreviewOpen(false)}
                    templateContent={previewContent.content}
                    templateType={previewContent.type}
                />
            )}
        </div>
    );
}
