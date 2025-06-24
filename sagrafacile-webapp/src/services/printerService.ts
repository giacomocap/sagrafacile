import apiClient from './apiClient';
import { PrinterDto, PrinterUpsertDto, PrintJobDto, PaginatedResult, PrintJobQueryParameters } from '@/types';

const printerService = {
  getPrinters: async (): Promise<PrinterDto[]> => {
    const response = await apiClient.get<PrinterDto[]>('/Printers');
    return response.data;
  },

  getPrinterById: async (printerId: number): Promise<PrinterDto> => {
    const response = await apiClient.get<PrinterDto>(`/Printers/${printerId}`);
    return response.data;
  },

  createPrinter: async (printerData: PrinterUpsertDto): Promise<PrinterDto> => {
    const response = await apiClient.post<PrinterDto>('/Printers', printerData);
    return response.data;
  },

  updatePrinter: async (printerId: number, printerData: PrinterUpsertDto): Promise<void> => {
    await apiClient.put(`/Printers/${printerId}`, printerData);
  },

  deletePrinter: async (printerId: number): Promise<void> => {
    await apiClient.delete(`/Printers/${printerId}`);
  },

  sendTestPrint: async (printerId: number): Promise<{ message: string }> => {
    const response = await apiClient.post<{ message: string }>(`/Printers/${printerId}/test-print`);
    return response.data;
  },

  getPrintJobs: async (params: PrintJobQueryParameters): Promise<PaginatedResult<PrintJobDto>> => {
    const response = await apiClient.get<PaginatedResult<PrintJobDto>>('/PrintJobs', { params });
    return response.data;
  },

  retryPrintJob: async (jobId: string): Promise<{ message: string }> => {
    const response = await apiClient.post<{ message: string }>(`/PrintJobs/${jobId}/retry`);
    return response.data;
  },
};

export default printerService;
