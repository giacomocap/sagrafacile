import apiClient from './apiClient';
import { PrinterCategoryAssignmentDto } from '@/types'; // Assuming GET returns full categories

// DTO for the POST request based on ProjectMemory.md
interface SetPrinterAssignmentsDto {
  categoryIds: number[];
}

const printerAssignmentService = {
  /**
   * Fetches the menu categories currently assigned to a specific printer.
   */
  getAssignmentsForPrinter: async (printerId: number, areaId: number): Promise<PrinterCategoryAssignmentDto[]> => {
    // Assuming the backend returns the full MenuCategoryDto objects for assigned categories
    // Adjust the return type if it only returns IDs (e.g., Promise<number[]>)
    const response = await apiClient.get<PrinterCategoryAssignmentDto[]>(`/Printers/${printerId}/assignments?areaId=${areaId}`);
    return response.data;
  },

  /**
   * Sets the complete list of menu category assignments for a specific printer.
   * Replaces any existing assignments.
   */
  setAssignmentsForPrinter: async (printerId: number, areaId: number, categoryIds: number[]): Promise<void> => {
    const payload: SetPrinterAssignmentsDto = { categoryIds };
    await apiClient.post(`/Printers/${printerId}/assignments?areaId=${areaId}`, payload);
  },
};

export default printerAssignmentService; 