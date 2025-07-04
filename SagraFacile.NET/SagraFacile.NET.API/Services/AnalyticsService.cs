using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs.Analytics;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    public class AnalyticsService : BaseService, IAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AnalyticsService> logger) : base(httpContextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private async Task<Day> GetTargetDayAsync(Guid organizationId, int? dayId, bool throwIfNotFound = false)
        {
            _logger.LogDebug("Attempting to get target day for organization {OrganizationId}, requested DayId: {DayId}, ThrowIfNotFound: {ThrowIfNotFound}", organizationId, dayId, throwIfNotFound);
            Day targetDayEntity = null;
            if (dayId.HasValue)
            {
                targetDayEntity = await _context.Days
                    .FirstOrDefaultAsync(d => d.Id == dayId.Value && d.OrganizationId == organizationId);
                if (targetDayEntity == null && throwIfNotFound)
                {
                    _logger.LogWarning("Day with ID {DayId} not found for organization {OrganizationId}.", dayId.Value, organizationId);
                    throw new KeyNotFoundException($"Giorno operativo con ID {dayId.Value} non trovato per l'organizzazione specificata.");
                }
                else if (targetDayEntity == null)
                {
                    _logger.LogDebug("Day with ID {DayId} not found for organization {OrganizationId}. (Not throwing exception as throwIfNotFound is false)", dayId.Value, organizationId);
                }
            }
            else
            {
                targetDayEntity = await _context.Days
                    .Where(d => d.OrganizationId == organizationId && d.Status == DayStatus.Open)
                    .OrderByDescending(d => d.StartTime)
                    .FirstOrDefaultAsync();

                if (targetDayEntity == null) // If no open day, try the most recently closed one
                {
                    _logger.LogDebug("No open day found for organization {OrganizationId}. Attempting to find most recently closed day.", organizationId);
                    targetDayEntity = await _context.Days
                        .Where(d => d.OrganizationId == organizationId && d.Status == DayStatus.Closed)
                        .OrderByDescending(d => d.EndTime)
                        .FirstOrDefaultAsync();
                }
            }

            if (targetDayEntity == null && throwIfNotFound && !dayId.HasValue) // if dayId was null and still no day found
            {
                _logger.LogWarning("No operational day (open or recently closed) found for organization {OrganizationId}.", organizationId);
                throw new KeyNotFoundException($"Nessun giorno operativo (aperto o chiuso di recente) trovato per l'organizzazione {organizationId}.");
            }
            _logger.LogDebug("Target day found for organization {OrganizationId}: {DayId} ({DayDate})", organizationId, targetDayEntity?.Id, targetDayEntity?.StartTime.ToString("yyyy-MM-dd"));
            return targetDayEntity;
        }

        // Dashboard
        public async Task<DashboardKPIsDto> GetDashboardKPIsAsync(Guid organizationId, int? dayId = null)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                _logger.LogWarning("User {UserId} from organization {UserOrgId} attempted to access KPIs for organization {OrganizationId} without SuperAdmin rights.", GetUserId(), userOrgId, organizationId);
                throw new UnauthorizedAccessException("User is not authorized to access KPIs for this organization.");
            }

            _logger.LogInformation("Fetching dashboard KPIs for organization {OrganizationId}, dayId: {DayId}", organizationId, dayId);

            Day targetDay = await GetTargetDayAsync(organizationId, dayId);

            if (targetDay == null)
            {
                _logger.LogInformation("No relevant operational day found for organization {OrganizationId} (dayId: {DayId}). Returning default KPIs.", organizationId, dayId);
                return new DashboardKPIsDto
                {
                    DayId = dayId ?? 0, // If dayId was provided but not found, reflect it. Otherwise 0.
                    DayDate = dayId.HasValue ? "N/A - Giorno non trovato" : DateTime.UtcNow.ToString("yyyy-MM-dd") + " (Nessun Giorno Operativo)",
                    MostPopularCategory = "N/A",
                    TodayTotalSales = 0,
                    TodayOrderCount = 0,
                    AverageOrderValue = 0,
                    TotalCoperti = 0
                };
            }

            var ordersQuery = _context.Orders
                .Where(o => o.DayId == targetDay.Id && o.OrganizationId == organizationId && o.Status != OrderStatus.PreOrder && o.Status != OrderStatus.Pending && o.Status != OrderStatus.Cancelled);

            var todayTotalSales = await ordersQuery.SumAsync(o => o.TotalAmount);
            var todayOrderCount = await ordersQuery.CountAsync();
            var averageOrderValue = todayOrderCount > 0 ? Math.Round(todayTotalSales / todayOrderCount, 2) : 0;
            var totalCoperti = await ordersQuery.SumAsync(o => o.NumberOfGuests);

            var mostPopularCategory = "N/A";
            if (todayOrderCount > 0)
            {
                var categoryPopularity = await _context.OrderItems
                    .Where(oi => ordersQuery.Select(o => o.Id).Contains(oi.OrderId))
                    .Include(oi => oi.MenuItem)
                    .ThenInclude(mi => mi.MenuCategory)
                    .GroupBy(oi => oi.MenuItem.MenuCategory.Name)
                    .Select(g => new { CategoryName = g.Key, TotalQuantity = g.Sum(oi => oi.Quantity) })
                    .OrderByDescending(x => x.TotalQuantity)
                    .FirstOrDefaultAsync();

                if (categoryPopularity != null)
                {
                    mostPopularCategory = categoryPopularity.CategoryName;
                    _logger.LogDebug("Most popular category for day {DayId} is {CategoryName} with {TotalQuantity} units.", targetDay.Id, categoryPopularity.CategoryName, categoryPopularity.TotalQuantity);
                }
                else
                {
                    _logger.LogDebug("No popular category found for day {DayId}.", targetDay.Id);
                }
            }

            _logger.LogInformation("Successfully fetched dashboard KPIs for organization {OrganizationId}, dayId: {DayId}. Total Sales: {TotalSales}, Order Count: {OrderCount}", organizationId, dayId, todayTotalSales, todayOrderCount);
            return new DashboardKPIsDto
            {
                TodayTotalSales = todayTotalSales,
                TodayOrderCount = todayOrderCount,
                AverageOrderValue = averageOrderValue,
                MostPopularCategory = mostPopularCategory,
                TotalCoperti = totalCoperti,
                DayId = targetDay.Id,
                DayDate = targetDay.StartTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
        }

        public async Task<List<SalesTrendDataDto>> GetSalesTrendAsync(Guid organizationId, int days = 7)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                _logger.LogWarning("User {UserId} from organization {UserOrgId} attempted to access sales trend for organization {OrganizationId} without SuperAdmin rights.", GetUserId(), userOrgId, organizationId);
                throw new UnauthorizedAccessException("User is not authorized to access sales trend for this organization.");
            }
            _logger.LogInformation("Fetching sales trend for organization {OrganizationId} for the last {Days} days.", organizationId, days);

            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-days + 1);

            var dailySales = await _context.Orders
                .Where(o => o.OrganizationId == organizationId &&
                              o.Day.StartTime.Date >= startDate && o.Day.StartTime.Date <= endDate &&
                              o.Status != OrderStatus.PreOrder && o.Status != OrderStatus.Pending && o.Status != OrderStatus.Cancelled)
                .GroupBy(o => new { o.DayId, DayDate = o.Day.StartTime.Date })
                .Select(g => new
                {
                    g.Key.DayId,
                    Date = g.Key.DayDate,
                    Sales = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                })
                .ToListAsync();

            var result = new List<SalesTrendDataDto>();
            for (int i = 0; i < days; i++)
            {
                var currentDate = startDate.AddDays(i);
                var salesData = dailySales.FirstOrDefault(ds => ds.Date == currentDate);
                if (salesData != null)
                {
                    result.Add(new SalesTrendDataDto
                    {
                        DayId = salesData.DayId,
                        Date = salesData.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Sales = salesData.Sales,
                        OrderCount = salesData.OrderCount
                    });
                    _logger.LogDebug("Sales data found for {CurrentDate}: Sales={Sales}, Orders={OrderCount}", currentDate.ToString("yyyy-MM-dd"), salesData.Sales, salesData.OrderCount);
                }
                else
                {
                    var dayEntity = await _context.Days
                        .FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.StartTime.Date == currentDate);
                    result.Add(new SalesTrendDataDto
                    {
                        DayId = dayEntity?.Id,
                        Date = currentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Sales = 0,
                        OrderCount = 0
                    });
                    _logger.LogDebug("No sales data for {CurrentDate}. Added zero values.", currentDate.ToString("yyyy-MM-dd"));
                }
            }
            _logger.LogInformation("Successfully fetched sales trend for organization {OrganizationId}.", organizationId);
            return result.OrderBy(r => r.Date).ToList();
        }

        public async Task<List<OrderStatusDistributionDto>> GetOrderStatusDistributionAsync(Guid organizationId, int? dayId = null)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                _logger.LogWarning("User {UserId} from organization {UserOrgId} attempted to access order status distribution for organization {OrganizationId} without SuperAdmin rights.", GetUserId(), userOrgId, organizationId);
                throw new UnauthorizedAccessException("User is not authorized to access order status distribution for this organization.");
            }
            _logger.LogInformation("Fetching order status distribution for organization {OrganizationId}, dayId: {DayId}", organizationId, dayId);

            Day targetDay = await GetTargetDayAsync(organizationId, dayId);
            if (targetDay == null)
            {
                _logger.LogInformation("No relevant operational day found for organization {OrganizationId} (dayId: {DayId}). Returning empty order status distribution.", organizationId, dayId);
                return new List<OrderStatusDistributionDto>();
            }

            var ordersQuery = _context.Orders.Where(o => o.DayId == targetDay.Id && o.OrganizationId == organizationId);
            var totalOrdersInDay = await ordersQuery.CountAsync();

            if (totalOrdersInDay == 0)
            {
                _logger.LogInformation("No orders found for day {DayId} in organization {OrganizationId}. Returning empty order status distribution.", targetDay.Id, organizationId);
                return new List<OrderStatusDistributionDto>();
            }

            var distribution = await ordersQuery
                .GroupBy(o => o.Status)
                .Select(g => new OrderStatusDistributionDto
                {
                    Status = g.Key.ToString(),
                    Count = g.Count(),
                    Percentage = Math.Round((decimal)g.Count() * 100 / totalOrdersInDay, 2)
                })
                .ToListAsync();

            _logger.LogInformation("Successfully fetched order status distribution for organization {OrganizationId}, dayId: {DayId}. Total orders: {TotalOrders}", organizationId, dayId, totalOrdersInDay);
            return distribution;
        }

        public async Task<List<TopMenuItemDto>> GetTopMenuItemsAsync(Guid organizationId, int days = 7, int limit = 5)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                _logger.LogWarning("User {UserId} from organization {UserOrgId} attempted to access top menu items for organization {OrganizationId} without SuperAdmin rights.", GetUserId(), userOrgId, organizationId);
                throw new UnauthorizedAccessException("User is not authorized to access top menu items for this organization.");
            }
            _logger.LogInformation("Fetching top {Limit} menu items for organization {OrganizationId} for the last {Days} days.", limit, organizationId, days);

            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-days + 1);

            var topItems = await _context.OrderItems
                .Where(oi => oi.Order.OrganizationId == organizationId &&
                             oi.Order.Day.StartTime.Date >= startDate && oi.Order.Day.StartTime.Date <= endDate &&
                             oi.Order.Status != OrderStatus.PreOrder && oi.Order.Status != OrderStatus.Pending && oi.Order.Status != OrderStatus.Cancelled)
                .GroupBy(oi => new { oi.MenuItemId, oi.MenuItem.Name, CategoryName = oi.MenuItem.MenuCategory.Name })
                .Select(g => new TopMenuItemDto
                {
                    ItemName = g.Key.Name,
                    CategoryName = g.Key.CategoryName,
                    Quantity = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice)
                })
                .OrderByDescending(dto => dto.Quantity)
                .ThenByDescending(dto => dto.Revenue)
                .Take(limit)
                .ToListAsync();

            _logger.LogInformation("Successfully fetched {Count} top menu items for organization {OrganizationId}.", topItems.Count, organizationId);
            return topItems;
        }

        // Orders Analytics
        public async Task<List<OrdersByHourDto>> GetOrdersByHourAsync(Guid organizationId, int? areaId = null, int? dayId = null)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                _logger.LogWarning("User {UserId} from organization {UserOrgId} attempted to access orders by hour for organization {OrganizationId} without SuperAdmin rights.", GetUserId(), userOrgId, organizationId);
                throw new UnauthorizedAccessException("User is not authorized to access orders by hour for this organization.");
            }

            Day targetDay = await GetTargetDayAsync(organizationId, dayId);
            if (targetDay == null)
            {
                _logger.LogInformation("No relevant operational day found for organization {OrganizationId} (dayId: {DayId}). Returning empty orders by hour distribution.", organizationId, dayId);
                return new List<OrdersByHourDto>();
            }

            var ordersQuery = _context.Orders
                .Where(o => o.OrganizationId == organizationId && o.DayId == targetDay.Id &&
                              o.Status != OrderStatus.PreOrder && o.Status != OrderStatus.Pending && o.Status != OrderStatus.Cancelled);

            if (areaId.HasValue)
            {
                var area = await _context.Areas.FirstOrDefaultAsync(a => a.Id == areaId.Value && a.OrganizationId == organizationId);
                if (area == null)
                {
                    _logger.LogWarning("Area with ID {AreaId} not found or does not belong to organization {OrganizationId} for GetOrdersByHour.", areaId.Value, organizationId);
                    throw new KeyNotFoundException($"Area with ID {areaId.Value} not found or does not belong to organization {organizationId}.");
                }
                ordersQuery = ordersQuery.Where(o => o.AreaId == areaId.Value);
            }
            _logger.LogInformation("Fetching orders by hour for organization {OrganizationId}, area {AreaId}, dayId: {DayId}", organizationId, areaId, targetDay.Id);

            var ordersByHour = await ordersQuery
                .GroupBy(o => o.OrderDateTime.Hour)
                .Select(g => new OrdersByHourDto
                {
                    Hour = g.Key,
                    OrderCount = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(dto => dto.Hour)
                .ToListAsync();

            var result = new List<OrdersByHourDto>();
            for (int h = 0; h < 24; h++)
            {
                var data = ordersByHour.FirstOrDefault(obh => obh.Hour == h);
                result.Add(data ?? new OrdersByHourDto { Hour = h, OrderCount = 0, Revenue = 0 });
            }
            _logger.LogInformation("Successfully fetched orders by hour for organization {OrganizationId}, area {AreaId}, dayId: {DayId}.", organizationId, areaId, targetDay.Id);
            return result;
        }

        public async Task<List<PaymentMethodDistributionDto>> GetPaymentMethodDistributionAsync(Guid organizationId, int? areaId = null, int? dayId = null)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                _logger.LogWarning("User {UserId} from organization {UserOrgId} attempted to access payment method distribution for organization {OrganizationId} without SuperAdmin rights.", GetUserId(), userOrgId, organizationId);
                throw new UnauthorizedAccessException("User is not authorized to access payment method distribution for this organization.");
            }

            Day targetDay = await GetTargetDayAsync(organizationId, dayId);
            if (targetDay == null)
            {
                _logger.LogInformation("No relevant operational day found for organization {OrganizationId} (dayId: {DayId}). Returning empty payment method distribution.", organizationId, dayId);
                return new List<PaymentMethodDistributionDto>();
            }

            var ordersQuery = _context.Orders
                .Where(o => o.OrganizationId == organizationId && o.DayId == targetDay.Id &&
                              o.Status != OrderStatus.PreOrder && o.Status != OrderStatus.Pending && o.Status != OrderStatus.Cancelled && o.PaymentMethod != null);

            if (areaId.HasValue)
            {
                var area = await _context.Areas.FirstOrDefaultAsync(a => a.Id == areaId.Value && a.OrganizationId == organizationId);
                if (area == null)
                {
                    _logger.LogWarning("Area with ID {AreaId} not found or does not belong to organization {OrganizationId} for GetPaymentMethodDistribution.", areaId.Value, organizationId);
                    throw new KeyNotFoundException($"Area with ID {areaId.Value} not found or does not belong to organization {organizationId}.");
                }
                ordersQuery = ordersQuery.Where(o => o.AreaId == areaId.Value);
            }
            _logger.LogInformation("Fetching payment method distribution for organization {OrganizationId}, area {AreaId}, dayId: {DayId}", organizationId, areaId, targetDay.Id);

            var totalPaidAmountInDay = await ordersQuery.SumAsync(o => o.TotalAmount);
            var totalPaidOrdersInDay = await ordersQuery.CountAsync();

            if (totalPaidOrdersInDay == 0)
            {
                _logger.LogInformation("No paid orders found for day {DayId} in organization {OrganizationId}. Returning empty payment method distribution.", targetDay.Id, organizationId);
                return new List<PaymentMethodDistributionDto>();
            }

            var distribution = await ordersQuery
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new PaymentMethodDistributionDto
                {
                    PaymentMethod = g.Key.ToString(),
                    Count = g.Count(),
                    Amount = g.Sum(o => o.TotalAmount),
                    Percentage = totalPaidAmountInDay > 0 ? Math.Round(g.Sum(o => o.TotalAmount) * 100 / totalPaidAmountInDay, 2) : 0
                })
                .ToListAsync();

            _logger.LogInformation("Successfully fetched payment method distribution for organization {OrganizationId}, dayId: {DayId}. Total paid orders: {TotalPaidOrders}", organizationId, dayId, totalPaidOrdersInDay);
            return distribution;
        }

        public async Task<List<AverageOrderValueTrendDto>> GetAverageOrderValueTrendAsync(Guid organizationId, int? areaId = null, int days = 7)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                _logger.LogWarning("User {UserId} from organization {UserOrgId} attempted to access average order value trend for organization {OrganizationId} without SuperAdmin rights.", GetUserId(), userOrgId, organizationId);
                throw new UnauthorizedAccessException("User is not authorized to access average order value trend for this organization.");
            }

            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-days + 1);
            _logger.LogInformation("Fetching average order value trend for organization {OrganizationId}, area {AreaId} from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}", organizationId, areaId, startDate, endDate);

            var ordersQueryBase = _context.Orders
                .Where(o => o.OrganizationId == organizationId &&
                              o.Day.StartTime.Date >= startDate && o.Day.StartTime.Date <= endDate &&
                              o.Status != OrderStatus.PreOrder && o.Status != OrderStatus.Pending && o.Status != OrderStatus.Cancelled);

            if (areaId.HasValue)
            {
                var area = await _context.Areas.FirstOrDefaultAsync(a => a.Id == areaId.Value && a.OrganizationId == organizationId);
                if (area == null)
                {
                    _logger.LogWarning("Area with ID {AreaId} not found or does not belong to organization {OrganizationId} for GetAverageOrderValueTrend.", areaId.Value, organizationId);
                    throw new KeyNotFoundException($"Area with ID {areaId.Value} not found or does not belong to organization {organizationId}.");
                }
                ordersQueryBase = ordersQueryBase.Where(o => o.AreaId == areaId.Value);
            }

            var dailyAOV = await ordersQueryBase
                .GroupBy(o => new { o.DayId, DayDate = o.Day.StartTime.Date })
                .Select(g => new
                {
                    g.Key.DayId,
                    Date = g.Key.DayDate,
                    TotalSales = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                })
                .ToListAsync();

            var result = new List<AverageOrderValueTrendDto>();
            for (int i = 0; i < days; i++)
            {
                var currentDate = startDate.AddDays(i);
                var aovData = dailyAOV.FirstOrDefault(ds => ds.Date == currentDate);
                if (aovData != null && aovData.OrderCount > 0)
                {
                    result.Add(new AverageOrderValueTrendDto
                    {
                        DayId = aovData.DayId,
                        Date = aovData.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        AverageValue = Math.Round(aovData.TotalSales / aovData.OrderCount, 2),
                        OrderCount = aovData.OrderCount
                    });
                    _logger.LogDebug("AOV data found for {CurrentDate}: AverageValue={AverageValue}, Orders={OrderCount}", currentDate.ToString("yyyy-MM-dd"), Math.Round(aovData.TotalSales / aovData.OrderCount, 2), aovData.OrderCount);
                }
                else
                {
                    var dayEntity = await _context.Days
                       .FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.StartTime.Date == currentDate);
                    result.Add(new AverageOrderValueTrendDto
                    {
                        DayId = aovData?.DayId ?? dayEntity?.Id,
                        Date = currentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        AverageValue = 0,
                        OrderCount = aovData?.OrderCount ?? 0
                    });
                    _logger.LogDebug("No AOV data for {CurrentDate}. Added zero values.", currentDate.ToString("yyyy-MM-dd"));
                }
            }
            _logger.LogInformation("Successfully fetched average order value trend for organization {OrganizationId}.", organizationId);
            return result.OrderBy(r => r.Date).ToList();
        }

        public async Task<List<OrderStatusTimelineEventDto>> GetOrderStatusTimelineAsync(Guid organizationId, int? areaId = null, int? dayId = null)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                _logger.LogWarning("User {UserId} from organization {UserOrgId} attempted to access order status timeline for organization {OrganizationId} without SuperAdmin rights.", GetUserId(), userOrgId, organizationId);
                throw new UnauthorizedAccessException("User is not authorized to access order status timeline for this organization.");
            }

            Day targetDay = await GetTargetDayAsync(organizationId, dayId);
            if (targetDay == null)
            {
                _logger.LogInformation("No relevant operational day found for organization {OrganizationId} (dayId: {DayId}). Returning empty order status timeline.", organizationId, dayId);
                return new List<OrderStatusTimelineEventDto>();
            }

            var ordersQuery = _context.Orders
                .Where(o => o.OrganizationId == organizationId && o.DayId == targetDay.Id);

            if (areaId.HasValue)
            {
                var area = await _context.Areas.FirstOrDefaultAsync(a => a.Id == areaId.Value && a.OrganizationId == organizationId);
                if (area == null)
                {
                    _logger.LogWarning("Area with ID {AreaId} not found or does not belong to organization {OrganizationId} for GetOrderStatusTimeline.", areaId.Value, organizationId);
                    throw new KeyNotFoundException($"Area with ID {areaId.Value} not found or does not belong to organization {organizationId}.");
                }
                ordersQuery = ordersQuery.Where(o => o.AreaId == areaId.Value);
            }
            _logger.LogInformation("Fetching order status timeline for organization {OrganizationId}, area {AreaId}, dayId: {DayId}", organizationId, areaId, targetDay.Id);

            var orders = await ordersQuery.OrderBy(o => o.OrderDateTime).ToListAsync();
            var timelineEvents = new List<OrderStatusTimelineEventDto>();

            // Simplified logic: Since Order model only has OrderDateTime and no separate status history table or individual status timestamps,
            // we can only create one event per order representing its creation or last significant update.
            // PreviousStatus and DurationInPreviousStatusMinutes will be null.
            // For a true timeline, an OrderStatusHistory table or individual status timestamps on the Order model are needed.
            foreach (var order in orders)
            {
                timelineEvents.Add(new OrderStatusTimelineEventDto
                {
                    OrderId = order.Id,
                    DisplayOrderNumber = order.DisplayOrderNumber,
                    Status = order.Status.ToString(),
                    Timestamp = order.OrderDateTime, // Using OrderDateTime as the primary event time
                    PreviousStatus = null,
                    DurationInPreviousStatusMinutes = null
                });
            }
            _logger.LogInformation("Successfully fetched order status timeline for organization {OrganizationId}, dayId: {DayId}. Total events: {EventCount}", organizationId, dayId, timelineEvents.Count);
            return timelineEvents.OrderBy(e => e.Timestamp).ThenBy(e => e.DisplayOrderNumber).ToList();
        }


        // Reports
        public async Task<byte[]> GenerateDailySummaryReportAsync(Guid organizationId, int dayId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                _logger.LogWarning("User {UserId} from organization {UserOrgId} attempted to generate daily summary report for organization {OrganizationId} without SuperAdmin rights.", GetUserId(), userOrgId, organizationId);
                throw new UnauthorizedAccessException("User is not authorized to generate daily summary report for this organization.");
            }
            _logger.LogInformation("Generating daily summary report for organization {OrganizationId}, day {DayId}", organizationId, dayId);

            var day = await _context.Days.FirstOrDefaultAsync(d => d.Id == dayId && d.OrganizationId == organizationId);
            if (day == null)
            {
                _logger.LogWarning("Day {DayId} not found for organization {OrganizationId} when generating daily summary report.", dayId, organizationId);
                throw new KeyNotFoundException($"Giorno operativo {dayId} non trovato.");
            }

            var orders = await _context.Orders
                .Where(o => o.DayId == dayId && o.OrganizationId == organizationId && o.Status != OrderStatus.PreOrder && o.Status != OrderStatus.Pending && o.Status != OrderStatus.Cancelled)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .ThenInclude(mi => mi.MenuCategory)
                .Include(o => o.Area)
                .ToListAsync();

            var reportContent = new StringBuilder();
            reportContent.AppendLine($"Daily Summary Report - SagraFacile");
            reportContent.AppendLine($"Organization ID: {organizationId}");
            reportContent.AppendLine($"Day ID: {dayId} ({day.StartTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})");
            reportContent.AppendLine($"Generated At: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC");
            reportContent.AppendLine("---");

            if (!orders.Any())
            {
                _logger.LogInformation("No orders found for day {DayId} in organization {OrganizationId}. Generating empty report.", dayId, organizationId);
                reportContent.AppendLine("No orders found for this day.");
                return Encoding.UTF8.GetBytes(reportContent.ToString());
            }

            reportContent.AppendLine($"Total Orders: {orders.Count}");
            reportContent.AppendLine($"Total Sales: {orders.Sum(o => o.TotalAmount).ToString("C", CultureInfo.GetCultureInfo("it-IT"))}");
            reportContent.AppendLine($"Average Order Value: {(orders.Count > 0 ? (orders.Sum(o => o.TotalAmount) / orders.Count) : 0).ToString("C", CultureInfo.GetCultureInfo("it-IT"))}");
            reportContent.AppendLine();

            reportContent.AppendLine("Sales by Category:");
            var salesByCategory = orders.SelectMany(o => o.OrderItems)
                .GroupBy(oi => oi.MenuItem.MenuCategory.Name)
                .Select(g => new { Category = g.Key, Total = g.Sum(oi => oi.Quantity * oi.UnitPrice) })
                .OrderByDescending(x => x.Total);
            foreach (var catSales in salesByCategory)
            {
                reportContent.AppendLine($"- {catSales.Category}: {catSales.Total.ToString("C", CultureInfo.GetCultureInfo("it-IT"))}");
            }
            reportContent.AppendLine();

            reportContent.AppendLine("Top Selling Items (by Quantity):");
            var topItems = orders.SelectMany(o => o.OrderItems)
                .GroupBy(oi => oi.MenuItem.Name)
                .Select(g => new { ItemName = g.Key, Quantity = g.Sum(oi => oi.Quantity), Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice) })
                .OrderByDescending(x => x.Quantity)
                .Take(10); // Top 10 items
            foreach (var itemSales in topItems)
            {
                reportContent.AppendLine($"- {itemSales.ItemName}: {itemSales.Quantity} units (Revenue: {itemSales.Revenue.ToString("C", CultureInfo.GetCultureInfo("it-IT"))})");
            }
            reportContent.AppendLine();

            reportContent.AppendLine("Payment Method Distribution:");
            var paymentMethods = orders.Where(o => !string.IsNullOrEmpty(o.PaymentMethod))
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new { Method = g.Key, Count = g.Count(), Amount = g.Sum(o => o.TotalAmount) });
            foreach (var pm in paymentMethods)
            {
                reportContent.AppendLine($"- {pm.Method}: {pm.Count} orders, Total: {pm.Amount.ToString("C", CultureInfo.GetCultureInfo("it-IT"))}");
            }

            _logger.LogInformation("Successfully generated daily summary report for organization {OrganizationId}, day {DayId}.", organizationId, dayId);
            return Encoding.UTF8.GetBytes(reportContent.ToString());
        }

        public async Task<byte[]> GenerateAreaPerformanceReportAsync(Guid organizationId, DateTime startDate, DateTime endDate)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                _logger.LogWarning("User {UserId} from organization {UserOrgId} attempted to generate area performance report for organization {OrganizationId} without SuperAdmin rights.", GetUserId(), userOrgId, organizationId);
                throw new UnauthorizedAccessException("User is not authorized to generate area performance report for this organization.");
            }
            _logger.LogInformation("Generating area performance report for organization {OrganizationId} from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}", organizationId, startDate, endDate);

            var ordersInDateRange = await _context.Orders
                .Where(o => o.OrganizationId == organizationId &&
                              o.Day.StartTime.Date >= startDate.Date && o.Day.StartTime.Date <= endDate.Date &&
                              o.Status != OrderStatus.PreOrder && o.Status != OrderStatus.Pending && o.Status != OrderStatus.Cancelled)
                .Include(o => o.Area)
                .ToListAsync();

            var reportContent = new StringBuilder();
            reportContent.AppendLine($"Area Performance Report - SagraFacile");
            reportContent.AppendLine($"Organization ID: {organizationId}");
            reportContent.AppendLine($"Period: {startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} to {endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            reportContent.AppendLine($"Generated At: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC");
            reportContent.AppendLine("---");

            if (!ordersInDateRange.Any())
            {
                _logger.LogInformation("No orders found for the period {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd} in organization {OrganizationId}. Generating empty report.", startDate, endDate, organizationId);
                reportContent.AppendLine("No orders found for this period.");
                return Encoding.UTF8.GetBytes(reportContent.ToString());
            }

            var areaPerformance = ordersInDateRange
                .GroupBy(o => o.Area)
                .Select(g => new
                {
                    AreaName = g.Key?.Name ?? "N/A (Area non specificata)",
                    TotalSales = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count(),
                    AverageOrderValue = g.Count() > 0 ? Math.Round(g.Sum(o => o.TotalAmount) / g.Count(), 2) : 0
                })
                .OrderBy(ap => ap.AreaName)
                .ToList();

            reportContent.AppendLine("Performance by Area:");
            foreach (var perf in areaPerformance)
            {
                reportContent.AppendLine($"- Area: {perf.AreaName}");
                reportContent.AppendLine($"  Total Sales: {perf.TotalSales.ToString("C", CultureInfo.GetCultureInfo("it-IT"))}");
                reportContent.AppendLine($"  Order Count: {perf.OrderCount}");
                reportContent.AppendLine($"  Average Order Value: {perf.AverageOrderValue.ToString("C", CultureInfo.GetCultureInfo("it-IT"))}");
                reportContent.AppendLine();
            }

            _logger.LogInformation("Successfully generated area performance report for organization {OrganizationId} from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}.", organizationId, startDate, endDate);
            return Encoding.UTF8.GetBytes(reportContent.ToString());
        }
    }
}
