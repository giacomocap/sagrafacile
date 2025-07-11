// Based on SagraFacile.NET.API/DTOs

// ==================
// SagraPreOrdine Sync
// ==================
export interface SyncConfigurationDto {
  id: number;
  organizationId: string;
  platformBaseUrl: string;
  apiKey: string;
  isEnabled: boolean;
}

export interface SyncConfigurationUpsertDto {
  platformBaseUrl: string;
  apiKey: string;
  isEnabled: boolean;
}

export interface MenuSyncResult {
  success: boolean;
  statusCode?: number;
  errorMessage?: string;
  errorDetails?: string;
}

// ==================
// Organization & Area
// ==================
export interface OrganizationDto {
  id: string;
  name: string;
  slug: string;
  subscriptionStatus?: string | null;
}

export interface AreaDto {
  id: number;
  name: string;
  organizationId: string;
  slug: string;
  isActive: boolean;
  // Workflow flags
  enableWaiterConfirmation: boolean;
  enableKds: boolean;
  enableCompletionConfirmation: boolean;
  // Printer-related fields
  receiptPrinterId?: number | null; // Added for printing
  printComandasAtCashier: boolean; // Added for printing
  // Queue System flag
  enableQueueSystem: boolean; // Added for Queue System
  guestCharge: number;
  takeawayCharge: number;
}
export interface AreaResponseDto {
  id: number;
  name: string;
  organizationId: string;
  isActive: boolean;
}

export interface AreaUpsertDto {
  name: string;
  organizationId: string;
  enableWaiterConfirmation: boolean;
  enableKds: boolean;
  enableCompletionConfirmation: boolean;
  receiptPrinterId?: number | null;
  printComandasAtCashier: boolean;
  enableQueueSystem: boolean;
  guestCharge: number;
  takeawayCharge: number;
}

// ==================
// KDS
// ==================
export interface KdsStationDto {
  id: number;
  name: string;
  areaId: number;
  organizationId: string;
  // assignedCategoryIds?: number[]; // Consider adding if needed directly on the station DTO
}

// NOTE: Aligned with backend OrderItem.cs KdsStatus enum
export enum KdsStatus {
  Pending = 0,
  Confirmed = 1,
}

export interface KdsOrderItemDto {
  orderItemId: number;
  menuItemName: string;
  quantity: number;
  note?: string | null;
  kdsStatus: KdsStatus;
}

export interface KdsOrderDto {
  orderId: string;
  displayOrderNumber?: string | null;
  orderDateTime: string; // ISO Date string
  tableNumber?: string | null;
  customerName?: string | null; // Added for KDS display
  items: KdsOrderItemDto[];
  dayId?: number | null; // Added for Operational Day feature
  numberOfGuests: number; // Added for Coperti
  isTakeaway: boolean; // Added for Asporto
  guestCharge: number;
  takeawayCharge: number;
}

// Result type for KDS Order Detail Dialog onClose callback
export interface KdsOrderDetailDialogCloseResult {
  completed: boolean;
  updatedStatuses?: { [key: number]: KdsStatus };
}

// ==================
// Queue System
// ==================

// DTO for API requests needing cashier station context (e.g., CallNext)
export interface CallNextQueueRequestDto {
  cashierStationId?: number | null;
}

// DTO for API requests to call a specific number
export interface CallSpecificQueueRequestDto {
  ticketNumber: number; // Changed from numberToCall to match backend
  cashierStationId?: number | null;
}

// DTO for API requests to update the next sequential number manually
export interface UpdateNextSequentialNumberRequestDto {
  nextNumber: number;
}

// DTO representing the state of the queue for an area
export interface QueueStateDto {
  areaId: number;
  isQueueSystemEnabled: boolean;
  nextSequentialNumber: number;
  lastCalledNumber?: number | null;
  lastCalledCashierStationId?: number | null;
  lastCalledCashierStationName?: string | null;
  lastCallTimestamp?: string | null;
  lastResetTimestamp?: string | null;
}

// DTO returned by API after successfully calling a number
export interface CalledNumberDto {
  ticketNumber: number;
  cashierStationId?: number | null;
  cashierStationName?: string | null;
  message?: string;
}

// DTO for SignalR broadcast when a number is called
export interface CalledNumberBroadcastDto {
  areaId: number;
  ticketNumber: number;
  cashierStationId?: number | null;
  cashierStationName?: string | null;
  timestamp: string;
}

