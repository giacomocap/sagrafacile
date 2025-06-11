# **Architecture: Queue Display Advertising Carousel**

## 1. Overview

### 1.1. Goal

To enhance the public-facing **Queue Display Page** by incorporating a rotating carousel of advertisements (images and videos) in the lower portion of the screen. This utilizes the screen real estate to display promotional content, sponsor messages, or event information.

### 1.2. Core Requirements

*   The system must support both image and video file formats.
*   Media items are managed at the **Organization** level.
*   Media items can be **assigned** to one or more **Areas** with specific display settings (order, duration).
*   The solution must work in a **self-hosted, local network environment** with potentially no internet access.
*   **Phase 1 (Complete):** A functional carousel with hardcoded media content for rapid deployment.
*   **Phase 2 (Complete):** A full-featured management interface in the Admin panel for event organizers to upload, order, and manage ad content without code changes.

## 2. Frontend Architecture

### 2.1. `QueueDisplayPage` Layout Modification

The page at `src/app/(public)/qdisplay/org/[orgSlug]/area/[areaId]/page.tsx` is restructured to use a vertical flexbox layout that fills the screen height.

*   **Top Section (~65% height):** This container holds the existing queue number display grid. It is configured to grow and fill the available space.
*   **Bottom Section (~35% height):** This container has a fixed height and houses the new `AdCarousel` component.

### 2.2. New Component: `AdCarousel.tsx`

*   **Location:** `sagrafacile-webapp/src/components/public/AdCarousel.tsx`
*   **Purpose:** A self-contained, reusable component to display a list of media items.
*   **Props:** It accepts an array of media items, e.g., `mediaItems: AdMedia[]`.
*   **Logic:**
    *   It manages its own state, including the `currentIndex` of the media being displayed.
    *   It uses `useEffect` to manage the rotation logic:
        *   For **images**, it uses `setTimeout` to advance to the next item after a specified duration.
        *   For **videos**, it uses the `onEnded` event listener to advance to the next item automatically. Videos are configured with `autoPlay`, `muted`, and `loop={false}`.
    *   It features a simple fade-in/fade-out transition between media items for a smooth visual experience.

### 2.3. Admin UI: `AdManagementPage.tsx`

*   **Location:** `sagrafacile-webapp/src/app/app/org/[orgId]/admin/ads/page.tsx`
*   **Purpose:** A unified page for managing the organization's media library and assigning media to specific areas.
*   **Functionality:**
    1.  **Media Library:** Displays all media items uploaded for the organization. Allows for adding, editing (name), and deleting media.
    2.  **Area Selector:** An `AdminAreaSelector` component allows the user to choose an area.
    3.  **Assignments View:** Once an area is selected, it displays a table of media items assigned to that area, showing their display order, duration, and status. It allows for adding, editing, and deleting these assignments.

## 3. Backend & Data Architecture (Phase 2 - Refactored)

### 3.1. Media File Storage Strategy

This directly addresses the self-hosted environment requirement.

*   **Local Storage:** All ad media (images, videos) are stored directly on the server running the SagraFacile .NET backend.
*   **File Path:** A dedicated directory within the backend's static file serving root is used. The structure is `SagraFacile.NET/SagraFacile.NET.API/wwwroot/media/promo/[organizationId]/`. This isolates media by organization. The path was changed from `/ads` to `/promo` to avoid issues with ad-blockers.
*   **Serving Files:** The .NET application is configured to serve static files from this `wwwroot/media` directory.
*   **Docker Integration:** The `wwwroot/media` directory inside the container is mapped to a persistent Docker Volume on the host machine.

### 3.2. Database Model

The data model was refactored to decouple media items from areas.

#### `AdMediaItems` Table
Stores the central library of media for an organization.

| Column Name       | Data Type      | Notes                                 |
| ----------------- | -------------- | ------------------------------------- |
| `Id`              | `Guid`         | Primary Key                           |
| `OrganizationId`  | `int`          | Foreign Key to `Organizations` table. |
| `Name`            | `string(100)`  | A user-friendly name for the media.   |
| `MediaType`       | `int` (Enum)   | `0` for Image, `1` for Video.         |
| `FilePath`        | `string`       | Relative path to the file.            |
| `MimeType`        | `string`       | e.g., `image/jpeg`, `video/mp4`.      |
| `UploadedAt`      | `DateTime`     | Timestamp of when the ad was uploaded.|

#### `AdAreaAssignments` Table
A join table that links a media item to an area and defines its display properties for that specific area.

| Column Name       | Data Type      | Notes                                     |
| ----------------- | -------------- | ----------------------------------------- |
| `Id`              | `Guid`         | Primary Key                               |
| `AdMediaItemId`   | `Guid`         | Foreign Key to `AdMediaItems` table.      |
| `AreaId`          | `int`          | Foreign Key to `Areas` table.             |
| `DisplayOrder`    | `int`          | To control the sequence of ads in an area.|
| `DurationSeconds` | `int`          | **For images only.** Duration to display. |
| `IsActive`        | `bool`         | To enable/disable an ad for this area.    |

### 3.3. API Endpoints

The RESTful endpoints were refactored to support the new architecture.

*   **Public Endpoint (for the display):**
    *   `GET /api/public/areas/{areaId}/ads`
        *   Returns a sorted, active list of `AdAreaAssignment` DTOs for the specified area, which includes the nested `AdMediaItem` details.

*   **Admin Endpoints (for management):**
    *   **Media Library:**
        *   `GET /api/admin/organizations/{organizationId}/ads`: Gets all media for an org.
        *   `POST /api/admin/organizations/{organizationId}/ads`: Uploads a new media file and creates an `AdMediaItem`.
        *   `PUT /api/admin/ads/{adId}`: Updates an `AdMediaItem`'s name.
        *   `DELETE /api/admin/ads/{adId}`: Deletes an `AdMediaItem` and its file.
    *   **Area Assignments:**
        *   `GET /api/admin/areas/{areaId}/ad-assignments`: Gets all assignments for an area.
        *   `POST /api/admin/ad-assignments`: Creates a new assignment.
        *   `PUT /api/admin/ad-assignments/{assignmentId}`: Updates an assignment's properties.
        *   `DELETE /api/admin/ad-assignments/{assignmentId}`: Deletes an assignment.
