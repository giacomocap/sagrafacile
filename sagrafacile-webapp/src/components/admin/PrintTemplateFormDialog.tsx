"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import * as z from "zod";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { DocumentType, PrintTemplateDto, PrintTemplateUpsertDto, TemplateType } from "@/types";
import { useEffect } from "react";
import { useOrganization } from "@/contexts/OrganizationContext";
import printTemplateService from "@/services/printTemplateService";
import { toast } from "sonner";

interface PrintTemplateFormDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSave: (template: PrintTemplateDto) => void;
  template?: PrintTemplateDto | null;
}

const formSchema = z.object({
  name: z.string().min(1, "Il nome è obbligatorio."),
  templateType: z.nativeEnum(TemplateType),
  documentType: z.nativeEnum(DocumentType),
  isDefault: z.boolean(),
  htmlContent: z.string().nullable(),
  escPosHeader: z.string().nullable(),
  escPosFooter: z.string().nullable(),
});

export function PrintTemplateFormDialog({ isOpen, onClose, onSave, template }: PrintTemplateFormDialogProps) {
  const { organizations, selectedOrganizationId } = useOrganization();
  const currentOrganization = organizations.find(org => org.id === selectedOrganizationId);
  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      name: "",
      templateType: TemplateType.Receipt,
      documentType: DocumentType.EscPos,
      isDefault: false,
      htmlContent: "",
      escPosHeader: "",
      escPosFooter: "",
    },
  });

  const watchedDocumentType = form.watch("documentType");

  useEffect(() => {
    if (template) {
      form.reset({
        name: template.name,
        templateType: template.templateType,
        documentType: template.documentType,
        isDefault: template.isDefault,
        htmlContent: template.htmlContent || "",
        escPosHeader: template.escPosHeader || "",
        escPosFooter: template.escPosFooter || "",
      });
    } else {
      form.reset({
        name: "",
        templateType: TemplateType.Receipt,
        documentType: DocumentType.EscPos,
        isDefault: false,
        htmlContent: "",
        escPosHeader: "",
        escPosFooter: "",
      });
    }
  }, [template, form]);

  const onSubmit = async (values: z.infer<typeof formSchema>) => {
    if (!currentOrganization) return;

    const upsertData: PrintTemplateUpsertDto = {
      ...values,
      htmlContent: values.documentType === DocumentType.HtmlPdf ? values.htmlContent : null,
      escPosHeader: values.documentType === DocumentType.EscPos ? values.escPosHeader : null,
      escPosFooter: values.documentType === DocumentType.EscPos ? values.escPosFooter : null,
    };

    try {
      let savedTemplate: PrintTemplateDto;
      if (template) {
        savedTemplate = await printTemplateService.updatePrintTemplate(currentOrganization.id.toString(), template.id, upsertData);
        toast.success("Template aggiornato con successo!");
      } else {
        savedTemplate = await printTemplateService.createPrintTemplate(currentOrganization.id.toString(), upsertData);
        toast.success("Template creato con successo!");
      }
      onSave(savedTemplate);
      onClose();
    } catch (error: any) {
        const errorMessage = error?.response?.data || "Errore durante il salvataggio del template.";
        toast.error("Salvataggio fallito", {
            description: errorMessage,
        });
        console.error("Failed to save template", error);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[600px] max-h-[90vh] flex flex-col">
        <DialogHeader className="flex-shrink-0">
          <DialogTitle>{template ? "Modifica Template" : "Crea Nuovo Template"}</DialogTitle>
          <DialogDescription>
            Compila i dettagli del template. Clicca Salva per confermare.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col flex-1 min-h-0">
            <div className="space-y-4 overflow-y-auto flex-1 pr-2">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nome</FormLabel>
                  <FormControl>
                    <Input placeholder="Es. Scontrino A5" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="templateType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Tipo Template</FormLabel>
                    <Select onValueChange={(value) => field.onChange(Number(value))} value={field.value.toString()}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Seleziona un tipo" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value={TemplateType.Receipt.toString()}>Scontrino</SelectItem>
                        <SelectItem value={TemplateType.Comanda.toString()}>Comanda</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="documentType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Tipo Documento</FormLabel>
                    <Select onValueChange={(value) => field.onChange(Number(value))} value={field.value.toString()}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Seleziona un tipo" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value={DocumentType.EscPos.toString()}>ESC/POS</SelectItem>
                        <SelectItem value={DocumentType.HtmlPdf.toString()}>HTML/PDF</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            {watchedDocumentType === DocumentType.HtmlPdf && (
              <FormField
                control={form.control}
                name="htmlContent"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Contenuto HTML</FormLabel>
                    <FormControl>
                      <Textarea
                        placeholder="<html>...</html>"
                        className="min-h-[200px] font-mono"
                        {...field}
                        value={field.value || ''}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

            {watchedDocumentType === DocumentType.EscPos && (
              <div className="space-y-4">
                <FormField
                  control={form.control}
                  name="escPosHeader"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Header ESC/POS</FormLabel>
                      <FormControl>
                        <Textarea
                          placeholder="Testo intestazione..."
                          className="font-mono"
                          {...field}
                          value={field.value || ''}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="escPosFooter"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Footer ESC/POS</FormLabel>
                      <FormControl>
                        <Textarea
                          placeholder="Testo piè di pagina..."
                          className="font-mono"
                          {...field}
                          value={field.value || ''}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
            )}

            <FormField
              control={form.control}
              name="isDefault"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                  <div className="space-y-0.5">
                    <FormLabel>Template di Default</FormLabel>
                    <DialogDescription>
                      Imposta come template di default per questo tipo.
                    </DialogDescription>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                    />
                  </FormControl>
                </FormItem>
              )}
            />
            </div>

            <DialogFooter className="flex-shrink-0 mt-4">
              <Button type="button" variant="outline" onClick={onClose}>Annulla</Button>
              <Button type="submit">Salva</Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
