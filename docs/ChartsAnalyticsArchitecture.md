# Charts & Analytics Architecture - SagraFacile

## Overview

This document outlines the architecture for implementing charts and analytics in the SagraFacile admin interface. The feature will provide visual insights into order data, sales trends, and operational metrics to help administrators make informed decisions.

## Goals

- **Dashboard KPIs**: Display key performance indicators on the admin home page
- **Order Analytics**: Provide detailed charts on the orders admin page
- **Responsive Design**: Show KPIs only on mobile, full charts on desktop/tablet
- **Day-Based Analysis**: Leverage the operational day (Giornata) system for accurate reporting
- **Real-time Updates**: Periodic refresh of data (not real-time SignalR)
- **Export Capabilities**: Enable report generation for administrative purposes

## Technical Stack

- **Charts Library**: Shadcn/ui Charts (built on Recharts)
- **Data Source**: .NET API endpoints (no direct database access)
- **Refresh Strategy**: Periodic API calls (configurable intervals)
- **Responsive Strategy**: CSS-based hiding/showing of components
- **Date Range**: Default 7 days with custom range capability

## Architecture Components

### 1. Frontend Components Structure

The frontend will feature a modular structure for chart components located under `src/components/charts/`. This includes:
-   **Dashboard Components**: For displaying KPIs and overview charts (e.g., `DashboardKPIs`, `SalesTrendChart`, `OrderStatusChart`, `TopMenuItemsChart`).
-   **Orders Page Components**: For detailed analytics related to orders (e.g., `OrdersByHourChart`, `PaymentMethodsChart`, `AverageOrderValueChart`, `OrderStatusTimelineChart`).
-   **Shared Components**: Reusable elements like chart containers, custom tooltips, loading states, and empty states (e.g., `ChartContainer`, `ChartTooltip`, `LoadingChart`, `EmptyChart`).

### 2. Backend API Endpoints

#### Analytics Controller (`/api/analytics`)

```csharp
// Dashboard KPIs
GET /api/analytics/dashboard/kpis?organizationId={id}&dayId={dayId}
GET /api/analytics/dashboard/sales-trend?organizationId={id}&days={days}
GET /api/analytics/dashboard/order-status?organizationId={id}&dayId={dayId}
GET /api/analytics/dashboard/top-menu-items?organizationId={id}&days={days}&limit={limit}

// Orders Analytics
GET /api/analytics/orders/by-hour?organizationId={id}&areaId={areaId}&dayId={dayId}
GET /api/analytics/orders/payment-methods?organizationId={id}&areaId={areaId}&dayId={dayId}
GET /api/analytics/orders/average-value-trend?organizationId={id}&areaId={areaId}&days={days}
GET /api/analytics/orders/status-timeline?organizationId={id}&areaId={areaId}&dayId={dayId}

// Reports
GET /api/analytics/reports/daily-summary?organizationId={id}&dayId={dayId}
GET /api/analytics/reports/area-performance?organizationId={id}&startDate={date}&endDate={date}
```

#### DTOs for Analytics

The backend will use specific Data Transfer Objects (DTOs) to structure the data returned by the analytics endpoints. Key DTOs include:

-   **`DashboardKPIsDto`**: Aggregated Key Performance Indicators for the dashboard (e.g., total sales, order count for a specific day).
    -   `TodayTotalSales`, `TodayOrderCount`, `AverageOrderValue`, `MostPopularCategory`, `DayId`, `DayDate`.
-   **`SalesTrendDataDto`**: Data points for sales trends over a period (e.g., daily sales and order counts).
    -   `Date`, `Sales`, `OrderCount`, `DayId?`.
-   **`OrderStatusDistributionDto`**: Distribution of orders by their current status for a specific day.
    -   `Status`, `Count`, `Percentage`.
-   **`TopMenuItemDto`**: Information about top-selling menu items.
    -   `ItemName`, `CategoryName`, `Quantity`, `Revenue`.
-   **`OrdersByHourDto`**: Order counts and revenue aggregated by the hour for a specific day.
    -   `Hour`, `OrderCount`, `Revenue`.
-   **`PaymentMethodDistributionDto`**: Distribution of orders by payment method for a specific day.
    -   `PaymentMethod`, `Count`, `Amount`, `Percentage`.
-   **`AverageOrderValueTrendDto`**: Trend of average order values over a period.
    -   `Date`, `AverageValue`, `OrderCount`, `DayId?`.
