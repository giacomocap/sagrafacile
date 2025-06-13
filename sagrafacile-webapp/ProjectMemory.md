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

## (2025-06-12) - Implemented Frontend for On-Demand Printer Configuration
**Context:** Continued implementation of the "On-Demand Printing for Windows Companion App" feature, focusing on the frontend Admin UI changes. This follows the backend changes made in `SagraFacile.NET` project memory from 2025-06-12.
**Accomplishments:**
*   **`sagrafacile-webapp/src/types/index.ts` Updated:**
    *   Added `PrintMode` enum (`Immediate`, `OnDemandWindows`).
    *   Added `printMode: PrintMode` property to `PrinterDto` and `PrinterUpsertDto` interfaces.
*   **`sagrafacile-webapp/src/components/admin/PrinterFormDialog.tsx` Updated:**
    *   Imported `PrintMode` enum.
    *   Added `printMode` to the Zod validation schema and to the `useForm` default values (defaulting to `PrintMode.Immediate`).
    *   Updated the `useEffect` hook that resets the form to correctly handle `printMode` for both new and existing printers.
    *   Updated the `useEffect` hook that watches `printerType`:
        *   If `printerType` is `Network`, `printMode` is automatically set to `PrintMode.Immediate`.
        *   This logic applies in both add and edit modes.
    *   The `onSubmit` function now includes `printMode` in the `dataToSend` payload.
    *   Conditionally rendered the `PrintMode` `FormField` (Select component):
        *   It is displayed and editable if `printerType` is `PrinterType.WindowsUsb`.
        *   It is displayed but *disabled* and set to `PrintMode.Immediate` if `printerType` is `PrinterType.Network`.
    *   Added Italian labels for `PrintMode` options: "Immediata (stampa subito)" and "Su Richiesta (in coda sull'app Windows)".
**Key Decisions:**
*   The `PrintMode` selection is only enabled for `WindowsUsb` printers, as `Network` printers always print immediately.
*   The UI clearly communicates this distinction through conditional rendering/disabling and descriptive text.
**Next Steps:**
*   User to test the updated `PrinterFormDialog` to ensure `PrintMode` is correctly handled for both Network and WindowsUsb printers during creation and editing.
*   Proceed with Windows Companion App changes for on-demand printing as outlined in the main `Roadmap.md` and backend `ProjectMemory.md`.

## (2025-06-11) - Shifted to Pre-built Docker Image Deployment Strategy
**Context:** Aligned with the overall project shift, the frontend deployment will now rely on pre-built Docker images distributed via a container registry. This simplifies user setup by removing local build requirements.
**Accomplishments (Overall Project & Frontend Impact):**
*   **`docker-compose.yml` Updated:** The main `docker-compose.yml` was modified. The `frontend` service now uses an `image:` directive (e.g., `yourdockerhub_username/sagrafacile-frontend:latest`) instead of a `build:` directive. The `NEXT_PUBLIC_API_BASE_URL=/api` and `NODE_ENV=production` environment variables are retained.
*   **New Helper Scripts Created:** `start.bat`/`start.sh`, `update.bat`/`update.sh`, and `stop.bat`/`stop.sh` were created in the repository root. These scripts manage the Docker Compose lifecycle, including pulling images.
*   **Documentation Updated:**
    *   `README.md`: The "Docker Deployment & Installation Guide" was rewritten to reflect the new process (download ZIP, configure `.env`, run `start` script).
    *   `DEPLOYMENT_ARCHITECTURE.md`: Updated to detail the pre-built image strategy. The frontend Dockerfile (`sagrafacile-webapp/Dockerfile`) is now noted as a developer/CI artifact, not for user builds.
**Key Decisions:**
*   The SagraFacile frontend will be distributed as a pre-built Docker image.
*   The existing `sagrafacile-webapp/Dockerfile` will be used by the developer/CI pipeline to build this image.
*   End-users will no longer need the frontend source code in the distributable package.
**Next Steps (Developer):**
*   Ensure the CI/CD pipeline correctly builds and pushes the frontend Docker image to the chosen container registry.
*   Verify that the `NEXT_PUBLIC_API_BASE_URL=/api` setting works as expected when the frontend image is pulled and run via the updated `docker-compose.yml` and Caddy routing.
*   Update placeholder image names in `docker-compose.yml` and relevant documentation with actual registry paths.

## (2025-06-11) - Initiated Docker-Based Deployment Setup (Frontend Aspects)
**Context:** Began implementing a comprehensive Docker-based deployment strategy for SagraFacile.
**Accomplishments:**
*   **Deployment Architecture Documented:** Created `DEPLOYMENT_ARCHITECTURE.md` in the repository root, detailing the 5-phase plan, core technologies (Docker, Docker Compose, Caddy), and setup workflows. This document also covers frontend aspects like the `NEXT_PUBLIC_API_BASE_URL=/api` configuration for use with Caddy.
*   **Frontend Dockerfile (`sagrafacile-webapp/Dockerfile`):** Created a multi-stage Dockerfile for the Next.js application.
    *   Stage 1 (builder): Uses `node:20-alpine`, copies `package.json` and `package-lock.json`, runs `npm install`, copies the rest of the code, and runs `npm run build`.
    *   Stage 2 (runner): Uses `node:20-alpine`, sets `NODE_ENV=production`, copies built assets (`.next`, `public`, `node_modules`, `package.json`) from the builder stage, exposes port 3000, and sets `CMD ["npm", "start"]`.
