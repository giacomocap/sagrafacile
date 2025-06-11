# SagraFacile - Waiter Mobile Interface Architecture

This document outlines the architecture and implementation plan for the Waiter Mobile Interface feature in SagraFacile. The primary goal of this interface is to allow waiters to scan an order receipt's QR code at the customer's table, associate a table number with the order, and confirm it for preparation, triggering kitchen printing and KDS updates.

## Core Workflow

1.  **Cashier:** Prints a receipt for a paid order. The receipt contains a QR code uniquely identifying the order (using `orderId`).
2.  **Waiter:** Approaches the customer's table, uses the SagraFacile web app on a mobile/tablet device, and navigates to the Waiter interface.
3.  **Waiter:** Scans the QR code on the customer's receipt using the device's camera via the web app.
4.  **App:** Extracts the `orderId` from the QR code and fetches the corresponding order details from the backend API (`GET /api/orders/{orderId}`).
5.  **App:** Displays the order details (items, quantities, notes) and prompts the waiter to enter the table number.
6.  **Waiter:** Enters the table number into the input field.
7.  **Waiter:** Reviews the order and table number, then taps a "Confirm & Send to Kitchen" button.
8.  **App:** Sends a request to the backend API (`PUT /api/orders/{orderId}/confirm-preparation`) including the entered table number.
9.  **System (Backend):**
    *   Receives the confirmation request.
    *   Validates the request (user role, order status).
    *   Updates the `Order.Status` to `Preparing`.
    *   Updates the `Order.TableNumber` with the provided value.
    *   Saves the changes to the database.
    *   Triggers the `PrintService` to print comandas to the appropriate kitchen/bar printers (based on category assignments).
    *   Triggers the SignalR Hub to send real-time updates to the relevant Kitchen Display Systems (KDS).
10. **App:** Provides visual feedback (success/error message) to the waiter.

## Implementation Plan

### Phase 0: Documentation & Setup

1.  **Create Documentation:** This `WaiterArchitecture.md` file.
2.  **Update Roadmap:** Modify `Roadmap.md` to place the Waiter Interface implementation before KDS UI but after SignalR Hub setup. Mark as core feature.
3.  **Update READMEs:** Mention this architecture document in relevant `README.md` files.
4.  **Add Waiter Role:** Ensure the "Waiter" role exists in the backend Identity system (via seeding or Admin UI).

### Phase 1: Backend Changes

1.  **Order Model:** Add `TableNumber` (string, nullable) to `SagraFacile.NET.API.Models.Order`. Create and apply EF Core migration.
2.  **Order DTOs:** Update `OrderDto` in `SagraFacile.NET.API.DTOs` to include `TableNumber`.
3.  **Confirmation Endpoint (`PUT /api/orders/{orderId}/confirm-preparation`):**
    *   Create the controller action in `OrdersController.cs`.
    *   Define an input DTO, e.g., `ConfirmPreparationDto { string TableNumber }`.
    *   Implement authorization check for "Waiter" role.
    *   Implement service logic (`OrderService.cs`):
        *   Fetch order.
        *   Validate status (e.g., must be `Paid`).
        *   Update `Status` to `Preparing`.
        *   Update `TableNumber`.
        *   Save changes.
        *   Invoke `PrintService` methods (existing logic for comanda printing).
        *   Invoke SignalR Hub methods (existing logic for KDS updates).
4.  **Retrieval Endpoint (`GET /api/orders/{orderId}`):** Ensure this endpoint returns the `TableNumber` in the `OrderDto` and is accessible by the "Waiter" role.

### Phase 2: Frontend - Cashier Receipt Update

1.  **QR Code Generation:**
    *   Modify `ReceiptDialog.tsx` (`sagrafacile-webapp/src/components/ReceiptDialog.tsx`).
    *   In the function that prepares data for the WebSocket (`formatReceiptForPlainText` or similar):
        *   Ensure the `orderId` is available from the order data.
        *   Use a library (e.g., `qrcode.react` or a text-based generator) to create a QR code representation containing the `orderId`.
        *   Embed this representation into the string sent to the `SagraFacile.WindowsPrinterService`. *(Requires testing printer capability)*.