-   **`OrderStatusTimelineEventDto`**: Represents an event in an order's lifecycle for timeline visualization.
    -   `OrderId`, `DisplayOrderNumber`, `Status`, `Timestamp`, `PreviousStatus?`, `DurationInPreviousStatusMinutes?`.
    -   *Note: Due to the current `Order` model structure (lacking individual status timestamps), this DTO will primarily reflect the order creation/last update time and its current status. True timeline event tracking would require model changes or a dedicated history table.*

### 3. Service Layer

#### AnalyticsService

```csharp
public interface IAnalyticsService
{
    // Dashboard
    Task<DashboardKPIsDto> GetDashboardKPIsAsync(int organizationId, int? dayId = null);
    Task<List<SalesTrendDataDto>> GetSalesTrendAsync(int organizationId, int days = 7);
    Task<List<OrderStatusDistributionDto>> GetOrderStatusDistributionAsync(int organizationId, int? dayId = null);
    Task<List<TopMenuItemDto>> GetTopMenuItemsAsync(int organizationId, int days = 7, int limit = 5);
    
    // Orders Analytics
    Task<List<OrdersByHourDto>> GetOrdersByHourAsync(int organizationId, int? areaId = null, int? dayId = null);
    Task<List<PaymentMethodDistributionDto>> GetPaymentMethodDistributionAsync(int organizationId, int? areaId = null, int? dayId = null);
    Task<List<AverageOrderValueTrendDto>> GetAverageOrderValueTrendAsync(int organizationId, int? areaId = null, int days = 7);
    Task<List<OrderStatusTimelineEventDto>> GetOrderStatusTimelineAsync(int organizationId, int? areaId = null, int? dayId = null);
    
    // Reports
    Task<byte[]> GenerateDailySummaryReportAsync(int organizationId, int dayId); // Returns byte array for file download
    Task<byte[]> GenerateAreaPerformanceReportAsync(int organizationId, DateTime startDate, DateTime endDate); // Returns byte array
}
```

#### Key Algorithms & Logic in `AnalyticsService`
-   **`GetTargetDayAsync` (Helper)**: Determines the relevant operational `Day` entity. If `dayId` is provided, it fetches that specific day. Otherwise, it attempts to find the current open `Day`, or the most recently closed `Day` if none are open.
-   **`GetDashboardKPIsAsync`**:
    1.  Identifies the target operational day using `GetTargetDayAsync`.
    2.  Filters orders for that day and organization, excluding non-terminal statuses (PreOrder, Pending, Cancelled).
    3.  Calculates total sales, order count, and average order value.
    4.  Determines the most popular menu category by summing quantities of items sold from each category.
-   **`GetSalesTrendAsync`**:
    1.  Calculates a date range based on the `days` parameter.
    2.  Groups orders by their operational day's date within this range.
    3.  Aggregates total sales and order counts for each day.
    4.  Ensures all days in the range are present in the result, filling with zero values if no sales data exists.
-   **`GetOrderStatusDistributionAsync`**:
    1.  Identifies the target operational day.
    2.  Groups orders for that day by their `Status`.
    3.  Calculates the count for each status and its percentage of the total orders for that day.
-   **`GetTopMenuItemsAsync`**:
    1.  Calculates a date range.
    2.  Groups `OrderItems` within this period by `MenuItemId`, `Name`, and `CategoryName`.
    3.  Sums quantity sold and total revenue for each item.
    4.  Orders by quantity then revenue, taking the specified `limit`.
-   **`GetOrdersByHourAsync`**:
    1.  Identifies the target operational day.
    2.  Filters orders by `AreaId` if provided.
    3.  Groups orders by the hour of their `OrderDateTime`.
    4.  Aggregates order count and revenue for each hour.
    5.  Ensures all 24 hours are present in the result, filling with zero values if no data exists.
-   **`GetPaymentMethodDistributionAsync`**:
    1.  Identifies the target operational day.
    2.  Filters orders by `AreaId` if provided, considering only orders with a `PaymentMethod`.
    3.  Groups orders by `PaymentMethod`.
    4.  Calculates count, total amount, and percentage of total sales for each payment method.
-   **`GetAverageOrderValueTrendAsync`**:
    1.  Calculates a date range.
    2.  Filters orders by `AreaId` if provided.
    3.  Groups orders by their operational day's date.
    4.  For each day, calculates total sales and order count to derive the average order value.
    5.  Ensures all days in the range are present, filling with zero values if no data exists.
