import apiClient from './apiClient';
import { PaginatedResult, PrintTemplateDto, PrintTemplateUpsertDto } from '@/types';

// Assuming the backend will have query parameters for templates
export interface PrintTemplateQueryParameters {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortAscending?: boolean;
  // Add filters here later if needed, e.g., by TemplateType or DocumentType
}

const printTemplateService = {
  async getPrintTemplates(orgId: string, params: PrintTemplateQueryParameters): Promise<PaginatedResult<PrintTemplateDto>> {
    const response = await apiClient.get<PaginatedResult<PrintTemplateDto>>(`/organizations/${orgId}/print-templates`, { params });
    return response.data;
  },

  async getPrintTemplate(orgId: string, templateId: number): Promise<PrintTemplateDto> {
    const response = await apiClient.get<PrintTemplateDto>(`/organizations/${orgId}/print-templates/${templateId}`);
    return response.data;
  },

  async createPrintTemplate(orgId: string, data: PrintTemplateUpsertDto): Promise<PrintTemplateDto> {
    const response = await apiClient.post<PrintTemplateDto>(`/organizations/${orgId}/print-templates`, data);
    return response.data;
  },

  async updatePrintTemplate(orgId: string, templateId: number, data: PrintTemplateUpsertDto): Promise<PrintTemplateDto> {
    const response = await apiClient.put<PrintTemplateDto>(`/organizations/${orgId}/print-templates/${templateId}`, data);
    return response.data;
  },

  async deletePrintTemplate(orgId: string, templateId: number): Promise<void> {
    await apiClient.delete(`/organizations/${orgId}/print-templates/${templateId}`);
  },
};

export default printTemplateService;