### Phase 3: Frontend - Waiter UI Implementation

1.  **Routing & Access Control:**
    *   Create route `/app/org/{orgId}/waiter` within `sagrafacile-webapp/src/app/app/org/[orgId]/`.
    *   Implement layout and protect the route for the "Waiter" role.
    *   Add navigation link if needed.
2.  **Scanning Page Component:**
    *   Create the main component for the `/waiter` page.
    *   Add a button to trigger scanning.
    *   Integrate a QR code scanning library (e.g., `react-qr-reader`, `html5-qrcode`). Handle camera permissions.
3.  **Order Confirmation Component:**
    *   Create a component to display order details and handle confirmation.
    *   Triggered after successful scan.
    *   Input state for `orderId`.
    *   Fetch order data using `useEffect` and `apiClient.get(`/orders/${orderId}`)`.
    *   Display order details (Order #, Items, etc.) using Shadcn/ui components.
    *   Include a controlled `<Input>` component for `tableNumber`.
    *   Include a "Confirm & Send to Kitchen" `<Button>`.
4.  **Confirmation Action:**
    *   Button's `onClick` handler:
        *   Validate `tableNumber` input.
        *   Call `apiClient.put(`/orders/${orderId}/confirm-preparation`, { tableNumber })`.
        *   Handle loading state.
        *   Display success/error toasts (`sonner`).
    *   On success, potentially clear the view or navigate back.

## Phase 3.5: List-Based View Enhancement (Post-Initial Implementation)

*   **Goal:** Enhance the Waiter UI beyond just scanning by adding lists of orders, allowing waiters to view and manage orders without needing the physical receipt QR code.
*   **UI Changes:**
    *   Introduce Tabs (e.g., "Da Confermare", "In Corso / Pronti").
    *   Keep the QR Scan button accessible.
    *   Display orders in mobile-friendly lists within each tab.
    *   "Da Confermare" list shows orders with status `Paid` or `PreOrder`. Clicking an order opens the confirmation view (pre-filled, needs table number).
    *   "In Corso / Pronti" list shows orders with status `Preparing` or `ReadyForPickup`. Clicking an order shows a read-only summary.
    *   Integrate SignalR for real-time list updates.
*   **Backend API Requirements:**
    *   The `GET /api/orders` endpoint needs modification:
        *   Allow access for the "Waiter" role.
        *   Add support for filtering by multiple `OrderStatus` values via query parameters (e.g., `?statuses=Paid&statuses=PreOrder`).
        *   Ensure it correctly filters orders by the waiter's organization context.

### Phase 4: Future Enhancements

*   **Cashier Pre-Order Scan:** Reuse the QR scanning component on the Cashier page to scan pre-order QR codes and load the order into the cart.

## Visual Plan (Mermaid) - Original Scan Flow

```mermaid
sequenceDiagram
    participant C as Cashier UI
    participant WP as Windows Printer Service
    participant P as Printer
    participant W as Waiter UI (Mobile)
    participant API as Backend API
    participant KDS as Kitchen Display System
    participant KP as Kitchen Printer

    C->>WP: Send Print Command (Receipt Data + OrderID for QR)
    WP->>P: Print Receipt (with QR Code)

    Note over W: Waiter scans QR Code at Table
    W->>W: Activate QR Scanner
    W-->>API: GET /api/orders/{orderId}
    API-->>W: Order Details (without TableNumber initially)

    Note over W: Waiter enters Table Number & Confirms
    W->>API: PUT /api/orders/{orderId}/confirm-preparation\n(Body: { tableNumber: "T12" })
    API->>API: Update Order.Status = Preparing
    API->>API: Update Order.TableNumber = "T12"
    API-->>W: Confirmation Success/Error

    par Trigger Preparation
        API->>API: Trigger PrintService
        API->>KP: Print Comanda (Network or via SignalR->WP)
    and
        API->>API: Trigger SignalR Hub
        API-->>KDS: Push Order Update (via SignalR)
    end
