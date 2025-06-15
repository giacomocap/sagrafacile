# Project Memory - SagraFacile WebApp Frontend

# How to work on the project
*   **Technology:** Next.js (App Router), TypeScript, Tailwind CSS, Shadcn/ui.
*   **API Interaction:** Use `src/services/apiClient.ts` for backend calls. Ensure DTOs in `src/types/index.ts` match backend definitions. Handle loading and error states gracefully.
*   **State Management:** Primarily React Context API (`src/contexts`) for global state like Auth, Organization, Session. Use hooks (`src/hooks`) for reusable logic (e.g., `useSignalRHub`, `usePrinterWebSocket`). Use `useState` for local component state.
*   **UI Components:** Leverage Shadcn/ui components (`src/components/ui`). Create reusable custom components in `src/components` following Shadcn patterns where applicable.
*   **Routing:** Use Next.js App Router conventions (`src/app`). Protect routes using layout checks (`src/app/app/layout.tsx`, `src/app/app/org/[orgId]/layout.tsx`) and `AuthContext`.
*   **Real-time:** Use `useSignalRHub` hook for SignalR interactions with the backend `/api/orderHub`. Use `usePrinterWebSocket` for local printer communication via `ws://localhost:9101`.
*   **Testing:** Manual testing by the user is the primary method after implementation. Focus on testing core workflows (Cashier, Waiter, KDS, Admin) across different roles.
*   **Memory:** Update this `ProjectMemory.md` file at the end of each session, summarizing accomplishments, key decisions, identified issues, and agreed-upon next steps. Reference relevant file paths.
---
# Session Summaries (Newest First)

## (2025-06-15) - Translated Admin UI
Translated client-facing strings to Italian in Admin UI components (`sagrafacile-webapp/src/app/app/org/[orgId]/admin` and `sagrafacile-webapp/src/components/admin`) to improve user experience for Italian-speaking operators.

## (2025-06-15) - Resolved Docker Permissions for API Media Uploads
Fixed `System.UnauthorizedAccessException` in .NET API Docker service by modifying `SagraFacile.NET/SagraFacile.NET.API/Dockerfile` to set correct write permissions for `/app/wwwroot/media/promo` as a non-root user, ensuring successful media uploads.

## (2025-06-15) - Resolved Next.js Image Optimization Issue in Docker Environment
Resolved "400 Bad Request" errors for Next.js image optimization in Docker by implementing environment-aware URL transformation in `sagrafacile-webapp/next.config.ts` and `sagrafacile-webapp/src/lib/imageUtils.ts`, allowing internal Docker network fetching and bypassing circular Caddy routing.

## (2025-06-12) - Implemented Frontend for On-Demand Printer Configuration
Implemented `PrintMode` selection in `sagrafacile-webapp/src/components/admin/PrinterFormDialog.tsx` for `WindowsUsb` printers, allowing "Immediate" or "On-Demand" printing, while network printers default to "Immediate."

## (2025-06-11) - Shifted to Pre-built Docker Image Deployment Strategy
Updated `docker-compose.yml` to use pre-built Docker images for frontend deployment, simplifying user setup by removing local build requirements and updating documentation (`README.md`, `DEPLOYMENT_ARCHITECTURE.md`).

## (2025-06-11) - Initiated Docker-Based Deployment Setup (Frontend Aspects)
Established comprehensive Docker-based deployment for frontend, including multi-stage `sagrafacile-webapp/Dockerfile`, `docker-compose.yml` service definition, Caddy routing, and helper scripts for user setup.

## (2025-06-10) - Unified Receipt Dialog Layout to Fix Overflow
Fixed `ReceiptDialog` overflow on mobile/constrained screens by adding `min-h-0` to `ScrollArea` and simplifying `ResponsiveDialog.tsx` to use a single `Sheet` component for consistent layout.

## (2025-06-10) - Responsive Receipt Dialog for Mobile
Implemented responsive `ReceiptDialog` using `useMediaQuery` and `ResponsiveDialog.tsx` (conditionally rendering `Dialog` or `Sheet`), and refactored `ReceiptDialog.tsx` with vertical flexbox to prevent overflow on mobile.