*   **Docker Compose (`docker-compose.yml`):** This file (in the repo root) now includes the `frontend` service definition, specifying its build context (`./sagrafacile-webapp`), Dockerfile, container name (`sagrafacile_frontend`), and environment variables (`NEXT_PUBLIC_API_BASE_URL=/api`, `NODE_ENV=production`).
*   **Caddyfile:** This file (in the repo root) includes the rule `handle { reverse_proxy frontend:3000 }` to route traffic to the frontend service.
*   **`.env.example`:** This file (in the repo root) notes that `NEXT_PUBLIC_API_BASE_URL` is handled by Caddy and generally doesn't need to be set by the user for the frontend in this deployment model.
**Key Decisions:**
*   The frontend will be served via Caddy, which handles HTTPS and routes appropriate requests to the Next.js container listening on port 3000.
*   `NEXT_PUBLIC_API_BASE_URL` is set to `/api` to ensure frontend API calls are correctly routed through Caddy to the backend.
*   **Helper Scripts Created:** `setup.bat` and `setup.sh` were created in the repository root to guide users through the Docker Compose setup, including `.env` configuration and Caddy CA certificate installation. These scripts also provide guidance on finding the server's local IP address.
*   **Main README Updated:** The main `README.md` in the repository root was significantly updated with a comprehensive "Docker Deployment & Installation Guide," incorporating details from `DEPLOYMENT_ARCHITECTURE.md` and instructions for using the new setup scripts, trusting the Caddy CA, and basic network configuration considerations. It also references `docs/NetworkingArchitecture.md` for more detailed network planning.
**Next Steps (Overall Deployment Plan):**
*   Manually package all components (source code, Dockerfiles, `docker-compose.yml`, `Caddyfile`, `.env.example`, setup scripts, `README.md`) into a distributable `.zip` file (Task 4.3).
*   Finalize the Windows Printer Service application and its installer, ensuring it integrates with the Dockerized backend and respects the CA certificate.
*   Further refine documentation as needed.

## (2025-06-10) - Unified Receipt Dialog Layout to Fix Overflow
**Context:** The `ReceiptDialog` had persistent layout issues on both mobile and vertically-constrained desktop screens. When an order contained many items, the scrollable area would expand instead of scrolling, pushing the footer with action buttons out of view.
**Accomplishments:**
*   **`sagrafacile-webapp/src/components/ReceiptDialog.tsx`:** Added `min-h-0` to the `ScrollArea` component. This was a key fix to ensure the flex item (`flex-1`) could shrink properly within its container, enabling scrolling instead of overflowing.
*   **`sagrafacile-webapp/src/components/shared/ResponsiveDialog.tsx`:** After initial attempts to maintain separate desktop (`Dialog`) and mobile (`Sheet`) views proved complex, the component was simplified to use the `Sheet` component for all screen sizes. This provides a single, consistent, and reliable layout.
**Key Decisions:**
*   Abandoned the responsive branching logic (Dialog vs. Sheet) in favor of a single, robust layout using the `Sheet` component. This simplifies the code and guarantees consistent behavior across all devices, resolving the overflow headache permanently.

## (2025-06-10) - Responsive Receipt Dialog for Mobile
**Context:** The receipt dialog (`ReceiptDialog.tsx`) could overflow on mobile screens when displaying orders with many items, making the action buttons at the bottom inaccessible.
**Accomplishments:**
*   **Created `sagrafacile-webapp/src/hooks/use-media-query.ts`:**
    *   Added a new, reusable hook to detect screen size changes using `window.matchMedia`. This provides a clean way to implement responsive logic in components.
*   **Created `sagrafacile-webapp/src/components/shared/ResponsiveDialog.tsx`:**
    *   Developed a new wrapper component that uses the `useMediaQuery` hook.
    *   It conditionally renders its children inside a standard `Dialog` on desktop screens (`min-width: 768px`) and a `Sheet` (via the `Drawer` component) on mobile screens.
    *   This encapsulates the responsive presentation logic, allowing the content component to remain unaware of the container.
*   **Installed `Drawer` Component:**
    *   Added the `Drawer` component from shadcn/ui (`npx shadcn@latest add drawer`) to be used as the mobile-friendly sheet.
*   **Refactored `sagrafacile-webapp/src/components/ReceiptDialog.tsx`:**
    *   Replaced the static `Dialog` component with the new `ResponsiveDialog`.
    *   The dialog's content is now wrapped in a `flex flex-col h-full` layout to ensure it correctly fills the height of the mobile sheet.
    *   The `ScrollArea` height was adjusted to be more flexible on mobile (`max-h-[60vh]`) while remaining constrained on desktop (`md:max-h-[40vh]`), preventing overflow issues.