-   **`GetOrderStatusTimelineAsync`**:
    1.  Identifies the target operational day.
    2.  Filters orders by `AreaId` if provided.
    3.  For each order, creates a single timeline event using the `Order.OrderDateTime` as the timestamp and the current `Order.Status`. `PreviousStatus` and `DurationInPreviousStatusMinutes` are set to `null` due to the `Order` model not storing individual status transition timestamps.
-   **Report Generation (`GenerateDailySummaryReportAsync`, `GenerateAreaPerformanceReportAsync`)**:
    1.  Fetch relevant order data based on parameters (dayId or date range).
    2.  Aggregate data (e.g., total sales, sales by category, top items for daily; sales by area for performance).
    3.  Currently, format this data into a simple UTF-8 encoded text string. *Future enhancement: Integrate a library for PDF/Excel generation.*

### 4. Database Queries Strategy

#### Leveraging Operational Days (Giornata)

All analytics queries will use the `Day` table to ensure accurate reporting:

```sql
-- Example: Sales by day using Day table
SELECT 
    d.Id as DayId,
    d.StartTime as Date,
    SUM(o.TotalAmount) as Sales,
    COUNT(o.Id) as OrderCount
FROM Days d
LEFT JOIN Orders o ON o.DayId = d.Id
WHERE d.OrganizationId = @organizationId
    AND d.StartTime >= @startDate
    AND d.StartTime <= @endDate
GROUP BY d.Id, d.StartTime
ORDER BY d.StartTime DESC
```

#### Key Query Patterns

1.  **Current Day Queries**: Utilize the `GetTargetDayAsync` helper in the service layer. This helper first checks for an open `Day` (`Day.Status == DayStatus.Open`). If none is found, it looks for the most recently closed `Day`. Queries are then filtered by the `DayId` of this target day.
2.  **Historical Queries**: Use date ranges applied to `Day.StartTime` for selecting relevant operational days.
3.  **Area Filtering**: Join through `Orders.AreaId` or filter directly on `Orders.AreaId` if the query starts from the `Orders` table. Ensure `AreaId` belongs to the `OrganizationId`.
4.  **Time-based Grouping**:
    *   **Daily Grouping**: Group by `Day.StartTime.Date`.
    *   **Hourly Grouping**: Group by `Orders.OrderDateTime.Hour` for orders within a specific target day.
5.  **Excluding Non-Terminal Orders**: Most analytical queries should exclude orders with statuses like `PreOrder`, `Pending`, or `Cancelled` to reflect actual sales and operational data.

### 5. Frontend Implementation Details

#### Dashboard Home Page Integration
The admin dashboard page (`/admin/page.tsx`) will:
- Fetch and display `DashboardKPIsDto` data using the `DashboardKPIs` component.
- Conditionally render more detailed charts (`SalesTrendChart`, `OrderStatusChart`, `TopMenuItemsChart`) for desktop/tablet views using a `useMediaQuery` hook.

#### Orders Page Integration
The orders admin page (`/admin/orders/page.tsx`) will:
- Integrate a new analytics section below the existing orders table.
- Display charts like `OrdersByHourChart`, `PaymentMethodsChart`, `AverageOrderValueChart`, and `OrderStatusTimelineChart`.
- These charts will be reactive to existing filters on the page (e.g., selected Area, selected Day).

#### Chart Component Pattern
Chart components (e.g., `SalesTrendChart.tsx`) will typically:
- Accept properties like `organizationId`, date range parameters (`days`), and optional filters (`areaId`, `dayId`).
- Manage their own state for data, loading status, and errors.
- Fetch data from the `analyticsService` within a `useEffect` hook.
- Implement periodic data refresh using `setInterval`.
- Display appropriate loading (`LoadingChart`) or empty/error states (`EmptyChart`).
- Utilize a `ChartContainer` for consistent styling and Recharts components for rendering.

### 6. Services Layer (Frontend)

#### Analytics Service (`analyticsService.ts`)
A dedicated frontend service (`analyticsService.ts`) will encapsulate API calls to the backend `/api/analytics` endpoints.
- It will provide methods corresponding to each backend endpoint (e.g., `getDashboardKPIs`, `getSalesTrend`).
- It will handle request parameter construction and response data typing.
- May implement client-side caching strategies (see Performance Considerations).

### 7. Responsive Design Strategy

#### Mobile-First KPIs
The `DashboardKPIs` component will be designed to be mobile-friendly, always visible, and present key metrics concisely.

#### Chart Visibility Control
A `useMediaQuery` hook will be used to detect screen size. Full charts on the dashboard and orders page will typically be hidden on smaller screens (mobile) to maintain usability, showing only on tablet/desktop.