## (2025-06-10) - Mobile-Responsive Admin Interface
Optimized Admin UI for mobile by creating `AdminNavigation.tsx`, enhancing `admin/layout.tsx` with responsive design (Sheet for mobile nav), and redesigning `admin/page.tsx` into a card-based dashboard.

## (2025-06-09) - Fixed Public Display API Calls
Resolved 401 Unauthorized error on public Queue Display page by updating `sagrafacile-webapp/src/app/(public)/qdisplay/org/[orgSlug]/area/[areaId]/page.tsx` to call the correct public API endpoint `/api/public/areas/{areaId}/queue/state`.

## (2025-06-09) - Enhanced Ad Assignment UI
Improved `AdAssignmentUpsertDialog.tsx` by conditionally hiding the "Duration" field and setting its value to `null` when a video media type is selected, streamlining UX.

## (2025-06-09) - Finalized and Debugged Dynamic Ad Carousel
Completed dynamic ad carousel implementation on public Queue Display, fetching media from backend, adding automatic video duration, and resolving CORS, ad blocker, and Next.js Image component issues.

## (2025-06-09) - Implemented Ad Carousel Backend and Admin UI
Implemented backend API for ad media management (CRUD, file storage) and created a new frontend admin page (`/app/org/[orgId]/admin/ads`) with `AdUpsertDialog.tsx` for managing ad media.

## (2025-06-09) - Implemented Ad Carousel on Public Queue Display
Added a rotating display of images and videos (`AdCarousel.tsx`) to the public Queue Display page, with a two-phase architecture plan for dynamic content.

## (2025-06-09) - Optimized Cashier Order Panel UI for Vertical Space
Refactored `CashierOrderPanel.tsx` to compact queue controls and reduce order item spacing, optimizing vertical space for better visibility on laptop screens.

## (2025-06-08) - Display Guest and Takeaway Charges in UI
Updated frontend UI (`ReceiptDialog.tsx`, `CashierOrderPanel.tsx`, `table-order/area/[areaId]/page.tsx`, `admin/orders/page.tsx`, `ReprintOrderDialog.tsx`) to display guest and takeaway charges from backend in live orders and historical receipts.

## (2025-06-08) - Implemented Guest and Takeaway Charges
Implemented admin UI in `sagrafacile-webapp/src/app/app/org/[orgId]/admin/areas/page.tsx` to configure and display per-guest "coperto" and per-order "asporto" fees for areas.

## (2025-06-07) - Unified QR Code Scanning and Order Viewing
Centralized QR code scanning (`OrderQrScanner.tsx`) and order confirmation/viewing (`OrderConfirmationView.tsx`) into reusable components, integrating them into Waiter, Mobile Table Order, and Cashier interfaces.

## (2025-06-07) - Refactored Waiter Interface to be Area-Specific
Refactored Waiter interface to be area-specific, moving the main page to a dynamic route (`/waiter/area/[areaId]`) and creating a new `AreaSelector` page for initial area selection.

## (2025-06-07) - Refactored Area Selection and Added Table Order Area Selector
Centralized area selection logic into a reusable `AreaSelector.tsx` component, applying it to both Cashier and a new Table Order area selection page, and updating Admin layout navigation.

## (2025-06-07) - Implemented Mobile Table Ordering Page (Phase 1)
Implemented the basic structure for the mobile table ordering page, integrating menu display, cart, order form, payment flow, and SignalR stock updates, with UI/UX refinements.

## (2025-06-06) - Enhanced Pre-order Scan with Immediate Stock Warning
Improved pre-order UX by adding a client-side warning toast in Cashier page (`handleScanResult`) for potential stock issues immediately after scanning.

## (2025-06-06) - Adjust Cashier Order Panel Styling for Laptop Screens
Adjusted styling in `CashierOrderPanel.tsx` to reduce horizontal and vertical space, improving visibility of the order summary on laptop screens.

## (Next Session) - Planned Work
Current session paused debugging of USB thermal printer due to issues with the `SagraFacile.WindowsPrinterService` companion app's registration with the SignalR hub. This impacts both backend and frontend testing of the printing feature.


---
# Historical Sessions (Condensed)

