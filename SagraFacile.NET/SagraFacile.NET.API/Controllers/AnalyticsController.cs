using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs.Analytics; // Added for DTOs
using SagraFacile.NET.API.Services;
using System;
using System.Collections.Generic; // Added for List results
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
// using Microsoft.Extensions.Logging; // Removed, assuming logging is in service layer

namespace SagraFacile.NET.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")] // Protect all analytics endpoints
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        // private readonly ILogger<AnalyticsController> _logger; // Removed

        public AnalyticsController(IAnalyticsService analyticsService) // Removed ILogger
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            // _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Removed
        }

        // Dashboard KPIs
        [HttpGet("dashboard/kpis")]
        [ProducesResponseType(typeof(DashboardKPIsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DashboardKPIsDto>> GetDashboardKPIs([FromQuery, Required] int organizationId, [FromQuery] int? dayId = null)
        {
            try
            {
                var kpis = await _analyticsService.GetDashboardKPIsAsync(organizationId, dayId);
                return Ok(kpis);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception) // General exception
            {
                // Consider logging the specific exception in the service layer
                return StatusCode(500, "An error occurred while fetching dashboard KPIs.");
            }
        }

        [HttpGet("dashboard/sales-trend")]
        [ProducesResponseType(typeof(List<SalesTrendDataDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<SalesTrendDataDto>>> GetSalesTrend([FromQuery, Required] int organizationId, [FromQuery] int days = 7)
        {
            try
            {
                var salesTrend = await _analyticsService.GetSalesTrendAsync(organizationId, days);
                return Ok(salesTrend);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while fetching sales trend.");
            }
        }

        [HttpGet("dashboard/order-status")]
        [ProducesResponseType(typeof(List<OrderStatusDistributionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<OrderStatusDistributionDto>>> GetOrderStatusDistribution([FromQuery, Required] int organizationId, [FromQuery] int? dayId = null)
        {
            try
            {
                var orderStatus = await _analyticsService.GetOrderStatusDistributionAsync(organizationId, dayId);
                return Ok(orderStatus);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while fetching order status distribution.");
            }
        }

        [HttpGet("dashboard/top-menu-items")]
        [ProducesResponseType(typeof(List<TopMenuItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<TopMenuItemDto>>> GetTopMenuItems([FromQuery, Required] int organizationId, [FromQuery] int days = 7, [FromQuery] int limit = 5)
        {
            try
            {
                var topItems = await _analyticsService.GetTopMenuItemsAsync(organizationId, days, limit);
                return Ok(topItems);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while fetching top menu items.");
            }
        }

        // Orders Analytics
        [HttpGet("orders/by-hour")]
        [ProducesResponseType(typeof(List<OrdersByHourDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<OrdersByHourDto>>> GetOrdersByHour([FromQuery, Required] int organizationId, [FromQuery] int? areaId = null, [FromQuery] int? dayId = null)
        {
            try
            {
                var ordersByHour = await _analyticsService.GetOrdersByHourAsync(organizationId, areaId, dayId);
                return Ok(ordersByHour);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while fetching orders by hour.");
            }
        }

        [HttpGet("orders/payment-methods")]
        [ProducesResponseType(typeof(List<PaymentMethodDistributionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<PaymentMethodDistributionDto>>> GetPaymentMethodDistribution([FromQuery, Required] int organizationId, [FromQuery] int? areaId = null, [FromQuery] int? dayId = null)
        {
            try
            {
                var paymentMethods = await _analyticsService.GetPaymentMethodDistributionAsync(organizationId, areaId, dayId);
                return Ok(paymentMethods);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while fetching payment method distribution.");
            }
        }

        [HttpGet("orders/average-value-trend")]
        [ProducesResponseType(typeof(List<AverageOrderValueTrendDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<AverageOrderValueTrendDto>>> GetAverageOrderValueTrend([FromQuery, Required] int organizationId, [FromQuery] int? areaId = null, [FromQuery] int days = 7)
        {
            try
            {
                var aovTrend = await _analyticsService.GetAverageOrderValueTrendAsync(organizationId, areaId, days);
                return Ok(aovTrend);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while fetching average order value trend.");
            }
        }

        // Reports
        [HttpGet("reports/daily-summary")]
        [ProducesResponseType(StatusCodes.Status200OK)] // Returns FileResult
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateDailySummaryReport([FromQuery, Required] int organizationId, [FromQuery, Required] int dayId)
        {
            try
            {
                var reportBytes = await _analyticsService.GenerateDailySummaryReportAsync(organizationId, dayId);
                if (reportBytes == null || reportBytes.Length == 0)
                {
                    return NotFound("Report data not found or empty.");
                }
                return File(reportBytes, "application/pdf", $"DailySummary_Org{organizationId}_Day{dayId}.pdf");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while generating daily summary report.");
            }
        }

        [HttpGet("reports/area-performance")]
        [ProducesResponseType(StatusCodes.Status200OK)] // Returns FileResult
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateAreaPerformanceReport([FromQuery, Required] int organizationId, [FromQuery, Required] DateTime startDate, [FromQuery, Required] DateTime endDate)
        {
            if (startDate > endDate)
            {
                return BadRequest("Start date cannot be after end date.");
            }
            try
            {
                var reportBytes = await _analyticsService.GenerateAreaPerformanceReportAsync(organizationId, startDate, endDate);
                 if (reportBytes == null || reportBytes.Length == 0)
                {
                    return NotFound("Report data not found or empty.");
                }
                return File(reportBytes, "application/pdf", $"AreaPerformance_Org{organizationId}_{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.pdf");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while generating area performance report.");
            }
        }
    }
}
