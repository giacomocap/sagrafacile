import apiClient from './apiClient';
import { PrinterDto, PrinterUpsertDto } from '@/types';

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
};

export default printerService; 