## Stock Management - Admin UI (Frontend - 2025-06-04)
*   **Summary:** Implemented Admin UI for stock (`scorta`) management. Updated `MenuItemDto` and created `StockUpdateBroadcastDto` in types. Added API client functions for stock operations. Refactored Admin Menu Items page: removed category dropdown (shows all items for area), added "Category" column, updated Add/Edit dialogs with category select and `scorta` input. Added "Stock" column, "Reset Stock" button (item-level), and "Reset All Area Stock" button (area-level) to the items table. Integrated SignalR for real-time stock updates in Cashier UI (display, out-of-stock indication, client-side pre-check).
*   **Outcome:** Admin users can now manage stock levels. Cashier UI reflects stock in real-time.

## DisplayOrderNumber Fixes (Frontend - 2025-06-04)
*   **Summary:** Corrected `displayOrderNumber` usage in Staff Pickup Confirmation page (table, SignalR, toasts), Waiter Interface (order view/list), and Cashier Reprint Dialog. Confirmed KDS components already had correct logic, pointing to a potential backend DTO issue for KDS.
*   **Outcome:** Improved display of human-readable order numbers across various interfaces. Backend KDS DTO check identified as a next step.

## Login Page Image Update (Frontend - 2025-06-04)
*   **Summary:** Changed the login page image to `/sagrafacile-logo.png` and adjusted styling for better presentation.
*   **Outcome:** Enhanced visual appeal of the login page.

## Display Order Number - Initial Implementation (Frontend - 2025-06-03)
*   **Summary:** Began frontend implementation for `displayOrderNumber`. Updated `OrderDto`, `KdsOrderDto`, `OrderStatusBroadcastDto` types. Modified Receipt Dialog, KDS main view & detail dialog, Public Order Pickup Display, and Admin Orders table to show `displayOrderNumber` (with fallbacks). Added an informational tooltip on Admin Areas page about `Area.Slug`'s influence on the order number prefix.
*   **Outcome:** Initial display of `displayOrderNumber` across multiple frontend views. Further testing and UI considerations planned.

## Automatic Token Refresh (Frontend - 2025-05-29)
*   **Summary:** Implemented automatic JWT token refresh. Added `TokenResponseDto` and `RefreshTokenRequestDto` types. Enhanced `AuthContext` to store/manage both access and refresh tokens in `localStorage` and state. Refactored `apiClient.ts` with interceptors to handle 401 errors by attempting a token refresh using `/accounts/refresh-token` endpoint, updating tokens, and retrying the original request. Includes logic to queue requests during refresh.
*   **Key Decisions:** Standard refresh token flow. `AuthContext` for token management. `apiClient` for transparent refresh logic.
*   **Outcome:** Improved user session persistence by automatically refreshing expired access tokens.

## Public Order Pickup Display & Staff Confirmation (Frontend - 2025-05-29)
*   **Summary:** Implemented frontend for Public Order Pickup Display and Staff Confirmation. Added `OrderStatusBroadcastDto` type. Enhanced `apiClient` with `getPublicReadyForPickupOrders` and `confirmOrderPickup`. Created Public Pickup Display page (`/pickup-display/...`) with initial data fetch, SignalR (`ReceiveOrderStatusUpdate`), order display, and TTS via new `useAnnouncements` hook. Modified Staff Pickup Confirmation page (`/pickup-confirmation/...`) with "Rechime" button and `useAnnouncements`. Refactored `useAnnouncements` hook for versatility (added `speakRawText`). Modified Queue Display page (`/qdisplay/...`) to use `useAnnouncements`.
*   **Key Decisions:** `Area-${areaId}` SignalR group. Centralized announcements in `useAnnouncements`.
*   **Identified Issues:** Public Pickup Display not showing orders (API returning empty), traced to potential backend `DayId` filtering issue in `OrderService.GetOrdersByStatusAsync`.
*   **Outcome:** New public and staff interfaces for order pickup. Backend investigation for data fetching issue prioritized. The "Planned Public Order Pickup Display" session's items were incorporated.

