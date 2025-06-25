import { useState, useEffect } from 'react';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { useOrganization } from '@/contexts/OrganizationContext';
import printTemplateService from '@/services/printTemplateService';
import { PreviewRequestDto, TemplateType } from '@/types';
import { Loader2 } from 'lucide-react';

interface PrintTemplatePreviewDialogProps {
    isOpen: boolean;
    onClose: () => void;
    templateContent: string;
    templateType: TemplateType;
}

export function PrintTemplatePreviewDialog({ isOpen, onClose, templateContent, templateType }: PrintTemplatePreviewDialogProps) {
    const { selectedOrganizationId } = useOrganization();
    const [pdfUrl, setPdfUrl] = useState<string | null>(null);
    const [isLoading, setIsLoading] = useState(false);

    useEffect(() => {
        if (isOpen && selectedOrganizationId && templateContent) {
            const fetchPreview = async () => {
                setIsLoading(true);
                setPdfUrl(null);
                try {
                    const payload: PreviewRequestDto = {
                        htmlContent: templateContent,
                        templateType: templateType,
                    };
                    const blob = await printTemplateService.previewTemplate(selectedOrganizationId.toString(), payload);
                    const url = URL.createObjectURL(blob);
                    setPdfUrl(url);
                } catch (error) {
                    console.error("Failed to generate preview", error);
                    toast.error("Impossibile generare l'anteprima del PDF.");
                    onClose();
                } finally {
                    setIsLoading(false);
                }
            };

            fetchPreview();
        }

        return () => {
            if (pdfUrl) {
                URL.revokeObjectURL(pdfUrl);
            }
        };
    }, [isOpen, selectedOrganizationId, templateContent, templateType, onClose]);

    return (
        <Dialog open={isOpen} onOpenChange={onClose}>
            <DialogContent className="w-full max-w-[95vw] h-[95vh] flex flex-col">
                <DialogHeader className="flex-shrink-0">
                    <DialogTitle>Anteprima Template</DialogTitle>
                </DialogHeader>
                <div className="flex-1 flex items-center justify-center min-h-0">
                    {isLoading && <Loader2 className="h-8 w-8 animate-spin" />}
                    {pdfUrl && !isLoading && (
                        <iframe src={pdfUrl} className="w-full h-full" title="PDF Preview" />
                    )}
                </div>
                <DialogFooter className="flex-shrink-0">
                    <Button onClick={onClose} variant="outline">Chiudi</Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
