using SagraFacile.NET.API.DTOs.Analytics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    public interface IAnalyticsService
    {
        // Dashboard
        Task<DashboardKPIsDto> GetDashboardKPIsAsync(Guid organizationId, int? dayId = null);
        Task<List<SalesTrendDataDto>> GetSalesTrendAsync(Guid organizationId, int days = 7);
        Task<List<OrderStatusDistributionDto>> GetOrderStatusDistributionAsync(Guid organizationId, int? dayId = null);
        Task<List<TopMenuItemDto>> GetTopMenuItemsAsync(Guid organizationId, int days = 7, int limit = 5);

        // Orders Analytics
        Task<List<OrdersByHourDto>> GetOrdersByHourAsync(Guid organizationId, int? areaId = null, int? dayId = null);
        Task<List<PaymentMethodDistributionDto>> GetPaymentMethodDistributionAsync(Guid organizationId, int? areaId = null, int? dayId = null);
        Task<List<AverageOrderValueTrendDto>> GetAverageOrderValueTrendAsync(Guid organizationId, int? areaId = null, int days = 7);
        Task<List<OrderStatusTimelineEventDto>> GetOrderStatusTimelineAsync(Guid organizationId, int? areaId = null, int? dayId = null);

        // Reports
        Task<byte[]> GenerateDailySummaryReportAsync(Guid organizationId, int dayId);
        Task<byte[]> GenerateAreaPerformanceReportAsync(Guid organizationId, DateTime startDate, DateTime endDate);
    }
}