## Customer Queue System - UI & Real-time (Frontend - "Previous Date" prior to 2025-05-29)
*   **Summary:** Integrated Customer Queue System UI into `CashierOrderPanel` (props for state/actions, conditional display, "NOW SERVING"/"NEXT", "Call Next", "Call Specific", "Ripeti Ultimo" buttons). Enhanced Cashier Page (`/cashier/.../page.tsx`) with state for queue, initial fetch, and SignalR integration (`useSignalRHub` for `Area-{areaId}` group, listeners for `QueueNumberCalled`, `QueueReset`, `QueueStateUpdated` to update local state). Action handlers call `queueService` and rely on SignalR for UI updates. Created Public Queue Display page (`/qdisplay/...`) with initial fetch, SignalR, audio, and TTS. Corrected SignalR DTOs/event names (PascalCase).
*   **Key Decisions:** Queue UI in `CashierOrderPanel`. SignalR for real-time updates. PascalCase event names. Explicit group joining.
*   **Identified Issues:** Resolved SignalR event name/group joining issues. Resolved `useEffect` dependency issue causing handler re-registration. Cashier `lastCalledNumber` display issue remained under investigation.
*   **Outcome:** Cashier and Public Display UIs for queue system with real-time updates. Debugging of Cashier UI display ongoing.

## Login & UI Enhancements (Various Dates)
*   Revamped Login Page UI (`login-04` template inspired).
*   Enhanced Cashier Interface: Coperti/Asporto UI, cart grouping, mandatory customer name, clear order button.
*   Improved Receipt Dialog: Category grouping, Asporto highlighting, better Coperti display, fixed reprint issues.

## Printing Architecture & Admin UIs (Various Dates)
*   Implemented Admin Orders Page Reprint with printer selection.
*   Cashier Station Integration: Selection in Cashier UI, refactored Cashier page into components (`CashierMenuPanel`, `CashierOrderPanel`), improved Area data fetching.
*   Implemented Cashier Station Management Admin UI (CRUD).
*   Enhanced Area Admin UI: Default printer configuration, comanda printing toggles.
*   Implemented Printer Configuration Admin UI: Printer CRUD (Network/USB), category assignments.
*   Implemented Configurable Order Workflow Admin UI: Toggles for `EnableWaiterConfirmation`, `EnableKds`, `EnableCompletionConfirmation` in Area settings.
*   Refactored `ReceiptDialog` and `ReprintOrderDialog` to use backend printing, removing client-side WebSocket logic for printing.

## SagraPreOrdine & Operational Day Features (Various Dates)
*   Implemented Frontend UI for SagraPreOrdine Integration (configuration & sync trigger).
*   Implemented "No Day Open" Warning/Blur Overlay for Cashier, Waiter, KDS; Admin banner.
*   Implemented Admin Order Filtering by Day with role restrictions.

## Day Management & Admin Features (2025-04-23)
* Implemented Admin Day Management UI for viewing/managing operational days
* Added day-based filtering to Admin Orders page
* Created Day Status Indicator component and integrated it across interfaces
* Fixed SuperAdmin context issues
* Added "No Day Open" warning overlay for operational interfaces

## KDS & Waiter Interface Improvements (2025-04-19 to 2025-04-22)
* Refactored KDS confirmation workflow to use station-level confirmation
* Added KDS completed orders history dialog
* Enhanced KDS cards with customer name, item counts, and status badges
* Fixed Waiter UI with tabbed interface and status filtering
* Implemented Cashier reprint functionality
* Made customer name mandatory for all orders

## SignalR & Core Interfaces (2025-04-15 to 2025-04-18)
* Stabilized SignalR connections with proper authentication
* Built KDS interface with real-time updates and item confirmation
* Implemented KDS Admin UI for station and category management
* Created Waiter interface with QR scanning and order confirmation
* Updated Cashier interface to display backend-generated QR codes
* Fixed various context and API integration issues

## Initial Implementation (Before 2025-04-15)
* Established core architecture with Next.js, TypeScript, Tailwind, and Shadcn/ui
* Implemented Authentication system and role-based access control
* Created Admin CRUD interfaces for Organizations, Areas, Menu Items, and Users
* Built basic Cashier interface with area selection, menu browsing, cart, and payment
* Developed public Pre-Order interface for customer self-service
* Implemented Admin Orders History view