// DTO for SignalR broadcast when queue state is updated (optional, might just send QueueStateDto)
export interface QueueStateUpdateBroadcastDto {
  areaId: number;
  newState: QueueStateDto;
}

// DTO for SignalR broadcast when queue is reset
export interface QueueResetBroadcastDto {
  areaId: number;
  resetTimestamp: string; // ISO Date string
}

// DTO for API requests to respeak the last called number
export interface RespeakQueueRequestDto {
  cashierStationId: number;
}

// ==================
// Day (Operational Day / Giornata)
// ==================
// NOTE: Aligned with backend Day.cs DayStatus enum
export enum DayStatus {
  Open = 0,
  Closed = 1,
}

export interface DayDto {
  id: number;
  organizationId: string;
  startTime: string; // ISO Date string
  endTime?: string | null; // ISO Date string
  status: DayStatus;
  openedByUserId: string;
  closedByUserId?: string | null;
  totalSales?: number | null;
}


// ==================
// Menu
// ==================
export interface MenuCategoryDto {
  id: number;
  name: string;
  areaId: number;
}

export interface MenuItemDto {
  id: number;
  name: string;
  description?: string | null;
  price: number;
  menuCategoryId: number;
  // menuCategoryName?: string; // Consider adding to backend DTO if useful
  isNoteRequired: boolean;
  noteSuggestion?: string | null;
  scorta?: number | null; // Added for Stock Management
  // We might need AreaId here too for easier client-side grouping if not fetching per area
  // areaId?: number;
}

// ==================
// Stock Management (Scorta)
// ==================
export interface StockUpdateBroadcastDto {
  menuItemId: number;
  areaId: number;
  newScorta: number | null;
  timestamp: string; // ISO Date string
}

// ==================
// Order (Creation)
// ==================
export interface CreateOrderItemDto {
  menuItemId: number;
  quantity: number;
  note?: string | null;
}

export interface CreateOrderDto {
  areaId: number;
  customerName: string; // Added for Cashier UI
  items: CreateOrderItemDto[];
  paymentMethod?: string | null; // e.g., "Contanti", "POS"
  amountPaid?: number | null; // Optional for cash payments
  numberOfGuests: number; // Added for Coperti
  isTakeaway: boolean; // Added for Asporto
  cashierStationId?: number | null; // Added for Cashier Station selection
  tableNumber?: string; // Added for Mobile Table Ordering
  // cashierId?: string; // Added for Mobile Table Ordering (user taking order)
}

// Specifically for the public pre-order endpoint
export interface PreOrderDto {
  organizationId: string; // Required by backend for validation/context
  areaId: number;
  customerName: string;
  customerEmail: string;
  items: CreateOrderItemDto[];
  numberOfGuests: number; // Added for Coperti
  isTakeaway: boolean; // Added for Asporto
}


// ==================
// Order (Update/Confirmation)
// ==================
export interface ConfirmPreOrderPaymentDto {
  customerName?: string | null; // Optional: To update the name if needed
  paymentMethod: 'Contanti' | 'POS';
  amountPaid?: number | null; // Required for 'Contanti' if change is needed, otherwise optional
  items: CreateOrderItemDto[]; // The potentially modified list of items
  numberOfGuests: number; // Added for Coperti
  isTakeaway: boolean; // Added for Asporto
  cashierStationId?: number | null; // Added for Cashier Station selection
}


// ==================
// Order (Display/Response) - Based on OrderDto
// ==================
// Order (Display/Response) - Based on OrderDto
// ==================
// NOTE: Aligned with backend Order.cs OrderStatus enum as of 2025-04-16
export enum OrderStatus {
  PreOrder = 0,       // Order placed via public interface, not yet confirmed/paid
  Pending = 1,        // Order created by cashier, not yet paid/processed
  Paid = 2,           // Order paid
  Preparing = 3,      // Order confirmed by waiter, sent to kitchen/bar for preparation
  ReadyForPickup = 4, // Order preparation completed by KDS, ready for pickup
  Completed = 5,      // Order picked up/served
  Cancelled = 6       // Order cancelled
}

export interface OrderItemDto {
  menuItemId: number;
  menuItemName: string;
  quantity: number;
  unitPrice: number;
  note?: string | null;
}