**Key Decisions:**
*   Adopted a responsive dialog/sheet pattern to solve the mobile overflow problem without creating two separate receipt components.
*   Created reusable `useMediaQuery` and `ResponsiveDialog` components to promote code reuse and a consistent responsive strategy across the application.
**Follow-up Fix (2025-06-10):**
*   **Fixed `ReceiptDialog` Overflow:** Resolved an issue where the `Sheet` component on mobile would still overflow.
    *   **`sagrafacile-webapp/src/components/ReceiptDialog.tsx`:** Restructured the component's internal layout to use a vertical flexbox (`flex-col`). The `ScrollArea` was made the single flexible element (`flex-1`), while the header, totals section, and footer were made non-flexible (`flex-shrink-0`).
    *   **`sagrafacile-webapp/src/components/shared/ResponsiveDialog.tsx`:** Added `flex flex-col h-full` to the `SheetContent` component to ensure it correctly constrains its children and enables the flex layout to work as intended. This guarantees that the action buttons in the footer are always visible.

## (2025-06-10) - Mobile-Responsive Admin Interface
**Context:** The admin layout and landing page needed to be optimized for mobile phone operators to improve usability on smaller screens.
**Accomplishments:**
*   **Created `sagrafacile-webapp/src/components/admin/AdminNavigation.tsx`:**
    *   Extracted navigation logic into a reusable component that accepts `currentOrgId` and an optional `onLinkClick` callback.
    *   The component handles all admin navigation links with proper active state detection and mobile-friendly interaction.
*   **Enhanced `sagrafacile-webapp/src/app/app/org/[orgId]/admin/layout.tsx`:**
    *   Implemented responsive design with Tailwind CSS breakpoints (`lg:`, `sm:`, `xl:`).
    *   Added mobile navigation using Sheet component from shadcn/ui that slides in from the left.
    *   Desktop sidebar is hidden on mobile (`hidden lg:flex`) and shown on larger screens.
    *   Mobile hamburger menu button is positioned as a fixed overlay (`fixed top-4 left-4 z-50`).
    *   Header is responsive with truncated organization name and adaptive organization selector.
    *   Warning banner and main content padding adjust based on screen size.
    *   Logout button text adapts to screen size (shows email on xl screens, just "Logout" on smaller).
*   **Redesigned `sagrafacile-webapp/src/app/app/org/[orgId]/admin/page.tsx`:**
    *   Transformed the basic landing page into a comprehensive dashboard with card-based navigation.
    *   Added responsive grid layouts that adapt from 1 column on mobile to 4 columns on xl screens.
    *   Organized sections into "Gestione" (Management), "Operazioni" (Operations), and "Azioni Rapide" (Quick Actions).
    *   Each section uses colored icons and descriptive cards for better visual hierarchy.
    *   Added current day status badge when a day is open.
    *   Fixed TypeScript error by using `currentDay.startTime` instead of non-existent `date` property.
**Key Decisions:**
*   Used Sheet component for mobile navigation to provide a native mobile app-like experience.
*   Maintained desktop functionality while adding mobile support without breaking existing workflows.
*   Created reusable AdminNavigation component to reduce code duplication and improve maintainability.
*   Implemented progressive disclosure with responsive text and layout adjustments based on screen size.
*   Used Tailwind's responsive utilities extensively for mobile-first design approach.

## (2025-06-09) - Fixed Public Display API Calls
**Context:** The public-facing Queue Display page was failing with a 401 Unauthorized error because it was attempting to call a protected backend endpoint.
**Accomplishments:**
*   **`sagrafacile-webapp/src/app/(public)/qdisplay/org/[orgSlug]/area/[areaId]/page.tsx` Updated:**
    *   Modified the `fetchInitialOverallState` function to call the new, correct public API endpoint: `/api/public/areas/{areaId}/queue/state`. This resolves the 401 error and allows the page to load data without authentication.
**Key Decisions:**
*   Aligned the frontend API call with the backend refactoring that moved the queue state endpoint to the public controller.

## (2025-06-09) - Enhanced Ad Assignment UI
**Context:** Improved the user experience in the ad assignment dialog.
**Accomplishments:**
*   **Conditional Duration Field:**
    *   Modified `sagrafacile-webapp/src/components/admin/AdAssignmentUpsertDialog.tsx`.
    *   The "Duration" field is now hidden and its value is set to `null` when the selected media is a video.
    *   The field remains visible for images, improving clarity and preventing unnecessary data entry for videos where duration is intrinsic.

