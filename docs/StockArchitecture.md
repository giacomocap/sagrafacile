# Stock (Scorta) Management Architecture

## 1. Overview

This document outlines the architecture for implementing a stock management feature, referred to as "Scorta," within the SagraFacile system. The primary goal is to track the quantity of menu items, allow administrators to manage stock levels, and provide real-time stock visibility to cashiers, preventing sales of out-of-stock items.

## 2. Backend Changes (SagraFacile.NET API)

### 2.1. Database Model Updates

*   **`SagraFacile.NET/SagraFacile.NET.API/Models/MenuItem.cs`**:
    *   Add a new nullable integer property:
        ```csharp
        public int? Scorta { get; set; } // null = unlimited, integer = available quantity
        ```
*   **EF Core Migration**:
    *   Generate a migration (e.g., `AddScortaToMenuItem`).
    *   Apply the migration (`dotnet ef database update`).

### 2.2. DTO Updates

*   **`SagraFacile.NET/SagraFacile.NET.API/DTOs/MenuItemDto.cs`**:
    *   Add: `public int? Scorta { get; set; }`
*   **`SagraFacile.NET/SagraFacile.NET.API/DTOs/MenuItemUpsertDto.cs`**:
    *   Add: `public int? Scorta { get; set; }`
*   **New DTO for SignalR Broadcast**:
    *   `StockUpdateBroadcastDto.cs`:
        ```csharp
        public class StockUpdateBroadcastDto
        {
            public int MenuItemId { get; set; }
            public int AreaId { get; set; } // To help frontend target updates
            public int? NewScorta { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }
        ```

### 2.3. Service Layer

#### 2.3.1. `MenuItemService` (or equivalent)

*   Modify `CreateMenuItemAsync` and `UpdateMenuItemAsync` to accept and save the `Scorta` value from `MenuItemUpsertDto`.

#### 2.3.2. New Stock Management Methods (in `MenuItemService` or a new `StockService`)

*   `Task<ServiceResult> UpdateStockAsync(int menuItemId, int? newScorta, ClaimsPrincipal user)`:
    *   Updates `Scorta` for a single `MenuItem`.
    *   Authorization: Requires Admin/OrgAdmin.
    *   Broadcasts `StockUpdateBroadcastDto` via SignalR.
*   `Task<ServiceResult> ResetStockAsync(int menuItemId, ClaimsPrincipal user)`:
    *   Sets `Scorta` to `null` for a single `MenuItem`.
    *   Authorization: Requires Admin/OrgAdmin.
    *   Broadcasts `StockUpdateBroadcastDto` via SignalR.
*   `Task<ServiceResult> ResetAllStockForAreaAsync(int areaId, ClaimsPrincipal user)`:
    *   Sets `Scorta` to `null` for all `MenuItem` entities within the specified `Area`.
    *   Authorization: Requires Admin/OrgAdmin.
    *   Broadcasts multiple `StockUpdateBroadcastDto` (or a summary event) via SignalR.

### 2.4. API Controller (`MenuItemsController` or new `StockController`)

*   **Existing `MenuItemsController`**:
    *   Ensure `POST /api/menuitems` and `PUT /api/menuitems/{id}` correctly handle the `Scorta` field from `MenuItemUpsertDto`.
*   **New Endpoints for Stock Management**:
    *   `PUT /api/menuitems/{menuItemId}/stock`
        *   Accepts: `{ "newScorta": int? }`
        *   Calls: `UpdateStockAsync`
        *   Authorization: Admin/OrgAdmin
    *   `POST /api/menuitems/{menuItemId}/stock/reset`
        *   Calls: `ResetStockAsync`
        *   Authorization: Admin/OrgAdmin
    *   `POST /api/areas/{areaId}/stock/reset-all`
        *   Calls: `ResetAllStockForAreaAsync`
        *   Authorization: Admin/OrgAdmin

### 2.5. Order Processing Logic (`OrderService.cs`)

