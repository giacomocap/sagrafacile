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
            var organization = await _organizationService.GetOrganizationBySlugAsync(orgSlug);

            if (organization == null)
            {
                return NotFound($"Organization with slug '{orgSlug}' not found.");
            }

            return Ok(organization);
        }

        // GET /api/public/organizations/{orgSlug}/areas/{areaSlug}
        [HttpGet("organizations/{orgSlug}/areas/{areaSlug}")]
        [ProducesResponseType(typeof(AreaDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AreaDto>> GetAreaBySlugs(string orgSlug, string areaSlug)
        {
            var area = await _areaService.GetAreaBySlugsAsync(orgSlug, areaSlug);

            if (area == null)
            {
                return NotFound($"Area with slug '{areaSlug}' not found within organization '{orgSlug}'.");
            }

            return Ok(area);
        }

        // GET /api/public/areas/{areaId}/menucategories
        [HttpGet("areas/{areaId}/menucategories")]
        [ProducesResponseType(typeof(IEnumerable<MenuCategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // If area doesn't exist? Service might handle this.
        public async Task<ActionResult<IEnumerable<MenuCategoryDto>>> GetMenuCategoriesForArea(int areaId)
        {
            // Optional: Add a check to see if the Area ID exists publicly first?
            // var areaExists = await _areaService.AreaExistsAsync(areaId); // AreaExistsAsync might need adjustment for public access or a new public check method
            // if (!areaExists)
            // {
            //     return NotFound($"Area with ID {areaId} not found.");
            // }

            var categories = await _menuCategoryService.GetCategoriesByAreaAsync(areaId);
            if (categories == null)
            {
                return NotFound($"Area with ID {areaId} not found.");
            }
            return Ok(categories);
        }

        // GET /api/public/menucategories/{categoryId}/menuitems
        [HttpGet("menucategories/{categoryId}/menuitems")]
        [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // If category doesn't exist? Service might handle this.
        public async Task<ActionResult<IEnumerable<MenuItemDto>>> GetMenuItemsForCategory(int categoryId)
        {
            // Optional: Add a check to see if the Category ID exists publicly first?
            // var categoryExists = await _menuCategoryService.CategoryExistsAsync(categoryId); // CategoryExistsAsync might need adjustment
            // if (!categoryExists)
            // {
            //     return NotFound($"Menu category with ID {categoryId} not found.");
            // }

            var items = await _menuItemService.GetItemsByCategoryAsync(categoryId);
            if (items == null)
            {
                return NotFound($"Menu category with ID {categoryId} not found.");
            }
            return Ok(items);
        }

        // POST /api/public/preorders
        [HttpPost("preorders")]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // If Org/Area/Item not found
        public async Task<ActionResult<OrderDto>> CreatePreOrder([FromBody] PreOrderDto preOrderDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdOrderDto = await _orderService.CreatePreOrderAsync(preOrderDto);

                if (createdOrderDto == null)
                {
                    // This might indicate an internal server error or a handled exception during creation
                    return BadRequest("Could not create pre-order."); // Or return 500? Depends on service impl.
                }

                // Return 201 Created with the location header pointing to the standard GET order endpoint (if one exists)
                // For now, just return the created DTO with 201 status.
                // A GET endpoint for public orders might be needed later.
                return StatusCode(StatusCodes.Status201Created, createdOrderDto);

            }
            catch (KeyNotFoundException ex)
            {
                // Handle cases where Area, Organization, or MenuItem ID was not found
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // Handle validation errors like missing required notes or item not belonging to area
                return BadRequest(ex.Message);
            }
            catch (Exception ex) // Catch unexpected errors
            {
                // Log the exception (replace Console.WriteLine with proper logging)
                Console.WriteLine($"Unexpected error creating pre-order: {ex}");
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
            _logger.LogInformation("Attempting to fetch public active cashier stations for AreaId: {AreaId}", areaId);
            // Assuming _queueService has GetActiveCashierStationsForAreaAsync
            // If this method was specific to QueueService's internal logic and not just data retrieval,
            // it might need to be exposed via IQueueService or a more general service.
            var result = await _queueService.GetActiveCashierStationsForAreaAsync(areaId);

            if (!result.Success)
            {
                if (result.Errors != null && result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Public request for cashier stations failed for AreaId {AreaId}: Area not found.", areaId);
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
                return Ok(orders);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Error fetching ready-for-pickup orders for AreaId {AreaId}: {ErrorMessage}", areaId, ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching ready-for-pickup orders for AreaId {AreaId}", areaId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET: api/public/areas/{areaId}/ads
        [HttpGet("areas/{areaId}/ads")]
        [ProducesResponseType(typeof(IEnumerable<AdMediaItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<AdAreaAssignmentDto>>> GetActiveAdsForArea(int areaId)
        {
            try
            {
                var assignments = await _adAreaAssignmentService.GetAssignmentsForAreaAsync(areaId);
                return Ok(assignments);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Error fetching active ads for AreaId {AreaId}: {ErrorMessage}", areaId, ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching active ads for AreaId {AreaId}", areaId);
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
                        _logger.LogWarning("Public request for queue state failed for AreaId {AreaId}: Area not found.", areaId);
                        return NotFound(string.Join("; ", result.Errors));
                    }
                    _logger.LogError("Public request for queue state failed for AreaId {AreaId}: {Error}", areaId, string.Join("; ", result.Errors));
                    return StatusCode(StatusCodes.Status500InternalServerError, string.Join("; ", result.Errors));
                }
                return Ok(result.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching queue state for AreaId {AreaId}", areaId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