## (2025-06-09) - Finalized and Debugged Dynamic Ad Carousel
**Context:** Completed the full implementation of the "Queue Display Advertising Carousel" feature, connecting the frontend to the backend and resolving several runtime issues.
**Accomplishments:**
*   **Dynamic Ad Carousel:**
    *   Updated the public Queue Display page (`/qdisplay/...`) to fetch promotional media dynamically from the new public backend endpoint (`/api/public/areas/{areaId}/ads`).
    *   Replaced the hardcoded media list with a state-driven approach, transforming the fetched DTOs into the format required by the `AdCarousel` component.
*   **Automatic Video Duration:**
    *   Enhanced the `AdUpsertDialog.tsx` component. When a video file is selected for upload, the dialog now automatically reads the video's metadata and populates the "Duration" field in the form, improving UX.
*   **Troubleshooting & Bug Fixes:**
    *   **CORS & URL Resolution:** Fixed media loading errors (404s) by ensuring the frontend constructs absolute URLs pointing to the backend server, correctly stripping the `/api` prefix from the base URL for static content.
    *   **Ad Blocker Evasion:** Changed the backend file storage path from `/media/ads` to `/media/promo` to prevent client-side ad blockers from blocking image requests.
    *   **Next.js Image Component:** Resolved `ERR_BLOCKED_BY_CLIENT` errors by adding the backend's hostname and port to the `images.remotePatterns` configuration in `next.config.js`, whitelisting it as a valid image source.
    *   **TypeScript Type Safety:** Corrected a type mismatch in the `qdisplay` page where a `null` value for `durationSeconds` from the API was not handled, ensuring type compatibility.

## (2025-06-09) - Implemented Ad Carousel Backend and Admin UI
**Context:** Continuing the implementation of the "Queue Display Advertising Carousel" feature, focusing on the backend API and the admin management interface.
**Accomplishments:**
*   **Backend API (SagraFacile.NET):**
    *   Created `MediaType` enum and `AdMediaItem` model.
    *   Added `AdMediaItems` `DbSet` to `ApplicationDbContext` and created/applied the database migration.
    *   Created `AdMediaItemDto` and `AdMediaItemUpsertDto`.
    *   Implemented `IAdMediaItemService` and `AdMediaItemService` with logic for creating, reading, updating, and deleting ad media, including file storage operations in `wwwroot/media/ads`.
    *   Created `AdMediaItemsController` with public and admin-only endpoints for managing ads.
*   **Frontend Admin UI (sagrafacile-webapp):**
    *   Created a new admin page at `/app/org/[orgId]/admin/ads` for managing ad media.
    *   The page allows selecting an `Area` and displays a table of existing ads for it.
    *   Created `AdUpsertDialog.tsx` component for adding new media (with file upload) and editing existing media metadata.
    *   Integrated full CRUD functionality: add, edit (metadata), and delete (including file removal via backend).
    *   Added a link "Pubblicità Display" to the admin sidebar for easy access.
*   **Updated `Roadmap.md`:**
    *   Marked Phase 2 of the "Queue Display Advertising Carousel" as in progress.

## (2025-06-09) - Implemented Ad Carousel on Public Queue Display
**Context:** The user requested to add a rotating display of images and videos to the bottom of the public-facing Queue Display page to be used for advertisements on large TV screens.
**Accomplishments:**
*   **Created `docs/AdCarouselArchitecture.md`:**
    *   Authored a new architecture document outlining the full, two-phase implementation plan.
    *   Phase 1 covers the frontend component with hardcoded data.
    *   Phase 2 details the backend API, database schema (`AdMediaItems` table), and local file storage strategy for a dynamic, admin-managed system compatible with a self-hosted Docker environment.
*   **Created `sagrafacile-webapp/src/components/public/AdCarousel.tsx`:**
    *   Developed a new, reusable component to display a carousel of media items.
    *   It accepts an array of `AdMedia` objects (`{type, src, durationSeconds}`).
    *   It automatically rotates through items, handling images with a timer and videos via the `onEnded` event.
*   **Updated `sagrafacile-webapp/src/app/(public)/qdisplay/org/[orgSlug]/area/[areaId]/page.tsx`:**
    *   The page's root layout was changed to a vertical flex column (`h-screen`).
    *   The main content (queue display) now occupies the top, flexible-growth section.
    *   A new `<footer>` element with a fixed height (`25vh`) was added to the bottom to house the `AdCarousel`.
    *   The carousel was integrated with a hardcoded list of placeholder media for immediate demonstration.
*   **Updated `Roadmap.md`:**
    *   Added a new "Phase 5.6: Queue Display Advertising Carousel" section to formally track this feature, marking the documentation and Phase 1 frontend work as complete or in progress.

## (2025-06-09) - Optimized Cashier Order Panel UI for Vertical Space
**Context:** The user requested to reduce the vertical footprint of the `CashierOrderPanel` to maximize the visible area for the product list, which is crucial on smaller screens.
**Accomplishments:**
*   **`sagrafacile-webapp/src/components/cashier/CashierOrderPanel.tsx` Refactored:**
    *   **Compacted Queue Controls:** Reorganized the customer queue system UI. The "Now Serving" and "Next" number displays are now grouped horizontally with the main "Call Next" button, creating a single, dense primary action row.
    *   The secondary queue actions ("Repeat Last" and "Call Specific") were grouped into a second row below, further saving space. The "Repeat" button label was shortened to "Ripeti" and the "Call" button for the specific number was changed to an icon-only button.
    *   **Reduced Order Item Spacing:** The vertical padding (`py-2` to `py-1.5`) and overall vertical space (`space-y-2` to `space-y-1`) for items in the cart list were reduced. This allows more items to be displayed in the scrollable area without changing the font size.