export interface OrderDto {
  id: string; // Changed from number to string
  displayOrderNumber?: string | null;
  // orderNumber: string; // Removed
  areaId: number;
  areaName: string;
  cashierId?: string | null; // Made nullable
  cashierName?: string | null; // Made nullable
  waiterId?: string | null; // Added WaiterId (nullable)
  waiterName?: string | null; // Added WaiterName (nullable)
  orderDateTime: string; // ISO Date string
  status: OrderStatus;
  totalAmount: number;
  paymentMethod?: string | null;
  amountPaid?: number | null;
  customerName?: string | null; // Added for PreOrder
  customerEmail?: string | null; // Added for PreOrder
  tableNumber?: string | null; // Added for Waiter Interface
  qrCodeBase64?: string | null; // Added for QR Code image data
  items: OrderItemDto[];
  dayId?: number | null; // Added for Operational Day feature
  numberOfGuests: number; // Added for Coperti
  isTakeaway: boolean; // Added for Asporto
  guestCharge: number;
  takeawayCharge: number;
}

// DTO for SignalR broadcast when order status changes (for public pickup display)
export interface OrderStatusBroadcastDto {
  orderId: string;
  displayOrderNumber?: string | null;
  newStatus: OrderStatus;
  organizationId: string;
  areaId: number;
  customerName?: string | null;
  tableNumber?: string | null;
  statusChangeTime: string; // ISO date string
}


// ==================
// User & Auth
// ==================
export interface UserDto {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  emailConfirmed: boolean;
  roles: string[];
  organizationId: string;
  // organizationName?: string;
}

export interface TokenResponseDto {
  accessToken: string;
  accessTokenExpiryTime: string; // ISO Date string
  refreshToken: string;
  refreshTokenExpiryTime: string; // ISO Date string
  userId: string;
  email: string;
}

export interface RefreshTokenRequestDto {
  refreshToken: string;
}

// ==================
// Printer
// ==================
export enum PrinterType {
  Network = 0,
  WindowsUsb = 1
}

export enum DocumentType {
  EscPos = 0,
  HtmlPdf = 1,
}

export enum PrintMode {
  Immediate = 0,
  OnDemandWindows = 1,
}

export enum ReprintType {
  ReceiptOnly = 0,
  ReceiptAndComandas = 1
}

export interface PrinterDto {
  id: number;
  organizationId: string;
  name: string;
  type: PrinterType; // Enum: Network, WindowsUsb
  connectionString: string; // IP:Port for Network, GUID for WindowsUsb
  isEnabled: boolean;
  printMode: PrintMode; // Added for On-Demand Printing
  documentType: DocumentType;
  paperSize: string | null;
}

export interface PrinterUpsertDto {
  name: string;
  type: PrinterType;
  connectionString: string;
  isEnabled: boolean;
  organizationId: string;
  printMode: PrintMode; // Added for On-Demand Printing
  documentType: DocumentType;
  paperSize: string | null;
}

export enum TemplateType {
  Receipt = 0,
  Comanda = 1,
}

export interface PrintTemplateDto {
  id: number;
  name: string;
  templateType: TemplateType;
  documentType: DocumentType; // Reuse existing enum
  htmlContent: string | null;
  escPosHeader: string | null;
  escPosFooter: string | null;
  isDefault: boolean;
}

export type PrintTemplateUpsertDto = Omit<PrintTemplateDto, 'id'>;

export interface PrinterCategoryAssignmentDto {
  printerId: number;
  menuCategoryId: number;
  menuCategoryName: string;
  menuCategoryAreaId: number;
}

export interface PreviewRequestDto {
  htmlContent: string;
  templateType: TemplateType;
}

// ==================
// Print Job
// ==================
export enum PrintJobStatus {
  Pending = 0,
  Processing = 1,
  Succeeded = 2,
  Failed = 3,
}

export enum PrintJobType {
  Receipt = 0,
  Comanda = 1,
  TestPrint = 2,
}

export interface PrintJobDto {
  id: string; // Guid is a string
  jobType: PrintJobType;
  status: PrintJobStatus;
  createdAt: string; // ISO Date string
  lastAttemptAt?: string | null; // ISO Date string
  completedAt?: string | null; // ISO Date string
  retryCount: number;
  errorMessage?: string | null;
  orderId?: string | null;
  orderDisplayNumber?: string | null;
  printerId: number;
  printerName: string;
}

