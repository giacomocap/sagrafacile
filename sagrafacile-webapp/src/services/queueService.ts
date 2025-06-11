import apiClient from './apiClient';
import {
    QueueStateDto,
    CalledNumberDto,
    CallNextQueueRequestDto,
    CallSpecificQueueRequestDto,
    UpdateNextSequentialNumberRequestDto,
    RespeakQueueRequestDto // Added for the new respeak request
} from '@/types';

// Base path for queue related endpoints for a specific area
const getQueueBasePath = (areaId: number | string): string => `/Areas/${areaId}/queue`; // Allow string for areaId from params

// Service object
const queueService = {
    /**
     * Gets the current state of the queue for a specific area.
     * @param areaId The ID of the area.
     * @returns Promise<QueueStateDto>
     */
    getQueueState: async (areaId: number): Promise<QueueStateDto> => {
        const response = await apiClient.get<QueueStateDto>(`${getQueueBasePath(areaId)}/state`);
        return response.data;
    },

    /**
     * Calls the next sequential number in the queue.
     * @param areaId The ID of the area.
     * @param request DTO containing optional CashierStationId.
     * @returns Promise<CalledNumberDto>
     */
    callNext: async (areaId: number, request: CallNextQueueRequestDto): Promise<CalledNumberDto> => {
        const response = await apiClient.post<CalledNumberDto>(`${getQueueBasePath(areaId)}/call-next`, request);
        return response.data;
    },

    /**
     * Calls a specific number in the queue.
     * @param areaId The ID of the area.
     * @param request DTO containing the number to call and optional CashierStationId.
     * @returns Promise<CalledNumberDto>
     */
    callSpecific: async (areaId: number, request: CallSpecificQueueRequestDto): Promise<CalledNumberDto> => {
        const response = await apiClient.post<CalledNumberDto>(`${getQueueBasePath(areaId)}/call-specific`, request);
        return response.data;
    },

    /**
     * Resets the queue sequence for an area (Admin action).
     * @param areaId The ID of the area.
     * @returns Promise<void> - Returns void or potentially some confirmation result if the backend provides one.
     */
    resetQueue: async (areaId: number): Promise<void> => {
        // Consider if the backend returns a meaningful response or just status code
        await apiClient.post(`${getQueueBasePath(areaId)}/reset`, {});
        // Assuming no specific data is returned on success, otherwise map response.data
    },

    /**
     * Updates the next sequential number manually (Admin action).
     * @param areaId The ID of the area.
     * @param request DTO containing the new next number.
     * @returns Promise<void> - Returns void or potentially some confirmation result.
     */
    updateNextSequentialNumber: async (areaId: number, request: UpdateNextSequentialNumberRequestDto): Promise<void> => {
        await apiClient.put(`${getQueueBasePath(areaId)}/next-sequential-number`, request);
        // Assuming no specific data is returned on success
    },

    /**
     * Requests the backend to respeak the last called number for a specific area and cashier station.
     * @param areaId The ID of the area.
     * @param request DTO containing the CashierStationId.
     * @returns Promise<CalledNumberDto> - The details of the number that was resent for announcement.
     */
    respeakLastCalledNumber: async (areaId: number, request: RespeakQueueRequestDto): Promise<CalledNumberDto> => {
        const response = await apiClient.post<CalledNumberDto>(`${getQueueBasePath(areaId)}/respeak-last-called`, request);
        return response.data;
    }
};

export default queueService;