**Key Decisions:**
*   Prioritized UI density in the control panel to enhance usability on devices with limited vertical screen real estate. The changes make the product list more central to the user experience by giving it more room.

## (2025-06-08) - Display Guest and Takeaway Charges in UI
**Context:** Following the backend implementation of guest and takeaway charges, the frontend UI needed to be updated to display these charges in all relevant contexts (live orders and historical receipts).
**Accomplishments:**
*   **`sagrafacile-webapp/src/types/index.ts` Updated:**
    *   Added `guestCharge` and `takeawayCharge` to the `OrderDto` and `KdsOrderDto` interfaces to ensure the frontend receives the calculated charge data from the backend.
*   **`sagrafacile-webapp/src/components/ReceiptDialog.tsx` Updated:**
    *   Modified the dialog to display a full cost breakdown. It now shows a subtotal of items, a line for "Coperto" (guest charge), a line for "Asporto" (takeaway charge) if applicable, and then the final total amount.
*   **`sagrafacile-webapp/src/hooks/useOrderHandler.ts` Refactored:**
    *   The `openReceiptDialog` function was updated to accept a comprehensive `orderTotals` object instead of just the `cartTotal`. This ensures the preview receipt is generated with the correct final amount including all charges.
*   **`sagrafacile-webapp/src/app/app/org/[orgId]/cashier/area/[areaId]/page.tsx` Updated:**
    *   Implemented a `useMemo` hook (`orderTotals`) to calculate the subtotal, guest charge, takeaway charge, and final total based on the current cart, number of guests, and takeaway status.
    *   This `orderTotals` object is now passed to the `CashierOrderPanel` for live display and to the `useOrderHandler`'s `openReceiptDialog` function.
*   **`sagrafacile-webapp/src/components/cashier/CashierOrderPanel.tsx` Updated:**
    *   The component now receives the `orderTotals` prop and displays the full cost breakdown (subtotal, charges, total), giving the cashier a clear view before payment.
*   **`sagrafacile-webapp/src/app/app/org/[orgId]/table-order/area/[areaId]/page.tsx` Updated:**
    *   Similar to the Cashier page, this page now calculates the `orderTotals` and passes them to the `useOrderHandler`.
    *   The cart/checkout sheet was updated to display the full cost breakdown, ensuring clarity for the user placing the order.
**Key Decisions:**
*   Centralized the total calculation logic within the pages that manage order state (`CashierPage`, `MobileTableOrderPage`) and passed the results down to the display components. This keeps the UI components clean and ensures consistency.
*   The `ReceiptDialog` now serves as a universal component for displaying a detailed cost breakdown for both new and historical orders.
*   **`sagrafacile-webapp/src/app/app/org/[orgId]/admin/orders/page.tsx` Updated:**
    *   When opening the reprint dialog, the component now finds the associated area for the historical order.
    *   It calculates the `guestCharge` and `takeawayCharge` based on the area's rules and the order's details (`isTakeaway`, `numberOfGuests`).
    *   An "augmented" `OrderDto` object containing these calculated charges is passed to the `ReceiptDialog`, ensuring the historical receipt displays the full cost breakdown correctly.
*   **`sagrafacile-webapp/src/components/cashier/ReprintOrderDialog.tsx` Updated:**
    *   The component now fetches the details of the current `Area` in addition to the list of historical orders.
    *   When a user clicks to reprint an order, the component uses the fetched area details to calculate the `guestCharge` and `takeawayCharge`.
    *   Similar to the admin page, it passes an augmented `OrderDto` to the `ReceiptDialog` so that the reprint shows the correct and complete cost breakdown.
**Key Decisions:**
*   Replicated the total calculation logic from the live cashier interface to the historical order views. This ensures that receipts, whether for new or old orders, are consistent in their presentation of charges.
*   Instead of modifying the `ReceiptDialog` further, the pages responsible for opening it are now in charge of preparing the `OrderDto` with all necessary charge information, keeping the dialog's responsibility focused on display.


## (2025-06-08) - Implemented Guest and Takeaway Charges
**Context:** The user wanted to add the ability to configure a per-guest "coperto" (cover charge) and a per-order "asporto" (takeaway) fee.
**Accomplishments:**
*   **`sagrafacile-webapp/src/types/index.ts` Updated:**
    *   Added `guestCharge` and `takeawayCharge` to the `AreaDto` interface.