*   **Modify `CreateOrderAsync` and `ConfirmPreOrderPaymentAsync`**:
    *   **Stock Check (Before adding/confirming `OrderItem`):**
        *   Fetch `MenuItem` including `Scorta`.
        *   If `Scorta` is not `null`:
            *   If `Scorta < itemDto.Quantity`: Throw `InvalidOperationException` (e.g., "Item '{MenuItemName}' is out of stock or insufficient quantity available.").
    *   **Stock Decrement (Transactionally, before saving order):**
        *   For each `OrderItem` with a `MenuItem` having non-null `Scorta`:
            *   `menuItem.Scorta -= orderItem.Quantity;`
            *   Update `MenuItem` in the database.
    *   **Concurrency:** Rely on EF Core's default optimistic concurrency for now.
    *   **SignalR Broadcast for Stock Updates:**
        *   After successful stock decrement, inject `IHubContext<OrderHub>`.
        *   Broadcast `"ReceiveStockUpdate"` with `StockUpdateBroadcastDto` to `Area-{areaId}` group.

### 2.6. SignalR (`OrderHub.cs`)

*   Ensure clients (Cashier UI) can join/leave `Area-{areaId}` groups to receive `ReceiveStockUpdate` messages. (This group structure likely already exists for other features).

## 3. Frontend Changes (sagrafacile-webapp)

### 3.1. Type Updates (`src/types/index.ts`)

*   Add `scorta?: number | null;` to `MenuItemDto`.
*   Create `StockUpdateBroadcastDto` (matching backend).

### 3.2. Admin UI

*   **Menu Item Management (e.g., `.../admin/menu-items/...` forms):**
    *   Input field for `Scorta` (number, allows empty for `null`).
    *   Display current `Scorta` (or "Unlimited") in list views.
    *   "Reset Stock" button for individual items (calls `POST /api/menuitems/{menuItemId}/stock/reset`).
*   **New Stock Management Section/Page (e.g., under Area Admin):**
    *   UI to call `POST /api/areas/{areaId}/stock/reset-all`.
    *   (Optional) Table view of items with current stock, allowing quick updates via `PUT /api/menuitems/{menuItemId}/stock`.

### 3.3. Cashier Interface (`.../cashier/area/[areaId]/page.tsx` & `CashierMenuPanel.tsx`)

*   **Display Stock:**
    *   Show `scorta` for each item in `CashierMenuPanel` (e.g., "Scorta: 5", "Scorta: Illimitata", "Esaurito").
    *   Use distinct visual cues (badge, color).
*   **Interaction based on Stock:**
    *   **Client-side pre-check (in `handleAddItem`):** If `menuItem.scorta !== null && menuItem.scorta < (currentQuantityInCart + 1)`, show toast warning and prevent adding more. Backend will be the final authority.
    *   Visually disable/indicate out-of-stock items.
*   **Real-time Stock Updates:**
    *   In `CashierPage.tsx`, use `useSignalRHub` to listen for `"ReceiveStockUpdate"`.
    *   On message receipt, update `scorta` in the local `menuItems` state, triggering re-renders.

### 3.4. API Client (`src/services/apiClient.ts` or new `stockService.ts`)

*   Add functions for new stock management API endpoints.

## 4. UX Considerations & Specific Scenarios

### 4.1. When to Update Stock Display in Cashier UI?

*   **Backend Decrement:** Transactionally during `CreateOrderAsync` and `ConfirmPreOrderPaymentAsync`.
*   **Frontend Update:**
    *   Immediately for the active cashier upon their own successful order submission.
    *   Real-time for all connected cashiers in the same area via SignalR (`"ReceiveStockUpdate"` message).

### 4.2. Handling Pre-orders with Unavailable Items

*   **No Stock Decrement at Initial Pre-order:** Stock is not reserved or decremented when a customer places a pre-order online.
*   **Stock Check at Confirmation (Cashier Interface):**
    *   When a cashier loads a pre-order for payment (`ConfirmPreOrderPaymentAsync` on backend):
        *   Backend performs a stock check against *current* `Scorta`.
        *   If insufficient: Backend throws `InvalidOperationException` with details.
        *   Frontend (`CashierPage.tsx`) catches this API error.
    *   **UX on Frontend (Cashier):**
        1.  Display clear error toast (e.g., "Item 'X' esaurito. Disponibili: Y, Richiesti: Z.").
        2.  Pre-order is *not* confirmed.
        3.  Cashier informs customer, modifies cart (remove/reduce item, suggest substitute).
        4.  Cashier re-attempts confirmation of the modified pre-order.

## 5. Future Considerations (Optional)

*   Low stock alerts for administrators.
*   Stock movement ledger/history.
*   Integration with purchase orders/replenishment.
