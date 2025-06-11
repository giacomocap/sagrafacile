# SagraFacile - Configurable Order Workflow Architecture

This document outlines the architecture for the configurable order status workflow in SagraFacile. The goal is to allow administrators to tailor the order lifecycle based on the operational needs of specific Areas (stands/cashier points), particularly concerning the involvement of Waiters and the Kitchen Display System (KDS).

## 1. Order Status Enum

The defined states for an order:

```csharp
public enum OrderStatus
{
    PreOrder,       // Order placed via public interface, not yet confirmed/paid
    Pending,        // Order created by cashier, not yet paid/processed (Less used now, primarily transitions to Paid)
    Paid,           // Order paid at cashier or pre-order confirmed
    Preparing,      // Order accepted for preparation (either directly after Paid or after Waiter confirmation)
    ReadyForPickup, // All items for the order are marked as ready by KDS stations (or skipped if KDS not enabled)
    Completed,      // Order picked up/served (either directly after ReadyForPickup or after explicit confirmation)
    Cancelled       // Order cancelled
}
```

## 2. Configuration Flags

To control the workflow, the following boolean flags will be added to the `Area` entity (`SagraFacile.NET.API/Models/Area.cs`):

*   **`EnableWaiterConfirmation` (bool, default: `false`)**:
    *   If `true`, an order transitions from `Paid` to `Preparing` *only* after a Waiter scans the order QR code and confirms it via the Waiter Interface (`PUT /api/orders/{orderId}/confirm-preparation`). Comandas are typically printed *after* this step at station printers (see `PrinterArchitecture.md`).
    *   If `false`, an order transitions *automatically* from `Paid` to `Preparing`. Comandas are typically printed *immediately* after payment, either at the cashier or at stations (see `PrinterArchitecture.md`).
*   **`EnableKds` (bool, default: `false`)**:
    *   If `true`, an order transitions from `Preparing` to `ReadyForPickup` *only* after all its items have been marked as completed across all relevant KDS stations and the final KDS confirmation action (`PUT /api/orders/{orderId}/kds-confirm-complete/...`) is triggered for all associated stations.
    *   If `false`, an order transitions *automatically* from `Preparing` to `ReadyForPickup`.
*   **`EnableCompletionConfirmation` (bool, default: `false`)**:
    *   If `true`, an order transitions from `ReadyForPickup` to `Completed` *only* after an explicit confirmation step (e.g., final scan at pickup point, dedicated UI action - **Requires defining the trigger mechanism/UI**).
    *   If `false`, an order transitions *automatically* from `ReadyForPickup` to `Completed`.

*(Note: `EnablePreOrder` might be better suited as an Organization-level or system-level setting rather than per-Area).*

## 3. State Transition Logic

The core logic resides primarily within the `OrderService` in the backend. Key transition points are:

| Event Triggering Change                 | Current Status | `EnableWaiterConfirmation` | `EnableKds` | `EnableCompletionConfirmation` | Next Status      | Print Action Triggered (See `PrinterArchitecture.md`)                     |
| :-------------------------------------- | :------------- | :------------------------- | :---------- | :----------------------------- | :--------------- | :---------------------------------------------------------------------- |
| Cashier Payment / PreOrder Confirmation | `PreOrder`     | True                       | -           | -                              | `Paid`           | `Receipt`. `Comanda` (If `Station.PrintComandasAtThisStation` or `Area.PrintComandasAtCashier` is true). |
| Cashier Payment / PreOrder Confirmation | `PreOrder`     | False                      | True        | -                              | `Preparing`      | `Receipt`. `Comanda` (If `Station.PrintComandasAtThisStation` or `Area.PrintComandasAtCashier` is true). |
| Cashier Payment / PreOrder Confirmation | `PreOrder`     | False                      | False       | True                           | `ReadyForPickup` | `Receipt`. `Comanda` (If `Station.PrintComandasAtThisStation` or `Area.PrintComandasAtCashier` is true). |
| Cashier Payment / PreOrder Confirmation | `PreOrder`     | False                      | False       | False                          | `Completed`      | `Receipt`. `Comanda` (If `Station.PrintComandasAtThisStation` or `Area.PrintComandasAtCashier` is true). |
| Cashier Payment / PreOrder Confirmation | `Pending`      | True                       | -           | -                              | `Paid`           | `Receipt`. `Comanda` (If `Station.PrintComandasAtThisStation` or `Area.PrintComandasAtCashier` is true). |
| Cashier Payment / PreOrder Confirmation | `Pending`      | False                      | True        | -                              | `Preparing`      | `Receipt`. `Comanda` (If `Station.PrintComandasAtThisStation` or `Area.PrintComandasAtCashier` is true). |
| Cashier Payment / PreOrder Confirmation | `Pending`      | False                      | False       | True                           | `ReadyForPickup` | `Receipt`. `Comanda` (If `Station.PrintComandasAtThisStation` or `Area.PrintComandasAtCashier` is true). |
| Cashier Payment / PreOrder Confirmation | `Pending`      | False                      | False       | False                          | `Completed`      | `Receipt`. `Comanda` (If `Station.PrintComandasAtThisStation` or `Area.PrintComandasAtCashier` is true). |
| Waiter Confirmation Scan                | `Paid`         | True                       | True        | -                              | `Preparing`      | `Comanda` (If not already printed at creation/payment AND workflow now requires it for `Preparing` status via category/station assignment). |
| Waiter Confirmation Scan                | `Paid`         | True                       | False       | True                           | `ReadyForPickup` | `Comanda` (If not already printed at creation/payment AND workflow now requires it for `ReadyForPickup` status via category/station assignment). |
| Waiter Confirmation Scan                | `Paid`         | True                       | False       | False                          | `Completed`      | `Comanda` (If not already printed at creation/payment AND workflow now requires it for `Completed` status via category/station assignment). |
| KDS Stations Confirm Completion         | `Preparing`    | -                          | True        | True                           | `ReadyForPickup` | *None*                                                                  |
| KDS Stations Confirm Completion         | `Preparing`    | -                          | True        | False                          | `Completed`      | *None*                                                                  |
| Final Pickup Confirmation (New Event)   | `ReadyForPickup` | -                          | -           | True                           | `Completed`      | *None*                                                                  |