### 8. Error Handling & Loading States

#### Consistent Error Handling
- Frontend chart components will implement try-catch blocks for API calls.
- A shared `ChartErrorBoundary` React component can be used to catch rendering errors within charts and display a user-friendly message via `EmptyChart`.
- `LoadingChart` and `EmptyChart` components will provide consistent UI for these states.

### 9. Performance Considerations

#### Data Caching Strategy
- **Client-Side**: The frontend `analyticsService.ts` or individual chart components can implement a simple in-memory cache (e.g., using a `Map` with a Time-To-Live, TTL) for fetched analytics data to reduce redundant API calls, especially for frequently viewed charts with periodic refresh.
- **Backend**: The backend service itself does not currently implement caching, relying on efficient database queries.

### 10. Testing Strategy

#### Component Testing (Frontend)
- Unit/integration tests for individual chart components using a library like React Testing Library.
- Mock the `analyticsService` to provide controlled data for different scenarios (loading, data available, error).
- Verify correct rendering of loading states, chart data, and error messages.

## Implementation Phases

### Phase 9.1: Backend Foundation (Completed)
1.  **[COMPLETED]** Define and create all necessary DTOs for analytics data (`DashboardKPIsDto`, `SalesTrendDataDto`, `OrderStatusDistributionDto`, `TopMenuItemDto`, `OrdersByHourDto`, `PaymentMethodDistributionDto`, `AverageOrderValueTrendDto`, `OrderStatusTimelineEventDto`).
2.  **[COMPLETED]** Define `IAnalyticsService` interface with all required methods.
3.  **[COMPLETED]** Implement `AnalyticsController` with all API endpoints.
4.  **[COMPLETED]** Create skeleton `AnalyticsService` implementation and register for DI.

### Phase 9.2: Backend Logic Implementation (Completed)
1.  **[COMPLETED]** Implement data querying and business logic within all methods of `AnalyticsService.cs`.
    *   This includes logic for dashboard KPIs, sales trends, order status distribution, top menu items, orders by hour, payment method distribution, average order value trend, and a simplified order status timeline.
2.  **[COMPLETED]** Implement text-based report generation for daily summary and area performance reports. (Note: PDF/Excel generation is a future enhancement).

### Phase 9.3: Frontend Foundation (To Do)
1.  Install shadcn charts component (`npx shadcn-ui@latest add charts`).
2.  Create basic chart component file structure in `src/components/charts/`.
3.  Create frontend `analyticsService.ts` for API interactions.
4.  Add analytics DTO TypeScript definitions to `src/types/index.ts`.

### Phase 9.4: Frontend Dashboard Implementation (To Do)
1.  Implement `DashboardKPIs.tsx` component.
2.  Implement dashboard chart components: `SalesTrendChart.tsx`, `OrderStatusChart.tsx`, `TopMenuItemsChart.tsx`.
3.  Integrate KPIs and charts into the admin home page (`/admin/page.tsx`).
4.  Implement responsive behavior (KPIs always visible, charts on desktop/tablet).
5.  Implement error handling and loading states for dashboard components.

### Phase 9.5: Frontend Orders Analytics Implementation (To Do)
1.  Implement orders analytics chart components: `OrdersByHourChart.tsx`, `PaymentMethodsChart.tsx`, `AverageOrderValueChart.tsx`, `OrderStatusTimelineChart.tsx`.
2.  Integrate these charts into the orders admin page (`/admin/orders/page.tsx`).
3.  Ensure charts integrate with existing filter systems (area, day).
4.  Implement error handling and loading states for orders analytics components.

### Phase 9.6: Polish, Testing & Export (To Do)
1.  Frontend performance optimization (e.g., client-side caching if needed).
2.  Comprehensive testing of all charts and analytics features (frontend and backend interaction).
3.  Frontend UI for triggering report downloads (if backend report generation is enhanced beyond text).
4.  Documentation updates based on final implementation.
5.  User feedback integration.
>>>>>>> REPLACE

## Security Considerations

- All analytics endpoints require authentication
- Role-based access control (same permissions as existing admin pages)
- SuperAdmin organization context filtering
- Input validation for all parameters
- Rate limiting on analytics endpoints

## Monitoring & Maintenance

- Log analytics query performance
- Monitor cache hit rates
- Track chart rendering errors
- Regular review of query efficiency
- User feedback collection for chart usefulness

This architecture provides a solid foundation for implementing comprehensive analytics while maintaining consistency with the existing SagraFacile codebase and design patterns.
