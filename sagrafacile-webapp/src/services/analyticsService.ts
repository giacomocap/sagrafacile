import apiClient from "./apiClient";
import {
    DashboardKPIsDto,
    SalesTrendDataDto,
    OrderStatusDistributionDto,
    TopMenuItemDto,
    OrdersByHourDto,
    PaymentMethodDistributionDto,
    AverageOrderValueTrendDto,
    OrderStatusTimelineEventDto,
} from '../types';

export class AnalyticsService {
    // Dashboard KPIs
    static async getDashboardKPIs(organizationId: number, dayId?: number): Promise<DashboardKPIsDto> {
        const params = new URLSearchParams();
        params.append('organizationId', organizationId.toString());
        if (dayId) {
            params.append('dayId', dayId.toString());
        }

        const response = await apiClient.get(`/analytics/dashboard/kpis?${params.toString()}`);
        return response.data;
    }

    static async getSalesTrend(organizationId: number, days: number = 7): Promise<SalesTrendDataDto[]> {
        const params = new URLSearchParams();
        params.append('organizationId', organizationId.toString());
        params.append('days', days.toString());

        const response = await apiClient.get(`/analytics/dashboard/sales-trend?${params.toString()}`);
        return response.data;
    }

    static async getOrderStatusDistribution(organizationId: number, dayId?: number): Promise<OrderStatusDistributionDto[]> {
        const params = new URLSearchParams();
        params.append('organizationId', organizationId.toString());
        if (dayId) {
            params.append('dayId', dayId.toString());
        }

        const response = await apiClient.get(`/analytics/dashboard/order-status?${params.toString()}`);
        return response.data;
    }

    static async getTopMenuItems(organizationId: number, days: number = 7, limit: number = 5): Promise<TopMenuItemDto[]> {
        const params = new URLSearchParams();
        params.append('organizationId', organizationId.toString());
        params.append('days', days.toString());
        params.append('limit', limit.toString());

        const response = await apiClient.get(`/analytics/dashboard/top-menu-items?${params.toString()}`);
        return response.data;
    }

    // Orders Analytics
    static async getOrdersByHour(organizationId: number, areaId?: number, dayId?: number): Promise<OrdersByHourDto[]> {
        const params = new URLSearchParams();
        params.append('organizationId', organizationId.toString());
        if (areaId) {
            params.append('areaId', areaId.toString());
        }
        if (dayId) {
            params.append('dayId', dayId.toString());
        }

        const response = await apiClient.get(`/analytics/orders/by-hour?${params.toString()}`);
        return response.data;
    }

    static async getPaymentMethodDistribution(organizationId: number, areaId?: number, dayId?: number): Promise<PaymentMethodDistributionDto[]> {
        const params = new URLSearchParams();
        params.append('organizationId', organizationId.toString());
        if (areaId) {
            params.append('areaId', areaId.toString());
        }
        if (dayId) {
            params.append('dayId', dayId.toString());
        }

        const response = await apiClient.get(`/analytics/orders/payment-methods?${params.toString()}`);
        return response.data;
    }

    static async getAverageOrderValueTrend(organizationId: number, areaId?: number, days: number = 7): Promise<AverageOrderValueTrendDto[]> {
        const params = new URLSearchParams();
        params.append('organizationId', organizationId.toString());
        if (areaId) {
            params.append('areaId', areaId.toString());
        }
        params.append('days', days.toString());

        const response = await apiClient.get(`/analytics/orders/average-value-trend?${params.toString()}`);
        return response.data;
    }

    static async getOrderStatusTimeline(organizationId: number, areaId?: number, dayId?: number): Promise<OrderStatusTimelineEventDto[]> {
        const params = new URLSearchParams();
        params.append('organizationId', organizationId.toString());
        if (areaId) {
            params.append('areaId', areaId.toString());
        }
        if (dayId) {
            params.append('dayId', dayId.toString());
        }

        const response = await apiClient.get(`/analytics/orders/status-timeline?${params.toString()}`);
        return response.data;
    }

    // Reports
    static async generateDailySummaryReport(organizationId: number, dayId: number): Promise<Blob> {
        const params = new URLSearchParams();
        params.append('organizationId', organizationId.toString());
        params.append('dayId', dayId.toString());

        const response = await apiClient.get(`/analytics/reports/daily-summary?${params.toString()}`, {
            responseType: 'blob'
        });
        return response.data;
    }

    static async generateAreaPerformanceReport(organizationId: number, startDate: string, endDate: string): Promise<Blob> {
        const params = new URLSearchParams();
        params.append('organizationId', organizationId.toString());
        params.append('startDate', startDate);
        params.append('endDate', endDate);

        const response = await apiClient.get(`/analytics/reports/area-performance?${params.toString()}`, {
            responseType: 'blob'
        });
        return response.data;
    }
}

export const analyticsService = AnalyticsService;