export interface PrintJobQueryParameters {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortAscending?: boolean;
  // Add filters here later if needed
}

export interface OrderQueryParameters {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortAscending?: boolean;
  areaId?: number;
  dayId?: number | 'current';
  organizationId?: string;
  statuses?: number[];
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}


// ==================
// Cashier Station
// ==================
export interface CashierStationDto {
  id: number;
  organizationId: string;
  areaId: number;
  name: string;
  receiptPrinterId: number | null;
  printComandasAtThisStation: boolean;
  isEnabled: boolean;
  areaName?: string;      // For display, populated by backend DTO
  receiptPrinterName?: string | null; // For display, populated by backend DTO
}

export interface CashierStationUpsertDto {
  organizationId: string; // Not sent by frontend on create, but good for consistency if used elsewhere
  areaId: number;
  name: string;
  receiptPrinterId: number | null;
  printComandasAtThisStation: boolean;
  isEnabled: boolean;
}

// ==================
// General / Utility
// ==================

// Add other DTOs as needed (e.g., LoginDto, RegisterDto, UpdateUserDto)

// ==================
// Client-Side Specific Types (e.g. for UI state)
// ==================
export interface CartItem extends CreateOrderItemDto {
  cartItemId: string; // Client-side unique ID for the item instance in the cart
  name: string;
  unitPrice: number;
  totalPrice: number;
  isNoteRequired: boolean;
  noteSuggestion?: string | null;
  isOutOfStock?: boolean;
  // categoryName is not part of the core CartItem,
  // it's usually added dynamically or available via menuItemId lookup.
  // If consistently needed with cart item, consider adding to backend OrderItemDto enrichment if appropriate
  // or always ensure it's merged client-side when constructing displayable cart items.
}

// Client-side CartItem that includes categoryName, typically added after fetching menu items
export interface AppCartItem extends CartItem {
  categoryName: string;
}

// ==================
// Ad Carousel
// ==================
export interface AdMediaItemDto {
  id: string;
  organizationId: string;
  name: string;
  mediaType: 'Image' | 'Video';
  filePath: string;
  mimeType: string;
  uploadedAt: string;
}

export interface AdAreaAssignmentDto {
  id: string;
  adMediaItemId: string;
  areaId: number;
  displayOrder: number;
  durationSeconds: number | null;
  isActive: boolean;
  adMediaItem: AdMediaItemDto;
}

export interface AdMediaItemUpsertDto {
  displayOrder: number;
  durationSeconds?: number | null;
  isActive: boolean;
  file?: File;
}

// ==================
// Analytics & Charts
// ==================
export interface DashboardKPIsDto {
  todayTotalSales: number;
  todayOrderCount: number;
  averageOrderValue: number;
  mostPopularCategory: string | null;
  totalCoperti: number; // Total number of guests served (coperti)
  dayId: number | null;
  dayDate: string | null; // ISO Date string
}

export interface SalesTrendDataDto {
  date: string; // ISO Date string
  sales: number;
  orderCount: number;
  dayId: number | null;
}

export interface OrderStatusDistributionDto {
  status: string;
  count: number;
  percentage: number;
}

export interface TopMenuItemDto {
  itemName: string;
  categoryName: string;
  quantity: number;
  revenue: number;
}

export interface OrdersByHourDto {
  hour: number;
  orderCount: number;
  revenue: number;
}

export interface PaymentMethodDistributionDto {
  paymentMethod: string;
  count: number;
  amount: number;
  percentage: number;
}

export interface AverageOrderValueTrendDto {
  date: string; // ISO Date string
  averageValue: number;
  orderCount: number;
  dayId: number | null;
}

export interface OrderStatusTimelineEventDto {
  orderId: string;
  displayOrderNumber: string | null;
  status: string;
  timestamp: string; // ISO Date string
  previousStatus: string | null;
  durationInPreviousStatusMinutes: number | null;
}

// ==================
// User Invitations
// ==================
export interface UserInvitationRequestDto {
  email: string;
  roles: string[];
}

export interface AcceptInvitationDto {
  token: string;
  firstName: string;
  lastName: string;
  password: string;
  confirmPassword: string;
}

export interface InvitationDetailsDto {
  email: string;
  organizationName: string;
}

export interface PendingInvitationDto {
  id: string;
  email: string;
  roles: string;
  expiryDate: string;
  invitedAt: string;
}