**Notes:**

*   Transitions where flags are `false` happen automatically within the same service call that triggers the preceding status.
*   The flags `CashierStation.PrintComandasAtThisStation` and `Area.PrintComandasAtCashier` (defined in `PrinterArchitecture.md`) determine if a comanda is printed *immediately* upon order creation/payment confirmation by a cashier. 
*   If `EnableWaiterConfirmation` is `true`, and a comanda was *not* printed initially (due to the above flags being false), then a comanda print is triggered by the Waiter Confirmation Scan if the resulting order status (e.g., `Preparing`, `ReadyForPickup`, `Completed`) logically requires items to be made. This avoids duplicate comanda prints.
*   If `EnableCompletionConfirmation` is `true`, a new backend endpoint and corresponding frontend trigger (e.g., button in Waiter UI, dedicated pickup station UI) will be required for the "Final Pickup Confirmation" event.

## 4. Example Scenarios

*   **Scenario A: Simple Cashier -> Kitchen (No Waiter, No KDS)**
    *   `Area` Config: `EnableWaiterConfirmation=false`, `EnableKds=false`, `EnableCompletionConfirmation=false`
    *   Flow: `PreOrder`/`Pending` -> (Payment) -> `Completed`
    *   Printing: Receipt + Comandas printed immediately after payment (location depends on `PrintComandasAtCashier`).
*   **Scenario B: Cashier -> Waiter -> Kitchen Stations (No KDS)**
    *   `Area` Config: `EnableWaiterConfirmation=true`, `EnableKds=false`, `EnableCompletionConfirmation=false`
    *   Flow: `PreOrder`/`Pending` -> (Payment) -> `Paid` -> (Waiter Scan) -> `Completed`
    *   Printing: Receipt after payment. Comandas after Waiter Scan (typically at stations, assuming `PrintComandasAtCashier=false`).
*   **Scenario C: Cashier -> Waiter -> KDS -> Pickup Point**
    *   `Area` Config: `EnableWaiterConfirmation=true`, `EnableKds=true`, `EnableCompletionConfirmation=true`
    *   Flow: `PreOrder`/`Pending` -> (Payment) -> `Paid` -> (Waiter Scan) -> `Preparing` -> (KDS Completion) -> `ReadyForPickup` -> (Pickup Confirmation) -> `Completed`
    *   Printing: Receipt after payment. Comandas after Waiter Scan (typically at stations).

## 5. Implementation Details

*   **Backend:**
    *   Add flags to `Area` model and create/apply migration.
    *   Modify `OrderService` methods (`CreateOrderAsync`, `ConfirmPreOrderPaymentAsync`, `ConfirmOrderPreparationAsync`, `ConfirmKdsOrderCompletionAsync`) to check Area flags and set the correct `OrderStatus`. Implement automatic status transitions where flags are false.
    *   If `EnableCompletionConfirmation` is used, create `ConfirmOrderPickupAsync` service method and corresponding `[HttpPut]` endpoint in `OrdersController`. Define authorization.
    *   Ensure DTOs (`OrderDto`, `KdsOrderDto`, etc.) reflect the current status accurately.
*   **Frontend:**
    *   Add toggles/checkboxes to the Area Admin UI (`/app/org/{orgId}/admin/areas/{areaId}`) to manage the new flags.
    *   If `EnableCompletionConfirmation` is used, design and implement the UI trigger for the final pickup confirmation step.
