using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models; // Added for OrderStatus enum
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Controllers
{
    [Route("api/public")]
    [ApiController]
    [AllowAnonymous] // Allow access without authentication for all methods in this controller
    public class PublicController : ControllerBase
    {
        private readonly IOrganizationService _organizationService;
        private readonly IAreaService _areaService;
        private readonly IMenuCategoryService _menuCategoryService;
        private readonly IMenuItemService _menuItemService;
        private readonly IOrderService _orderService;
        private readonly IQueueService _queueService; // Added IQueueService
        private readonly IAdAreaAssignmentService _adAreaAssignmentService;
        private readonly ILogger<PublicController> _logger; // Added ILogger

        public PublicController(
            IOrganizationService organizationService,
            IAreaService areaService,
            IMenuCategoryService menuCategoryService,
            IMenuItemService menuItemService,
            IOrderService orderService,
            IQueueService queueService, // Added IQueueService
            IAdAreaAssignmentService adAreaAssignmentService,
            ILogger<PublicController> logger) // Added ILogger
        {
            _organizationService = organizationService;
            _areaService = areaService;
            _menuCategoryService = menuCategoryService;
            _menuItemService = menuItemService;
            _orderService = orderService;
            _queueService = queueService; // Added IQueueService
            _adAreaAssignmentService = adAreaAssignmentService;
            _logger = logger; // Added ILogger
        }

        // GET /api/public/organizations/{orgSlug}
        [HttpGet("organizations/{orgSlug}")]
        [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrganizationDto>> GetOrganizationBySlug(string orgSlug)
        {
            _logger.LogInformation("Public request to get organization by slug: {OrgSlug}", orgSlug);
            try
            {
                var organization = await _organizationService.GetOrganizationBySlugAsync(orgSlug);

                if (organization == null)
                {
                    _logger.LogInformation("Organization with slug '{OrgSlug}' not found.", orgSlug);
                    return NotFound($"Organization with slug '{orgSlug}' not found.");
                }

                _logger.LogInformation("Successfully retrieved organization with slug: {OrgSlug}", orgSlug);
                return Ok(organization);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting organization by slug: {OrgSlug}", orgSlug);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET /api/public/organizations/{orgSlug}/areas/{areaSlug}
        [HttpGet("organizations/{orgSlug}/areas/{areaSlug}")]
        [ProducesResponseType(typeof(AreaDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AreaDto>> GetAreaBySlugs(string orgSlug, string areaSlug)
        {
            _logger.LogInformation("Public request to get area by slugs: OrgSlug: {OrgSlug}, AreaSlug: {AreaSlug}", orgSlug, areaSlug);
            try
            {
                var area = await _areaService.GetAreaBySlugsAsync(orgSlug, areaSlug);

                if (area == null)
                {
                    _logger.LogInformation("Area with slug '{AreaSlug}' not found within organization '{OrgSlug}'.", areaSlug, orgSlug);
                    return NotFound($"Area with slug '{areaSlug}' not found within organization '{orgSlug}'.");
                }

                _logger.LogInformation("Successfully retrieved area by slugs: OrgSlug: {OrgSlug}, AreaSlug: {AreaSlug}", orgSlug, areaSlug);
                return Ok(area);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting area by slugs: OrgSlug: {OrgSlug}, AreaSlug: {AreaSlug}", orgSlug, areaSlug);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET /api/public/areas/{areaId}/menucategories
        [HttpGet("areas/{areaId}/menucategories")]
        [ProducesResponseType(typeof(IEnumerable<MenuCategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // If area doesn't exist? Service might handle this.
        public async Task<ActionResult<IEnumerable<MenuCategoryDto>>> GetMenuCategoriesForArea(int areaId)
        {
            _logger.LogInformation("Public request to get menu categories for AreaId: {AreaId}", areaId);
            try
            {
                var categories = await _menuCategoryService.GetCategoriesByAreaAsync(areaId);
                if (categories == null)
                {
                    _logger.LogInformation("Area with ID {AreaId} not found when getting menu categories.", areaId);
                    return NotFound($"Area with ID {areaId} not found.");
                }
                _logger.LogInformation("Successfully retrieved {Count} menu categories for AreaId: {AreaId}", ((List<MenuCategoryDto>)categories).Count, areaId);
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving menu categories for AreaId: {AreaId}", areaId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET /api/public/menucategories/{categoryId}/menuitems
        [HttpGet("menucategories/{categoryId}/menuitems")]
        [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // If category doesn't exist? Service might handle this.
        public async Task<ActionResult<IEnumerable<MenuItemDto>>> GetMenuItemsForCategory(int categoryId)
        {
            _logger.LogInformation("Public request to get menu items for CategoryId: {CategoryId}", categoryId);
            try
            {
                var items = await _menuItemService.GetItemsByCategoryAsync(categoryId);
                if (items == null)
                {
                    _logger.LogInformation("Menu category with ID {CategoryId} not found when getting menu items.", categoryId);
                    return NotFound($"Menu category with ID {categoryId} not found.");
                }
                _logger.LogInformation("Successfully retrieved {Count} menu items for CategoryId: {CategoryId}", ((List<MenuItemDto>)items).Count, categoryId);
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving menu items for CategoryId: {CategoryId}", categoryId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // POST /api/public/preorders
        [HttpPost("preorders")]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // If Org/Area/Item not found
        public async Task<ActionResult<OrderDto>> CreatePreOrder([FromBody] PreOrderDto preOrderDto)
        {
            _logger.LogInformation("Public request to create pre-order for AreaId: {AreaId}, CustomerName: {CustomerName}", preOrderDto.AreaId, preOrderDto.CustomerName);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for creating public pre-order for AreaId: {AreaId}. Errors: {@Errors}", preOrderDto.AreaId, ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var createdOrderDto = await _orderService.CreatePreOrderAsync(preOrderDto);

                if (createdOrderDto == null)
                {
                    _logger.LogError("CreatePreOrderAsync returned null unexpectedly for AreaId: {AreaId}, CustomerName: {CustomerName}", preOrderDto.AreaId, preOrderDto.CustomerName);
                    // This might indicate an internal server error or a handled exception during creation
                    return BadRequest("Could not create pre-order."); // Or return 500? Depends on service impl.
                }

                _logger.LogInformation("Successfully created public pre-order with ID: {OrderId} for AreaId: {AreaId}", createdOrderDto.Id, createdOrderDto.AreaId);
                // Return 201 Created with the location header pointing to the standard GET order endpoint (if one exists)
                // For now, just return the created DTO with 201 status.
                // A GET endpoint for public orders might be needed later.
                return StatusCode(StatusCodes.Status201Created, createdOrderDto);

            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found during public pre-order creation for AreaId: {AreaId}. Error: {Error}", preOrderDto.AreaId, ex.Message);
                // Handle cases where Area, Organization, or MenuItem ID was not found
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation during public pre-order creation for AreaId: {AreaId}. Error: {Error}", preOrderDto.AreaId, ex.Message);
                // Handle validation errors like missing required notes or item not belonging to area
                return BadRequest(ex.Message);
            }
            catch (Exception ex) // Catch unexpected errors
            {
                _logger.LogError(ex, "An unexpected error occurred while creating public pre-order for AreaId: {AreaId}", preOrderDto.AreaId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while creating the pre-order.");
            }
        }

        // Other public endpoints will be added here...

        // GET /api/public/areas/{areaId}/cashier-stations
        [HttpGet("areas/{areaId}/cashier-stations")] // Corrected route relative to "api/public"
        [ProducesResponseType(typeof(List<CashierStationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPublicActiveCashierStations(int areaId)
        {
            _logger.LogInformation("Public request for active cashier stations for AreaId: {AreaId}", areaId);
            // Assuming _queueService has GetActiveCashierStationsForAreaAsync
            // If this method was specific to QueueService's internal logic and not just data retrieval,
            // it might need to be exposed via IQueueService or a more general service.
            try
            {
                var result = await _queueService.GetActiveCashierStationsForAreaAsync(areaId);

                if (!result.Success)
                {
                    if (result.Errors != null && result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogWarning("Public request for cashier stations failed for AreaId {AreaId}: Area not found. Error: {Error}", areaId, string.Join("; ", result.Errors));
                        return NotFound(string.Join("; ", result.Errors));
                    }
                    _logger.LogError("Public request for cashier stations failed for AreaId {AreaId}: {Error}", areaId, string.Join("; ", result.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, string.Join("; ", result.Errors));
                }

                if (result.Value == null)
                {
                    _logger.LogWarning("GetActiveCashierStationsForAreaAsync succeeded for AreaId {AreaId} but returned null list.", areaId);
                    return Ok(new List<CashierStationDto>());
                }

                _logger.LogInformation("Successfully fetched {Count} public active cashier stations for AreaId: {AreaId}", result.Value.Count, areaId);
                return Ok(result.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching public active cashier stations for AreaId: {AreaId}", areaId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET /api/public/areas/{areaId}/orders/ready-for-pickup
        [HttpGet("areas/{areaId}/orders/ready-for-pickup")]
        [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetReadyForPickupOrders(int areaId)
        {
            _logger.LogInformation("Public request for ready-for-pickup orders for AreaId: {AreaId}", areaId);
            try
            {
                var orders = await _orderService.GetPublicOrdersByStatusAsync(areaId, OrderStatus.ReadyForPickup);
                _logger.LogInformation("Successfully retrieved {Count} ready-for-pickup orders for AreaId: {AreaId}", ((List<OrderDto>)orders).Count, areaId);
                return Ok(orders);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found during fetching ready-for-pickup orders for AreaId {AreaId}. Error: {ErrorMessage}", areaId, ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching ready-for-pickup orders for AreaId {AreaId}", areaId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET: api/public/areas/{areaId}/ads
        [HttpGet("areas/{areaId}/ads")]
        [ProducesResponseType(typeof(IEnumerable<AdMediaItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<AdAreaAssignmentDto>>> GetActiveAdsForArea(int areaId)
        {
            _logger.LogInformation("Public request to get active ads for AreaId: {AreaId}", areaId);
            try
            {
                var assignments = await _adAreaAssignmentService.GetAssignmentsForAreaAsync(areaId);
                _logger.LogInformation("Successfully retrieved {Count} active ads for AreaId: {AreaId}", ((List<AdAreaAssignmentDto>)assignments).Count, areaId);
                return Ok(assignments);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found during fetching active ads for AreaId {AreaId}. Error: {ErrorMessage}", areaId, ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching active ads for AreaId {AreaId}", areaId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET /api/public/areas/{areaId}/queue/state
        [HttpGet("areas/{areaId}/queue/state")]
        [ProducesResponseType(typeof(QueueStateDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetQueueState(int areaId)
        {
            _logger.LogInformation("Public request for queue state for AreaId: {AreaId}", areaId);
            try
            {
                var result = await _queueService.GetQueueStateAsync(areaId);
                if (!result.Success)
                {
                    if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogWarning("Public request for queue state failed for AreaId {AreaId}: Area not found. Error: {Error}", areaId, string.Join("; ", result.Errors));
                        return NotFound(string.Join("; ", result.Errors));
                    }
                    _logger.LogError("Public request for queue state failed for AreaId {AreaId}: {Error}", areaId, string.Join("; ", result.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, string.Join("; ", result.Errors));
                }
                _logger.LogInformation("Successfully retrieved queue state for AreaId: {AreaId}", areaId);
                return Ok(result.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching queue state for AreaId: {AreaId}", areaId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
