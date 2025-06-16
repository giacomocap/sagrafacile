using SagraFacile.NET.API.DTOs.Analytics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    public interface IAnalyticsService
    {
        // Dashboard
        Task<DashboardKPIsDto> GetDashboardKPIsAsync(int organizationId, int? dayId = null);
        Task<List<SalesTrendDataDto>> GetSalesTrendAsync(int organizationId, int days = 7);
        Task<List<OrderStatusDistributionDto>> GetOrderStatusDistributionAsync(int organizationId, int? dayId = null);
        Task<List<TopMenuItemDto>> GetTopMenuItemsAsync(int organizationId, int days = 7, int limit = 5);

        // Orders Analytics
        Task<List<OrdersByHourDto>> GetOrdersByHourAsync(int organizationId, int? areaId = null, int? dayId = null);
        Task<List<PaymentMethodDistributionDto>> GetPaymentMethodDistributionAsync(int organizationId, int? areaId = null, int? dayId = null);
        Task<List<AverageOrderValueTrendDto>> GetAverageOrderValueTrendAsync(int organizationId, int? areaId = null, int days = 7);
        Task<List<OrderStatusTimelineEventDto>> GetOrderStatusTimelineAsync(int organizationId, int? areaId = null, int? dayId = null);

        // Reports
        Task<byte[]> GenerateDailySummaryReportAsync(int organizationId, int dayId);
        Task<byte[]> GenerateAreaPerformanceReportAsync(int organizationId, DateTime startDate, DateTime endDate);
    }
}