*   **`sagrafacile-webapp/src/app/app/org/[orgId]/admin/areas/page.tsx` Updated:**
    *   Added input fields for "Guest Charge" and "Takeaway Charge" to the "Add New Area" and "Edit Area" dialogs.
    *   Updated the main table to display the configured charges for each area.
    *   The `AreaUpsertDto` interface and related state management and submission logic were updated to handle the new fields.
**Key Decisions:**
*   The frontend now allows administrators to view and manage these new area-specific charges, ensuring the UI is synchronized with the new backend capabilities.

## (2025-06-07) - Unified QR Code Scanning and Order Viewing
**Context:** The ability to scan a receipt's QR code to view or confirm an order was present in the Waiter interface but needed to be added to the Mobile Table Order page and unified for reusability.
**Accomplishments:**
*   **Created `sagrafacile-webapp/src/components/shared/OrderQrScanner.tsx`:**
    *   A new reusable component was created to handle the logic and UI for QR code scanning.
    *   It uses `@yudiel/react-qr-scanner` and includes logic to extract the order GUID from the scanned value.
    *   A `forceShowScanner` prop was added to allow it to be used directly within a dialog without needing an initial button click.
*   **Created `sagrafacile-webapp/src/components/shared/OrderConfirmationView.tsx`:**
    *   Extracted the order detail and confirmation logic from the Waiter page into a new, reusable component.
    *   This component fetches an order by its ID and displays its details.
    *   If the order is in a `Paid` or `PreOrder` state, it allows a waiter to assign a `tableNumber` and confirm it for preparation.
    *   If the order is in any other state, it displays the details in a read-only mode.
*   **Refactored Waiter Page (`sagrafacile-webapp/src/app/app/org/[orgId]/waiter/area/[areaId]/page.tsx`):**
    *   The page was updated to use the new `OrderQrScanner` and `OrderConfirmationView` components, significantly reducing its own code complexity.
    *   The local, duplicated implementation of the confirmation view was removed.
*   **Enhanced Mobile Table Order Page (`sagrafacile-webapp/src/app/app/org/[orgId]/table-order/area/[areaId]/page.tsx`):**
    *   Added a "Scansiona" button to the header.
    *   Clicking the button opens a dialog that uses the `OrderQrScanner` component.
    *   Upon a successful scan, the dialog displays the order details using the `OrderConfirmationView` component. This allows users on the table ordering page to quickly check an existing order's status.
*   **Enhanced Cashier Page (`sagrafacile-webapp/src/app/app/org/[orgId]/cashier/area/[areaId]/page.tsx` & `.../CashierOrderPanel.tsx`):**
    *   Added a "Scansiona QR" button to the `CashierOrderPanel`.
    *   This button opens a dialog similar to the one on the table order page, using `OrderQrScanner` and `OrderConfirmationView` to allow the cashier to check an order's status or confirm a paid one without leaving the interface.
**Key Decisions:**
*   Centralized QR scanning and order confirmation/viewing logic into shared components (`OrderQrScanner`, `OrderConfirmationView`) to promote reusability and maintainability.
*   This approach ensures a consistent user experience across the Waiter, Mobile Table Order, and Cashier interfaces when interacting with order QR codes.

**idea for the future: order status funnel dashboard, stuck order alerts for order stuck on first statuses??**

## (2025-06-07) - Refactored Waiter Interface to be Area-Specific
**Context:** The waiter interface was previously a single page. It has been refactored to follow the same pattern as the Cashier and Table Order interfaces, with a dedicated area selection step.
**Accomplishments:**
*   **Moved and Updated Waiter Page (`sagrafacile-webapp/src/app/app/org/[orgId]/waiter/area/[areaId]/page.tsx`):**
    *   The existing waiter page was moved to a new, dynamic route that includes an `areaId`.
    *   The page logic was updated to use the `areaId` from the URL to fetch and display orders only for the selected area, making it context-aware.
*   **Created Waiter Area Selection Page (`sagrafacile-webapp/src/app/app/org/[orgId]/waiter/page.tsx`):**
    *   A new page was created at the former waiter URL.
    *   This page now utilizes the reusable `AreaSelector` component, prompting the user to choose an operational area.
    *   Upon selection, it redirects the user to the corresponding area-specific waiter page.
**Key Decisions:**
*   Standardized the Waiter workflow to align with other role-based, area-specific interfaces in the application. This improves consistency and scalability.

## (2025-06-07) - Refactored Area Selection and Added Table Order Area Selector
**Context:** The user requested to make the area selection logic from the cashier page reusable and apply it to a new selection page for table orders.
**Accomplishments:**
*   **Created `sagrafacile-webapp/src/components/shared/AreaSelector.tsx`:**
    *   Developed a new reusable component that fetches and displays areas for a given `orgId`.
    *   It handles loading states, errors, and auto-selection if only one area is available.
    *   Props allow customization of titles, texts, and button appearance.
*   **Refactored Cashier Area Selection (`sagrafacile-webapp/src/app/app/org/[orgId]/cashier/page.tsx`):**
    *   Updated this page to use the new `AreaSelector` component, simplifying its own logic.
    *   The page now passes a callback to `AreaSelector` which handles navigation to the specific cashier area page (`/app/org/[orgId]/cashier/area/[areaId]`).
