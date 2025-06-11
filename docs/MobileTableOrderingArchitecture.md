# SagraFacile - Mobile Table Ordering & Payment Architecture

## 1. Introduction & Goals

This document outlines the architecture for a mobile-first interface within SagraFacile, designed to empower waiters and administrators to take customer orders and process payments directly at the table. It should have almost the same functionalities as the Cashier Interface, but built for mobile screens (smartphones and tablets)

**Key Objectives:**

*   **Streamline Table-Side Operations:** Reduce manual steps and improve efficiency for orders originating at the customer's table.
*   **Integrated Payment:** Allow immediate payment processing (Cash or POS) at the point of order entry (same as Cashier interface).
*   **Intelligent Workflow Progression:** When a table number is provided, the system should intelligently advance the order status, potentially bypassing intermediate steps if `Area.EnableWaiterConfirmation` is true.
*   **UI/UX Consistency:** Leverage existing UI patterns and components (e.g., from Pre-Order (example components and page in preorder-page.txt) and Cashier interfaces) for a familiar user experience.
*   **Mobile-First Design:** Ensure the interface is optimized for use on mobile devices (smartphones, tablets).

## 2. User Roles & Access

*   **Primary Users:** Staff members with "Waiter" or "Admin" roles.
*   **Permissions:**
    *   Access to the new mobile ordering route.
    *   Ability to browse the menu, create orders, and input customer/table details.
    *   Ability to process payments (Cash/POS).

## 3. Frontend Design & Workflow

*   **Route:** `/app/org/[orgId]/table-order/area/[areaId]/`
    *   Area selection is handled via URL parameters, consistent with the Cashier interface.
*   **Core UI Components & Logic:**
    *   **Authentication & Context:**
        *   Utilizes `AuthContext` for user authentication and role checks.
        *   Utilizes `OrganizationContext` for organization-specific data.
        *   Fetches `AreaDto` based on `areaId` from the URL to get area-specific settings (menu, workflow flags).
    *   **Menu Display & Cart Management:**
        *   Reuses menu browsing (categories, items) and client-side cart management logic, likely adapted from `pre-order-menu.tsx` (preorder-page.txt) and `CashierMenuPanel.tsx`.
        *   Includes functionality for adding items, adjusting quantities, and managing item notes.
    *   **Order Details Form:**
        *   `customerName` (string, mandatory).
        *   `isTakeaway` (boolean, defaults to `false`).
        *   `numberOfGuests` (integer, defaults to `1`; disabled if `isTakeaway` is true).
        *   `tableNumber` (string, mandatory for this specific workflow to trigger optimized status progression).
    *   **Payment Section:**
        *   "Paga Contanti" button.
        *   "Paga POS" button.
    *   **Submission Logic:**
        *   Upon confirming payment, the frontend constructs a `CreateOrderDto`.
        *   This DTO includes all order items, customer details, `tableNumber`, `paymentMethod`, and `amountPaid`.
        *   The DTO is sent to the backend `POST /api/orders` endpoint.
    *   **Receipt Handling:**
        *   Integrates the existing `ReceiptDialog` component (similar to `CashierPage.tsx`) to display the order summary and trigger printing after successful backend submission.
*   **Real-time Updates:**
    *   Subscribes to SignalR updates (e.g., `ReceiveStockUpdate` via `useSignalRHub`) to reflect real-time stock levels in the menu.

## 4. Backend Design & Workflow

