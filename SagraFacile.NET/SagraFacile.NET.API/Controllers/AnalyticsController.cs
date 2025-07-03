using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs.Analytics; // Added for DTOs
using SagraFacile.NET.API.Services;
using System;
using System.Collections.Generic; // Added for List results
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")] // Protect all analytics endpoints
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<AnalyticsController> _logger; // Added

        public AnalyticsController(IAnalyticsService analyticsService, ILogger<AnalyticsController> logger) // Added ILogger
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Added
        }

        // Dashboard KPIs
        [HttpGet("dashboard/kpis")]
        [ProducesResponseType(typeof(DashboardKPIsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DashboardKPIsDto>> GetDashboardKPIs([FromQuery, Required] Guid organizationId, [FromQuery] int? dayId = null)
        {
            _logger.LogInformation("Received request to get dashboard KPIs for OrganizationId: {OrganizationId}, DayId: {DayId}", organizationId, dayId);
            try
            {
                var kpis = await _analyticsService.GetDashboardKPIsAsync(organizationId, dayId);
                _logger.LogInformation("Successfully retrieved dashboard KPIs for OrganizationId: {OrganizationId}", organizationId);
                return Ok(kpis);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get dashboard KPIs for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex) // General exception
            {
                _logger.LogError(ex, "An error occurred while fetching dashboard KPIs for OrganizationId: {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while fetching dashboard KPIs.");
            }
        }

        [HttpGet("dashboard/sales-trend")]
        [ProducesResponseType(typeof(List<SalesTrendDataDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<SalesTrendDataDto>>> GetSalesTrend([FromQuery, Required] Guid organizationId, [FromQuery] int days = 7)
        {
            _logger.LogInformation("Received request to get sales trend for OrganizationId: {OrganizationId}, Days: {Days}", organizationId, days);
            try
            {
                var salesTrend = await _analyticsService.GetSalesTrendAsync(organizationId, days);
                _logger.LogInformation("Successfully retrieved sales trend for OrganizationId: {OrganizationId}", organizationId);
                return Ok(salesTrend);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get sales trend for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching sales trend for OrganizationId: {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while fetching sales trend.");
            }
        }

        [HttpGet("dashboard/order-status")]
        [ProducesResponseType(typeof(List<OrderStatusDistributionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<OrderStatusDistributionDto>>> GetOrderStatusDistribution([FromQuery, Required] Guid organizationId, [FromQuery] int? dayId = null)
        {
            _logger.LogInformation("Received request to get order status distribution for OrganizationId: {OrganizationId}, DayId: {DayId}", organizationId, dayId);
            try
            {
                var orderStatus = await _analyticsService.GetOrderStatusDistributionAsync(organizationId, dayId);
                _logger.LogInformation("Successfully retrieved order status distribution for OrganizationId: {OrganizationId}", organizationId);
                return Ok(orderStatus);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get order status distribution for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching order status distribution for OrganizationId: {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while fetching order status distribution.");
            }
        }

        [HttpGet("dashboard/top-menu-items")]
        [ProducesResponseType(typeof(List<TopMenuItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<TopMenuItemDto>>> GetTopMenuItems([FromQuery, Required] Guid organizationId, [FromQuery] int days = 7, [FromQuery] int limit = 5)
        {
            _logger.LogInformation("Received request to get top menu items for OrganizationId: {OrganizationId}, Days: {Days}, Limit: {Limit}", organizationId, days, limit);
            try
            {
                var topItems = await _analyticsService.GetTopMenuItemsAsync(organizationId, days, limit);
                _logger.LogInformation("Successfully retrieved top menu items for OrganizationId: {OrganizationId}", organizationId);
                return Ok(topItems);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get top menu items for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching top menu items for OrganizationId: {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while fetching top menu items.");
            }
        }

        // Orders Analytics
        [HttpGet("orders/by-hour")]
        [ProducesResponseType(typeof(List<OrdersByHourDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<OrdersByHourDto>>> GetOrdersByHour([FromQuery, Required] Guid organizationId, [FromQuery] int? areaId = null, [FromQuery] int? dayId = null)
        {
            _logger.LogInformation("Received request to get orders by hour for OrganizationId: {OrganizationId}, AreaId: {AreaId}, DayId: {DayId}", organizationId, areaId, dayId);
            try
            {
                var ordersByHour = await _analyticsService.GetOrdersByHourAsync(organizationId, areaId, dayId);
                _logger.LogInformation("Successfully retrieved orders by hour for OrganizationId: {OrganizationId}", organizationId);
                return Ok(ordersByHour);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get orders by hour for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Orders by hour data not found for OrganizationId: {OrganizationId}, AreaId: {AreaId}, DayId: {DayId}", organizationId, areaId, dayId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching orders by hour for OrganizationId: {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while fetching orders by hour.");
            }
        }

        [HttpGet("orders/payment-methods")]
        [ProducesResponseType(typeof(List<PaymentMethodDistributionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<PaymentMethodDistributionDto>>> GetPaymentMethodDistribution([FromQuery, Required] Guid organizationId, [FromQuery] int? areaId = null, [FromQuery] int? dayId = null)
        {
            _logger.LogInformation("Received request to get payment method distribution for OrganizationId: {OrganizationId}, AreaId: {AreaId}, DayId: {DayId}", organizationId, areaId, dayId);
            try
            {
                var paymentMethods = await _analyticsService.GetPaymentMethodDistributionAsync(organizationId, areaId, dayId);
                _logger.LogInformation("Successfully retrieved payment method distribution for OrganizationId: {OrganizationId}", organizationId);
                return Ok(paymentMethods);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get payment method distribution for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Payment method distribution data not found for OrganizationId: {OrganizationId}, AreaId: {AreaId}, DayId: {DayId}", organizationId, areaId, dayId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching payment method distribution for OrganizationId: {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while fetching payment method distribution.");
            }
        }

        [HttpGet("orders/average-value-trend")]
        [ProducesResponseType(typeof(List<AverageOrderValueTrendDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<AverageOrderValueTrendDto>>> GetAverageOrderValueTrend([FromQuery, Required] Guid organizationId, [FromQuery] int? areaId = null, [FromQuery] int days = 7)
        {
            _logger.LogInformation("Received request to get average order value trend for OrganizationId: {OrganizationId}, AreaId: {AreaId}, Days: {Days}", organizationId, areaId, days);
            try
            {
                var aovTrend = await _analyticsService.GetAverageOrderValueTrendAsync(organizationId, areaId, days);
                _logger.LogInformation("Successfully retrieved average order value trend for OrganizationId: {OrganizationId}", organizationId);
                return Ok(aovTrend);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get average order value trend for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Average order value trend data not found for OrganizationId: {OrganizationId}, AreaId: {AreaId}, Days: {Days}", organizationId, areaId, days);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching average order value trend for OrganizationId: {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while fetching average order value trend.");
            }
        }

        // Reports
        [HttpGet("reports/daily-summary")]
        [ProducesResponseType(StatusCodes.Status200OK)] // Returns FileResult
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateDailySummaryReport([FromQuery, Required] Guid organizationId, [FromQuery, Required] int dayId)
        {
            _logger.LogInformation("Received request to generate daily summary report for OrganizationId: {OrganizationId}, DayId: {DayId}", organizationId, dayId);
            try
            {
                var reportBytes = await _analyticsService.GenerateDailySummaryReportAsync(organizationId, dayId);
                if (reportBytes == null || reportBytes.Length == 0)
                {
                    _logger.LogWarning("Daily summary report data not found or empty for OrganizationId: {OrganizationId}, DayId: {DayId}", organizationId, dayId);
                    return NotFound("Report data not found or empty.");
                }
                _logger.LogInformation("Successfully generated daily summary report for OrganizationId: {OrganizationId}, DayId: {DayId}", organizationId, dayId);
                return File(reportBytes, "application/pdf", $"DailySummary_Org{organizationId}_Day{dayId}.pdf");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to generate daily summary report for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while generating daily summary report for OrganizationId: {OrganizationId}, DayId: {DayId}", organizationId, dayId);
                return StatusCode(500, "An error occurred while generating daily summary report.");
            }
        }

        [HttpGet("reports/area-performance")]
        [ProducesResponseType(StatusCodes.Status200OK)] // Returns FileResult
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateAreaPerformanceReport([FromQuery, Required] Guid organizationId, [FromQuery, Required] DateTime startDate, [FromQuery, Required] DateTime endDate)
        {
            _logger.LogInformation("Received request to generate area performance report for OrganizationId: {OrganizationId}, StartDate: {StartDate}, EndDate: {EndDate}", organizationId, startDate, endDate);
            if (startDate > endDate)
            {
                _logger.LogWarning("Bad request: Start date {StartDate} cannot be after end date {EndDate} for OrganizationId: {OrganizationId}", startDate, endDate, organizationId);
                return BadRequest("Start date cannot be after end date.");
            }
            try
            {
                var reportBytes = await _analyticsService.GenerateAreaPerformanceReportAsync(organizationId, startDate, endDate);
                 if (reportBytes == null || reportBytes.Length == 0)
                {
                    _logger.LogWarning("Area performance report data not found or empty for OrganizationId: {OrganizationId}, StartDate: {StartDate}, EndDate: {EndDate}", organizationId, startDate, endDate);
                    return NotFound("Report data not found or empty.");
                }
                _logger.LogInformation("Successfully generated area performance report for OrganizationId: {OrganizationId}, StartDate: {StartDate}, EndDate: {EndDate}", organizationId, startDate, endDate);
                return File(reportBytes, "application/pdf", $"AreaPerformance_Org{organizationId}_{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.pdf");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to generate area performance report for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while generating area performance report for OrganizationId: {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while generating area performance report.");
            }
        }
    }
}
