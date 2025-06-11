import axios, { AxiosError, AxiosResponse, InternalAxiosRequestConfig } from 'axios';
import { CashierStationDto, OrderDto, TokenResponseDto, RefreshTokenRequestDto } from '@/types';

// Retrieve the API base URL from environment variables
export const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL;

if (!apiBaseUrl) {
  console.error("Error: NEXT_PUBLIC_API_BASE_URL environment variable is not set.");
  // You might want to throw an error or provide a default fallback,
  // but logging an error is often sufficient during development.
}

const apiClient = axios.create({
  baseURL: apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
  // You can add other default settings like timeout here
  // timeout: 10000, // e.g., 10 seconds
});

// Variable to prevent multiple token refresh requests
let isRefreshing = false;
let failedQueue: Array<{ resolve: (value: unknown) => void, reject: (reason?: any) => void }> = [];

const processQueue = (error: AxiosError | null, token: string | null = null) => {
  failedQueue.forEach(prom => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token);
    }
  });
  failedQueue = [];
};

// Request Interceptor: Adds the JWT token to the Authorization header if available
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    // Check if running in the browser environment before accessing localStorage
    if (typeof window !== 'undefined') {
      const accessToken = localStorage.getItem('authToken'); // Use 'authToken' as per AuthContext
      if (accessToken) {
        config.headers.Authorization = `Bearer ${accessToken}`;
      }
    }
    return config;
  },
  (error: AxiosError) => {
    return Promise.reject(error);
  }
);

// Response Interceptor: Handle global errors or token refresh logic
apiClient.interceptors.response.use(
  (response: AxiosResponse) => {
    return response;
  },
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    if (error.response?.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        })
        .then(token => {
          if (originalRequest.headers) {
            originalRequest.headers['Authorization'] = 'Bearer ' + token;
          }
          return apiClient(originalRequest);
        })
        .catch(err => {
          return Promise.reject(err);
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const currentRefreshToken = localStorage.getItem('refreshToken');
      if (!currentRefreshToken) {
        console.error("No refresh token available for auto-refresh.");
        isRefreshing = false;
        // Trigger logout from AuthContext if possible, or redirect
        // This part needs careful handling to call AuthContext's logout
        // For now, just remove tokens and reject
        localStorage.removeItem('authToken');
        localStorage.removeItem('refreshToken');
        // Potentially: window.dispatchEvent(new Event('logout-event'));
        return Promise.reject(error);
      }

      try {
        const { data: tokenResponse } = await apiClient.post<TokenResponseDto>('/accounts/refresh-token', { refreshToken: currentRefreshToken } as RefreshTokenRequestDto);
        
        localStorage.setItem('authToken', tokenResponse.accessToken);
        if (tokenResponse.refreshToken) { // Backend might not always send a new refresh token
            localStorage.setItem('refreshToken', tokenResponse.refreshToken);
        }
        
        if (originalRequest.headers) {
            originalRequest.headers['Authorization'] = `Bearer ${tokenResponse.accessToken}`;
        }
        processQueue(null, tokenResponse.accessToken);
        return apiClient(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError as AxiosError, null);
        console.error("Token refresh failed:", refreshError);
        // Trigger logout from AuthContext
        localStorage.removeItem('authToken');
        localStorage.removeItem('refreshToken');
        // Potentially: window.dispatchEvent(new Event('logout-event'));
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }
    return Promise.reject(error);
  }
);

// Method to fetch public active cashier stations for an area
export const getActiveCashierStationsForArea = (areaId: string): Promise<AxiosResponse<CashierStationDto[]>> => {
    return apiClient.get(`/public/areas/${areaId}/cashier-stations`);
};

/**
 * Fetches orders that are ready for pickup for a specific area (public endpoint).
 * @param areaId The ID of the area.
 * @returns A promise that resolves to an array of OrderDto.
 */
export const getPublicReadyForPickupOrders = async (areaId: number): Promise<OrderDto[]> => {
  const response = await apiClient.get<OrderDto[]>(`/public/areas/${areaId}/orders/ready-for-pickup`);
  return response.data;
};

/**
 * Confirms that an order has been picked up (staff endpoint).
 * @param orderId The ID of the order to confirm.
 * @returns A promise that resolves to the updated OrderDto.
 */
export const confirmOrderPickup = async (orderId: string): Promise<OrderDto> => {
  const response = await apiClient.put<OrderDto>(`/orders/${orderId}/confirm-pickup`);
  return response.data;
};

export default apiClient;
