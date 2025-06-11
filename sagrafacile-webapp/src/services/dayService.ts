import apiClient from './apiClient'; // Import the configured Axios instance
import { DayDto } from '@/types'; // Import DayDto type
import { AxiosResponse } from 'axios';

// ==================
// Day API Functions
// ==================

/**
 * Fetches the current open operational day for the user's organization.
 * GET /api/days/current
 */
export const getCurrentOpenDay = async (): Promise<DayDto | null> => {
  try {
    const response: AxiosResponse<DayDto> = await apiClient.get('/days/current');
    // API returns 200 OK with DayDto if open, or potentially 200 OK with empty body/null or 404 if none open.
    // Adjust based on actual backend behavior. Assuming it returns DayDto or null/empty for now.
    return response.data || null; // Return null if data is falsy (empty body)
  } catch (error: any) {
    if (error.response && error.response.status === 404) {
      return null; // Explicitly return null if no day is open (404 Not Found)
    }
    console.error("Error fetching current open day:", error);
    throw error; // Re-throw other errors
  }
};

/**
 * Opens a new operational day for the user's organization.
 * POST /api/days/open
 * Requires Admin role.
 */
export const openDay = async (): Promise<DayDto> => {
  const response: AxiosResponse<DayDto> = await apiClient.post('/days/open');
  return response.data;
};

/**
 * Closes the specified operational day.
 * POST /api/days/{id}/close
 * Requires Admin role.
 * @param dayId The ID of the day to close.
 */
export const closeDay = async (dayId: number): Promise<DayDto> => {
  const response: AxiosResponse<DayDto> = await apiClient.post(`/days/${dayId}/close`);
  return response.data;
};

/**
 * Fetches a list of operational days for the user's organization.
 * GET /api/days
 * Requires Admin role.
 */
export const getDays = async (): Promise<DayDto[]> => {
  const response: AxiosResponse<DayDto[]> = await apiClient.get('/days');
  return response.data;
};

/**
 * Fetches a specific operational day by its ID.
 * GET /api/days/{id}
 * Requires Admin role.
 * @param dayId The ID of the day to fetch.
 */
export const getDayById = async (dayId: number): Promise<DayDto> => {
  const response: AxiosResponse<DayDto> = await apiClient.get(`/days/${dayId}`);
  return response.data;
};
