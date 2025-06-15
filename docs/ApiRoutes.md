# SagraFacile API Routes

This document lists the available API routes based on the controllers.

## Accounts (`/api/Accounts`)

*   `POST /api/Accounts/register` - Register a new user.
*   `POST /api/Accounts/login` - Log in a user and receive a JWT.
*   `POST /api/Accounts/assign-role` - Assign a role to a user (SuperAdmin only).
*   `POST /api/Accounts/unassign-role` - Unassign a role from a user (SuperAdmin only).
*   `GET /api/Accounts` - Get users for the current organization (Admin) or all users (SuperAdmin).
*   `GET /api/Accounts/{userId}` - Get a specific user by ID (with multi-tenancy checks).
*   `PUT /api/Accounts/{userId}` - Update a specific user (with multi-tenancy checks).
*   `DELETE /api/Accounts/{userId}` - Delete a specific user (with multi-tenancy checks).
*   `GET /api/Accounts/roles` - Get all available roles.
*   `POST /api/Accounts/roles` - Create a new role (SuperAdmin only).

## Areas (`/api/Areas`)

*   `GET /api/Areas` - Get all areas accessible by the user.
*   `GET /api/Areas/{id}` - Get a specific area by ID.
*   `POST /api/Areas` - Create a new area.
*   `PUT /api/Areas/{id}` - Update an existing area.
*   `DELETE /api/Areas/{id}` - Delete an area.

## Menu Categories (`/api/MenuCategories`)

*   `GET /api/MenuCategories?areaId={areaId}` - Get all menu categories for a specific area.
*   `GET /api/MenuCategories/{id}` - Get a specific menu category by ID.
*   `POST /api/MenuCategories` - Create a new menu category.
*   `PUT /api/MenuCategories/{id}` - Update an existing menu category.
*   `DELETE /api/MenuCategories/{id}` - Delete a menu category.

## Menu Items (`/api/MenuItems`)

*   `GET /api/MenuItems?categoryId={categoryId}` - Get all menu items for a specific category.
*   `GET /api/MenuItems/{id}` - Get a specific menu item by ID.
*   `POST /api/MenuItems` - Create a new menu item.
*   `PUT /api/MenuItems/{id}` - Update an existing menu item.
*   `DELETE /api/MenuItems/{id}` - Delete a menu item.

## Orders (`/api/Orders`)

*   `POST /api/Orders` - Create a new order.
*   `GET /api/Orders/{id}` - Get a specific order by ID.
*   `GET /api/Orders?organizationId={orgId}&areaId={areaId}` - Get orders, filtered by optional `organizationId` (required for SuperAdmin) and optional `areaId`.
*   `PUT /api/Orders/{orderId}/confirm-preparation` - Confirm an order (Paid/PreOrder) and assign a table number (Waiter/Admin roles).
*   `GET /api/Orders/kds-station/{kdsStationId}` - Get active orders relevant to a specific KDS station (Preparer/Admin roles).
*   `PUT /api/Orders/{orderId}/items/{orderItemId}/kds-status` - Update the KDS status of a specific order item (Preparer/Admin roles).

## KDS Stations (`/api/organizations/{organizationId}/areas/{areaId}/kds-stations`)

*   `GET /` - List KDS stations for the specified area (Admin roles).
*   `POST /` - Create a new KDS station for the area (Admin/SuperAdmin roles).
*   `GET /{kdsStationId}` - Get details of a specific KDS station (Admin roles).
*   `PUT /{kdsStationId}` - Update a KDS station's name (Admin/SuperAdmin roles).
*   `DELETE /{kdsStationId}` - Delete a KDS station (Admin/SuperAdmin roles).
*   `GET /{kdsStationId}/categories` - List menu categories assigned to the KDS station (Admin roles).
*   `POST /{kdsStationId}/categories/{menuCategoryId}` - Assign a menu category to the KDS station (Admin/SuperAdmin roles).
*   `DELETE /{kdsStationId}/categories/{menuCategoryId}` - Unassign a menu category from the KDS station (Admin/SuperAdmin roles).

## Organizations (`/api/Organizations`)

*   `GET /api/Organizations` - Get all organizations (SuperAdmin only).
*   `GET /api/Organizations/{id}` - Get a specific organization by ID (SuperAdmin only).
*   `POST /api/Organizations` - Create a new organization (SuperAdmin only).
*   `PUT /api/Organizations/{id}` - Update an existing organization (SuperAdmin only).
*   `DELETE /api/Organizations/{id}` - Delete an organization (SuperAdmin only).

## Public (`/api/public`) - No Authentication Required

*   `GET /api/public/organizations/{orgSlug}` - Get public details for an organization by its slug.
*   `GET /api/public/organizations/{orgSlug}/areas/{areaSlug}` - Get public details for an area by its organization and area slugs.
*   `GET /api/public/areas/{areaId}/menucategories` - Get menu categories for a specific area ID.
*   `GET /api/public/menucategories/{categoryId}/menuitems` - Get menu items for a specific category ID.
*   `POST /api/public/preorders` - Submit a new pre-order.