*   **DTO Usage (`CreateOrderDto`):**
    *   The existing `CreateOrderDto` will be used. Key fields for this flow:
        *   `AreaId` (int)
        *   `CustomerName` (string)
        *   `Items` (List<CreateOrderItemDto>)
        *   `PaymentMethod` (string: "Contanti" or "POS")
        *   `AmountPaid` (decimal?, total amount if POS, tendered amount if Cash)
        *   `NumberOfGuests` (int)
        *   `IsTakeaway` (bool)
        *   `TableNumber` (string?, crucial for this flow's optimized status progression)
        *   `CashierStationId` (int?, optional, less likely to be manually selected in this flow but supported by DTO).
    *   The `OrderService` will populate `Order.CashierId` with the authenticated user's ID who is creating the order via this interface (as they are acting as the cashier at the table).
*   **`OrderService.CreateOrderAsync` Modifications:**
    *   The service receives the `CreateOrderDto`.
    *   The `Order.CashierId` will be set to the ID of the authenticated user.
    *   **Status Transition Logic:**
        1.  Load the `Area` entity to access workflow flags (`EnableWaiterConfirmation`, `EnableKds`, `EnableCompletionConfirmation`).
        2.  **If `CreateOrderDto.TableNumber` is provided AND `Area.EnableWaiterConfirmation == true`:**
            *   The `Order.Status` is set directly based on subsequent flags:
                *   If `Area.EnableKds == true`: `OrderStatus.Preparing`.
                *   Else if `Area.EnableCompletionConfirmation == true`: `OrderStatus.ReadyForPickup`.
                *   Else: `OrderStatus.Completed`.
            *   This effectively bypasses the `OrderStatus.Paid` state that would normally require a separate waiter confirmation scan.
            *   `Order.WaiterId` is set to the authenticated user's ID (as they are initiating the confirmed order).
        3.  **Else (no `TableNumber` provided OR `Area.EnableWaiterConfirmation == false`):**
            *   The order follows the standard creation logic. The initial status will be determined as if created by a cashier without immediate waiter action (e.g., `Paid` if `EnableWaiterConfirmation` is true, or directly to `Preparing`/`ReadyForPickup`/`Completed` based on other flags if `EnableWaiterConfirmation` is false). `Order.WaiterId` would typically be null in this path unless set by a subsequent explicit waiter action.
        4.  `Order.DayId` is assigned from the current open operational day.
        5.  `Order.DisplayOrderNumber` is generated.
    *   **Payment Processing:** The `PaymentMethod` and `AmountPaid` from the DTO are stored on the `Order` entity.
    *   **Stock Decrement:** Stock levels for ordered items are checked and decremented transactionally.
    *   **Printing:** Receipt and Comandas are triggered based on the order's *initial effective status* and the area/station's print configurations.
        *   Example: If the order goes directly to `Preparing`, comandas are printed according to rules for the `Preparing` state.

## 5. Printing Logic

*   **Receipt:** Printed automatically upon successful order creation and payment confirmation via the `PrintService`, directed to the printer configured for the selected `CashierStationId` (or the Area's default).
*   **Comandas:** The logic must ensure comandas are printed correctly without duplication.
    *   **Receipt printing is separate and always occurs.** The following logic is only for comandas.
    *   A comanda is printed **immediately** at the time of order creation if:
        *   The selected `CashierStation` has `PrintComandasAtThisStation = true`.
        *   OR the `Area` has `PrintComandasAtCashier = true` (and the station does not).
    *   **If and only if a comanda was NOT printed immediately** (based on the rules above), the system will then trigger comanda printing when the order's status advances due to an implicit waiter confirmation (i.e., a mobile table order).
    *   This ensures that creating a table order from a mobile device has the **exact same comanda printing behavior as a manual waiter confirmation scan**: it triggers category-based printing to dedicated kitchen/bar printers, but only if those comandas haven't already been printed at the cashier/station.

## 6. Interaction with Workflow Flags

This interface primarily interacts with `Area.EnableWaiterConfirmation`:

*   **If `EnableWaiterConfirmation = true`:**
    *   Providing a `TableNumber` in this interface acts as an implicit waiter confirmation, advancing the order directly to `Preparing` (or further, depending on `EnableKds` and `EnableCompletionConfirmation`).
*   **If `EnableWaiterConfirmation = false`:**
    *   Providing a `TableNumber` simply stores it on the order. The order progresses through states as defined by `EnableKds` and `EnableCompletionConfirmation` without a distinct "waiter confirmation" step.

The flags `EnableKds` and `EnableCompletionConfirmation` continue to govern subsequent state transitions after the initial status is set.

## 7. Security Considerations

*   **Authentication:** All access to this interface and its backend API endpoints must be protected by JWT authentication.
*   **Authorization:** The route and API endpoints should be restricted to users with "Waiter" or "Admin" roles.
*   Standard API security practices (input validation, parameterized queries, etc.) apply.
