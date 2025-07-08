# SagraFacile - First-Time Setup Wizard Architecture

This document outlines the architecture for a guided setup wizard designed to improve the onboarding experience for new organization administrators.

## 1. Goals & Principles

*   **Reduce Friction:** Guide new users through the essential initial setup steps, reducing the "empty state" problem.
*   **Educate:** Briefly explain the core concepts of the system (Areas, Printers, etc.) as they are being created.
*   **Minimum Viable Setup:** The wizard should focus on creating the bare minimum entities required to start operating, not every possible configuration.
*   **Non-Intrusive:** The wizard should be easy to dismiss or skip for users who prefer to configure the system manually.

## 2. Triggering Logic

The wizard will be triggered automatically based on the state of the organization's configuration.

*   **Trigger Condition:** The wizard will launch if an authenticated `Admin` user navigates to the main admin dashboard (`/app/org/[orgId]/admin`) and the system detects that the organization has **zero `Areas`**.
*   **Implementation:**
    *   The main admin dashboard page (`/app/app/org/[orgId]/admin/page.tsx`) will fetch the list of Areas.
    *   A `useEffect` hook will check if `areas.length === 0` after the fetch is complete.
    *   If the condition is met, a state variable (e.g., `showSetupWizard`) will be set to `true`, conditionally rendering the wizard component.
*   **Dismissal:**
    *   The wizard will have a "Set up later" or "Skip" button.
    *   Clicking this will set a flag in the browser's `localStorage` (e.g., `setupWizardSkipped_org_{orgId} = true`).
    *   The trigger logic will check for this flag and will not launch the wizard automatically if it's present. The user could still re-launch it manually from a link on the dashboard.

## 3. UI/UX Flow (Multi-Step Modal)

The wizard will be implemented as a multi-step modal dialog.

### Step 1: Welcome

*   **UI:** A simple, welcoming message.
*   **Content:** "Welcome to SagraFacile! This quick wizard will help you set up the basics for your event in just a few minutes."
*   **Actions:** "Let's Start" (Next), "I'll do this later" (Skip).

### Step 2: Create Your First Area

*   **UI:** A simple form.
*   **Content:**
    *   **Explanation:** "An 'Area' is an operational zone, like your main kitchen, bar, or a specific food stand. Let's create your first one."
    *   **Field:** `Area Name` (e.g., "Cucina Principale").
*   **Action:** On "Next", the wizard calls `POST /api/Areas`, creates the Area, and stores the returned `area.id` in its internal state for the next steps.

### Step 3: Set Up a Printer

*   **UI:** A simple form.
*   **Content:**
    *   **Explanation:** "Now, let's add a printer. This can be a receipt printer at the cashier or a ticket printer in the kitchen."
    *   **Fields:** `Printer Name`, `Type` (Dropdown: Network, Windows USB), `Connection Info` (IP:Port or GUID).
*   **Action:** On "Next", calls `POST /api/printers`, creates the Printer, and stores the `printer.id`.

### Step 4: Create a Cashier Station

*   **UI:** A simple form.
*   **Content:**
    *   **Explanation:** "A 'Cashier Station' is a point of sale. Let's link the Area and Printer you just created."
    *   **Fields:** `Station Name` (e.g., "Cassa 1").
    *   The `Area` and `Receipt Printer` fields will be pre-filled and read-only, showing the names of the entities created in the previous steps.
*   **Action:** On "Next", calls `POST /api/cashierstations/...` to create the station.

### Step 5: Create Your First Menu Item

*   **UI:** A combined form.
*   **Content:**
    *   **Explanation:** "Let's create your first menu category and a sample item to get you started."
    *   **Fields:** `Category Name` (e.g., "Primi Piatti"), `Item Name` (e.g., "Spaghetti al Ragù"), `Item Price`.
*   **Action:** On "Next", it first calls the API to create the `MenuCategory`, then uses the returned category ID to create the `MenuItem`.

### Step 6: All Done!

*   **UI:** A confirmation and summary screen.
*   **Content:** "Setup Complete! You've successfully configured the basics. You can now explore the dashboard, add more to your menu, or head straight to the cashier interface."
*   **Actions:**
    *   "Go to Dashboard" button.
    *   "Manage Menu" link.
    *   "Finish" button (closes the modal).

## 4. Component Architecture (Frontend)

The wizard components will be located in `sagrafacile/sagrafacile-webapp/src/components/admin/setup-wizard/`.

*   **`SetupWizard.tsx`:**
    *   The main stateful component.
    *   Manages `isOpen`, `currentStep`, and state for the created entities (`createdArea`, `createdPrinter`, etc.).
    *   Renders the current step component based on `currentStep`.
    *   Contains the main `Dialog` wrapper and the Next/Back/Finish buttons.

*   **`Step[Name].tsx` (e.g., `StepArea.tsx`, `StepPrinter.tsx`):**
    *   Stateless components that receive callbacks (`onNext`, `onBack`) and data as props.
    *   Contain the UI and form elements for each specific step.
    *   They call the relevant API services and pass the result back to the parent `SetupWizard` via the `onNext` callback.

## 5. API Dependencies

The wizard relies on the following existing API endpoints. No new backend endpoints are required.

*   `POST /api/Areas`
*   `POST /api/Printers`
*   `POST /api/CashierStations/organization/{orgId}`
*   `POST /api/MenuCategories`
*   `POST /api/MenuItems`

## 6. Roadmap Integration

This feature will be added to the main `Roadmap.md` under a suitable phase (e.g., Phase 11, alongside other SaaS/Onboarding features) to ensure it is tracked.