*   **Created Table Order Area Selection Page (`sagrafacile-webapp/src/app/app/org/[orgId]/table-order/page.tsx`):**
    *   Added a new page that uses the `AreaSelector` component.
    *   Upon area selection, it redirects to the corresponding table order page (`/app/org/[orgId]/table-order/area/[areaId]`).
*   **Updated Admin Layout (`sagrafacile-webapp/src/app/app/org/[orgId]/admin/layout.tsx`):**
    *   Added a new navigation link "Table Orders" under the "Operations" section in the admin sidebar.
    *   This link points to the new `/app/org/[orgId]/table-order` page, allowing admins/waiters to select an area for table ordering.
**Key Decisions:**
*   Centralized area selection logic into a reusable `AreaSelector` component to reduce code duplication and improve maintainability.
*   Created a dedicated area selection precursor page for the table ordering flow, rather than embedding selection directly into the existing `table-order/area/[areaId]/page.tsx`.

## (2025-06-07) - Implemented Mobile Table Ordering Page (Phase 1)
**Context:** Started implementation of the "Mobile Table Ordering & Payment Interface" as outlined in `Roadmap.md` (Phase 6) and `docs/MobileTableOrderingArchitecture.md`.
**Accomplishments:**
*   **Created `sagrafacile-webapp/src/app/app/org/[orgId]/table-order/area/[areaId]/page.tsx`:**
    *   Established the basic structure for the mobile table ordering page, adapting UI elements and logic from `preorder-page.txt` (for menu display, cart sheet) and `CashierPage.tsx` (for payment concepts).
    *   Integrated `AuthContext` and `useParams` for user and route information.
    *   Implemented data fetching for `AreaDto`, `MenuCategoryDto[]`, and `MenuItemDto[]` using `apiClient` based on `orgId` and `areaId`. `MenuItemDto`s are augmented with `categoryName` client-side.
    *   Set up a Zod schema (`tableOrderFormSchema`) and `react-hook-form` for order details: `customerName`, `tableNumber` (both mandatory), `isTakeaway`, and `numberOfGuests`.
    *   Implemented client-side cart functionality (`MobileCartItem` local type extending `CartItem` with `categoryName`):
        *   Adding items to the cart.
        *   Increasing/decreasing quantities.
        *   Removing items.
        *   Managing item notes via an `AlertDialog`.
    *   Integrated a draggable bottom sheet for cart display and checkout, adapted from `preorder-page.txt`.
    *   Added "Paga Contanti" and "Paga POS" buttons. Clicking these buttons:
        *   Constructs a preview `OrderDto`.
        *   Opens the `ReceiptDialog` component, passing the preview order and payment details.
    *   The actual order submission to `POST /api/orders` (via `submitOrderToServer` function) is triggered from within the `ReceiptDialog`'s confirmation action. The `CreateOrderDto` includes `tableNumber` and sets `cashierId` to the current user's ID.
    *   Integrated SignalR (`useSignalRHub`) for real-time `ReceiveStockUpdate` messages to keep menu item stock levels current.
    *   **UI/UX Refinements for Table Order Page:**
        *   **Default "Coperti" (Number of Guests) to 0:** Changed the default value for `numberOfGuests` to `0` in the form. The `useEffect` hook that manages the interaction between `isTakeaway` and `numberOfGuests` was updated to no longer automatically set `numberOfGuests` to `1` when `isTakeaway` is false. The Zod schema validation (`refine` method) will ensure that `numberOfGuests` is at least `1` for non-takeaway orders upon submission. This change caters to scenarios where customers are already seated and placing subsequent orders.
        *   Adjusted the layout of "Nome Cliente" and "Numero Tavolo" input fields to be on the same row within the cart sheet form, making "Numero Tavolo" smaller and positioned to the right.
        *   Removed the drag-down-to-close functionality for the cart sheet to prevent interference with scrolling cart items. The close action is now solely handled by an "X" button, which includes a "Chiudi Riepilogo" label on larger screens.
        *   Changed the default note for items requiring a note to an empty string when added to the cart, and in the note editing dialog, to ensure users actively input notes.
        *   Modified payment buttons ("Paga Contanti", "Paga POS") to no longer be disabled if a required note is missing. Instead, clicking them with a missing required note will display an error toast and prevent payment processing.
        *   Conditionally rendered the "Scorta" (stock) label for menu items, so it only appears if `scorta` has a value (is not `null`).
*   **Updated `sagrafacile-webapp/src/types/index.ts`:**
    *   Added `tableNumber?: string;` and `cashierId?: string;` to `CreateOrderDto`.
    *   Added `cartItemId: string;` to `CartItem` interface for client-side unique identification of cart entries.
**Key Decisions:**
*   Leveraged existing components and patterns (`preorder-page.txt` for UI, `CashierPage.tsx` for flows, `ReceiptDialog` for submission).
*   `categoryName` is added to `MenuItemDto` client-side after fetching and used in a local `MobileCartItem` interface for cart state.
*   Order submission is a two-step process: payment buttons prepare data and open `ReceiptDialog`, which then handles the final call to `submitOrderToServer`.
**Next Steps (User Testing & Refinement):**
*   Thoroughly test the new Mobile Table Ordering page:
    *   Menu display and item interaction (add to cart, stock display).
    *   Cart management (quantities, notes, removal).
    *   Order form validation (`customerName`, `tableNumber`, `numberOfGuests`).
    *   Payment flow (Cash & POS) and `ReceiptDialog` integration.
    *   Verify correct `CreateOrderDto` is sent to the backend.
    *   Test SignalR stock updates.
*   Refine mobile-specific styling and UX.
*   Ensure route protection for "Waiter" and "Admin" roles (to be added in a subsequent step, likely in a layout file).

## (2025-06-06) - Enhanced Pre-order Scan with Immediate Stock Warning
**Context:** Improved the UX for handling pre-orders by providing an earlier, client-side warning about potential stock issues.
**Accomplishments:**
*   **Cashier Page Updated (`sagrafacile-webapp/src/app/app/org/[orgId]/cashier/area/[areaId]/page.tsx`):**
    *   Modified the `handleScanResult` function.
    *   After a pre-order is successfully scanned and its items are loaded into the cart, an immediate client-side check is performed against the current `menuItems` state (which includes `scorta` updated by SignalR).
    *   If any item in the scanned pre-order has a requested quantity greater than its currently known `scorta` (or if `scorta` is 0), a warning toast notification is displayed to the cashier.
    *   The toast message lists the specific items with potential stock issues and their requested vs. available quantities (e.g., "Attenzione: I seguenti articoli nel pre-ordine potrebbero avere scorte insufficienti o essere esauriti: [Item A (Richiesti: X, Disponibili: Y)], [Item B (Esaurito)]. La verifica finale avverrà al momento del pagamento.").
    *   This provides an early heads-up to the cashier, allowing them to discuss potential issues with the customer sooner. The definitive backend stock check during payment confirmation remains the authoritative step.
**Next Steps (User Testing):**
*   User to test scanning pre-orders where some items might have insufficient stock to verify the new warning toast appears correctly and provides useful information.

## (2025-06-06) - Adjust Cashier Order Panel Styling for Laptop Screens
**Context:** The right column of the cashier interface (Order Panel) was taking up too much horizontal space on laptop screens, making the order summary less visible.
**Accomplishments:**
*   **CashierOrderPanel.tsx Styling Adjustments (`sagrafacile-webapp/src/components/cashier/CashierOrderPanel.tsx`):**
    *   Removed the "Ordine Corrente" title from the `CardHeader` to save vertical space.
    *   Adjusted `CardHeader` padding.
    *   Reduced padding and margins within the "Sistema Coda Clienti" (Customer Queue System) section:
        *   `CardContent` padding changed from `p-4` to `p-3`.
        *   Section title bottom margin changed from `mb-3` to `mb-2`.
        *   Vertical spacing within the queue content changed from `space-y-3` to `space-y-2`.
        *   Gap between queue action buttons changed from `gap-2` to `gap-1`.
        *   Horizontal spacing for the specific number input area changed from `space-x-2` to `space-x-1`.
    *   Reduced padding and margins within the `CardFooter` (checkout section):
        *   `CardFooter` top padding changed from `pt-4` to `pt-3`.
        *   Bottom margins for various elements within the footer changed from `mb-3` to `mb-2`.
        *   Gap between payment buttons changed from `gap-3` to `gap-2`.
    *   Removed the unused `CardTitle` import from `@/components/ui/card` to resolve an ESLint warning.
**Next Steps (User Testing):**
*   User to verify the Cashier UI on laptop screens to confirm the Order Panel now takes up less space and the order summary is more visible.

## (Next Session) - Planned Work
**Context:** Current session paused debugging of USB thermal printer due to issues with the `SagraFacile.WindowsPrinterService` companion app's registration with the SignalR hub. This impacts both backend and frontend testing of the printing feature.
**Next Steps (Collaborative - Backend & Companion App Focus First):**
1.  **Enhance `SagraFacile.WindowsPrinterService` (Companion App):**
    *   Improve the UI/UX for displaying connection status to the SignalR hub.
    *   Provide a clearer way to configure the necessary settings (SignalR Hub URL, Printer GUID).
    *   Implement better logging within the companion app to aid troubleshooting.
2.  **Resume USB Thermal Printer Debugging (Backend & Frontend):**
    *   Once the companion app is improved and its connection/registration can be reliably verified, continue debugging the USB thermal printer functionality.
    *   Backend: Focus on ensuring the companion app correctly registers with the `OrderHub` using the matching GUID and that print jobs are dispatched.
    *   Frontend: No direct changes anticipated for this specific debugging, but successful printing is needed to fully test cashier workflows.


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
