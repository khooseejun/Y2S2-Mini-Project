using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace RestaurantSystem.Models
{
    public class ViewModels
    {
        // Login ViewModel
        public class LoginVM
        {
            [Required(ErrorMessage = "Please enter username")]
            public required string Username { get; set; }

            [Required(ErrorMessage = "Please enter password")]
            [DataType(DataType.Password)]
            public required string Password { get; set; }

            public bool RememberMe { get; set; }
        }

        // Register ViewModel
        public class RegisterVM
        {
            [Required(ErrorMessage = "Please enter username")]
            [StringLength(50)]
            public required string Username { get; set; }

            [Required(ErrorMessage = "Please enter email address")]
            [EmailAddress]
            [StringLength(100)]
            public required string Email { get; set; }

            [Required(ErrorMessage = "Please enter password")]
            [StringLength(100, MinimumLength = 6)]
            [DataType(DataType.Password)]
            public required string Password { get; set; }

            [Required(ErrorMessage = "Please enter full name")]
            [StringLength(100)]
            public required string FullName { get; set; }

            [Phone]
            [StringLength(20)]
            public string? PhoneNumber { get; set; }
        }

        // Reset Password ViewModel — used by logged-in members on the Profile page.
        public class ResetPasswordVM
        {
            [Required(ErrorMessage = "Please enter your current password")]
            [DataType(DataType.Password)]
            public required string OldPassword { get; set; }

            [Required(ErrorMessage = "Please enter a new password")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
            [DataType(DataType.Password)]
            public required string NewPassword { get; set; }

            [Required(ErrorMessage = "Please confirm your new password")]
            [DataType(DataType.Password)]
            [Compare("NewPassword", ErrorMessage = "The confirmation password does not match.")]
            public required string ConfirmNewPassword { get; set; }
        }

        // Step 1 of Password Recovery (Email)
        public class PasswordRecoveryVM
        {
            [Required(ErrorMessage = "Please enter your email address")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address")]
            public required string Email { get; set; }
        }

        // Step 2 of Password Recovery (Email)
        public class PasswordRecoveryResetVM
        {
            [Required]
            public required string Token { get; set; }

            [Required]
            public required string Email { get; set; }

            [Required(ErrorMessage = "Please enter a new password")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
            [DataType(DataType.Password)]
            public required string NewPassword { get; set; }

            [Required(ErrorMessage = "Please confirm your new password")]
            [DataType(DataType.Password)]
            [Compare("NewPassword", ErrorMessage = "The confirmation password does not match.")]
            public required string ConfirmNewPassword { get; set; }
        }

        // Edit Profile ViewModel
        public class EditProfileVM
        {
            [Required(ErrorMessage = "Please enter full name")]
            [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
            public required string FullName { get; set; }

            [Required(ErrorMessage = "Please enter email address")]
            [EmailAddress(ErrorMessage = "Invalid email address format")]
            [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
            public required string Email { get; set; }

            [Phone(ErrorMessage = "Invalid phone number format")]
            [StringLength(20)]
            public string? PhoneNumber { get; set; }

            [StringLength(250, ErrorMessage = "Address cannot exceed 250 characters")]
            public string? Address { get; set; }
        }


        // Represents a single item in the shopping cart
        public class CartItemVM
        {
            public int ProductId { get; set; }
            public required string ProductName { get; set; }
            public decimal Price { get; set; }
            public int Quantity { get; set; }

            // Customisations chosen for this line (Workflow.md §2.3)
            public List<CartAddonVM> Addons { get; set; } = new();

            [StringLength(200, ErrorMessage = "Special instructions cannot exceed 200 characters")]
            public string? SpecialInstructions { get; set; }

            /// <summary>Sum of the selected add-on prices (per single unit).</summary>
            public decimal AddonTotal => Addons.Sum(a => a.Price);

            /// <summary>Unit price including add-ons.</summary>
            public decimal UnitPrice => Price + AddonTotal;

            public decimal Total => UnitPrice * Quantity;

            /// <summary>Human-readable line for receipts, e.g. "Extra Cheese, No Onions".</summary>
            public string AddonSummary => string.Join(", ", Addons.Select(a => a.Name));
        }

        // A chosen add-on inside a cart line
        public class CartAddonVM
        {
            public int AddonId { get; set; }
            public required string Name { get; set; }
            public decimal Price { get; set; }
        }

        // Represents one row in the Top Selling Products ranking (Home page hero carousel)
        public class TopSellingItemVM
        {
            public int ProductId { get; set; }
            public required string ProductName { get; set; }
            public required string ImageUrl { get; set; }
            public int CategoryId { get; set; }
            public required string CategoryName { get; set; }
            public int TotalSold { get; set; }
        }

        // ==========================================
        // Order / Checkout ViewModels
        // ==========================================

        // Step 1: Dine-in vs Takeaway vs Delivery (+ table selection / address)
        public class CheckoutVM
        {
            public List<CartItemVM> CartItems { get; set; } = new();

            [Required(ErrorMessage = "Please choose Dine-in, Takeaway or Delivery")]
            public required string DiningOption { get; set; } // "DineIn" | "Takeaway" | "Delivery"

            public int? TableId { get; set; }

            public List<Table> AvailableTables { get; set; } = new();

            // Only required when DiningOption == "Delivery" — validated manually
            // in the controller since it depends on which option was picked.
            [StringLength(250, ErrorMessage = "Address cannot exceed 250 characters")]
            public string? Address { get; set; }

            /// <summary>Live price breakdown, filled by the controller.</summary>
            public OrderTotals Totals { get; set; }

            public decimal Subtotal => CartItems.Sum(i => i.Total);
            public decimal GrandTotal => Totals.Total;
        }

        // Step 2: Payment method selection
        public class PaymentVM
        {
            public List<CartItemVM> CartItems { get; set; } = new();

            // Nullable on purpose: the controller reads the real value from Session,
            // not from this form field, so it must NOT be implicitly treated as required
            // (a non-nullable string here would silently block every checkout with
            // "The DiningOption field is required.")
            public string? DiningOption { get; set; }
            public string? TableName { get; set; }

            [Required(ErrorMessage = "Please select a payment method")]
            public required string PaymentMethod { get; set; }

            /// <summary>Live price breakdown, filled by the controller.</summary>
            public OrderTotals Totals { get; set; }

            public decimal Subtotal => CartItems.Sum(i => i.Total);
            public decimal GrandTotal => Totals.Total;
        }

        // ==========================================
        // Reservation ViewModels (Workflow.md §2.5 / §3.6)
        // ==========================================

        // Customer-facing reservation form
        public class ReservationVM
        {
            public int ReservationId { get; set; }

            [Required(ErrorMessage = "Please enter the customer name")]
            [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
            public required string CustomerName { get; set; }

            [Required(ErrorMessage = "Please enter a phone number")]
            [Phone(ErrorMessage = "Invalid phone number format")]
            [StringLength(20)]
            public required string Phone { get; set; }

            [EmailAddress(ErrorMessage = "Invalid email address format")]
            [StringLength(100)]
            public string? Email { get; set; }

            [Required(ErrorMessage = "Please choose a date")]
            [DataType(DataType.Date)]
            public DateTime ReservationDate { get; set; } = DateTime.Today;

            // Bound as a string ("18:30") so <input type="time"> posts cleanly.
            [Required(ErrorMessage = "Please choose a time")]
            public string ReservationTime { get; set; } = "19:00";

            [Range(1, 50, ErrorMessage = "Guests must be between 1 and 50")]
            public int Guests { get; set; } = 2;

            public int? TableId { get; set; }

            [StringLength(250)]
            public string? SpecialRequests { get; set; }

            public string? Status { get; set; }

            public List<Table> AvailableTables { get; set; } = new();

            /// <summary>Parses <see cref="ReservationTime"/> into a TimeSpan, or null if invalid.</summary>
            public TimeSpan? ParsedTime =>
                TimeSpan.TryParse(ReservationTime, out var t) ? t : null;
        }

        // One row in the admin reservations table
        public class ReservationRowVM
        {
            public int ReservationId { get; set; }
            public required string CustomerName { get; set; }
            public required string Phone { get; set; }
            public string? Email { get; set; }
            public DateTime ReservationDate { get; set; }
            public TimeSpan ReservationTime { get; set; }
            public int Guests { get; set; }
            public int? TableId { get; set; }
            public string? TableName { get; set; }
            public required string Status { get; set; }
            public string StatusLabel => Models.ReservationStatus.Label(Status);
            public string StatusBadgeClass => Models.ReservationStatus.BadgeClass(Status);
            public string? SpecialRequests { get; set; }
            public DateTime CreatedDate { get; set; }

            public DateTime StartsAt => ReservationDate.Date + ReservationTime;
        }

        // ==========================================
        // Payment / Table admin ViewModels
        // ==========================================

        public class PaymentRowVM
        {
            public int PaymentId { get; set; }
            public int OrderId { get; set; }
            public string? Method { get; set; }
            public decimal Amount { get; set; }
            public required string Status { get; set; }
            public string? Reference { get; set; }
            public DateTime CreatedDate { get; set; }
            public DateTime? PaidDate { get; set; }
            public string? CustomerName { get; set; }
        }

        public class TableRowVM
        {
            public int TableId { get; set; }
            public required string TableName { get; set; }
            public int Capacity { get; set; }
            public string? Location { get; set; }
            public required string Status { get; set; }
            public bool IsOccupied { get; set; }
            public string StatusBadgeClass => Models.TableState.BadgeClass(Status);
        }

        // A single line item on a receipt / order summary
        public class OrderItemLineVM
        {
            public required string ProductName { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }

            public List<string> Addons { get; set; } = new();
            public string? SpecialInstructions { get; set; }

            public decimal SubTotal => UnitPrice * Quantity;
        }

        // Full summary of a placed order — used by Confirmation, Details, History, and the PDF receipt
        public class OrderSummaryVM
        {
            public int OrderId { get; set; }
            public DateTime OrderDate { get; set; }
            public required string Status { get; set; }
            public string StatusLabel => Models.OrderStatus.Label(Status);
            public string StatusBadgeClass => Models.OrderStatus.BadgeClass(Status);
            public decimal TotalAmount { get; set; }
            public required string DiningOption { get; set; }
            public string OrderTypeLabel => Models.OrderType.Label(DiningOption);
            public string? TableName { get; set; }
            public required string PaymentMethod { get; set; }
            public string? DeliveryAddress { get; set; }
            public List<OrderItemLineVM> Items { get; set; } = new();

            // Price breakdown (Workflow.md §2.4)
            public decimal SubtotalAmount { get; set; }
            public decimal ServiceCharge { get; set; }
            public decimal TaxAmount { get; set; }
            public decimal DeliveryFee { get; set; }
            public decimal DiscountAmount { get; set; }

            // Order lifecycle
            public DateTime? StatusUpdatedDate { get; set; }
            public string? CancellationReason { get; set; }

            /// <summary>Tracking timeline (Workflow.md §2.6) — the steps the order still has to go through.</summary>
            public List<OrderStatusStepVM> Timeline { get; set; } = new();

            /// <summary>Statuses the CURRENT user may move this order to (used by admin screens).</summary>
            public List<string> AllowedNextStatuses { get; set; } = new();

            // True only while the table this order used is still marked occupied —
            // drives whether "Check Out Table" shows up on My Orders / the receipt
            public bool TableStillOccupied { get; set; }

            // Only a still-pending order can be cancelled by the customer
            // (matches the rule OrderController.Cancel enforces server-side)
            public bool CanCancel => Status == Models.OrderStatus.Pending;
        }

        // One step in the customer-facing order tracking timeline
        public class OrderStatusStepVM
        {
            public required string Status { get; set; }
            public string Label => Models.OrderStatus.Label(Status);
            public string Description => Models.OrderStatus.Description(Status);
            public bool IsDone { get; set; }
            public bool IsCurrent { get; set; }
        }

        // ==========================================
        // Admin ViewModels
        // ==========================================

        // Used by Admin's UserCreate / UserEdit pages
        public class UserVM
        {
            public int UserId { get; set; }

            [Required(ErrorMessage = "Please enter username")]
            [StringLength(50)]
            public required string Username { get; set; }

            [Required(ErrorMessage = "Please enter email address")]
            [EmailAddress]
            [StringLength(100)]
            public required string Email { get; set; }

            [Required(ErrorMessage = "Please enter full name")]
            [StringLength(100)]
            public required string FullName { get; set; }

            [Phone]
            [StringLength(20)]
            public string? PhoneNumber { get; set; }

            [StringLength(250, ErrorMessage = "Address cannot exceed 250 characters")]
            public string? Address { get; set; }

            [Required]
            [StringLength(20)]
            public string Role { get; set; } = "MEMBER";

            // Required when creating a new user; leave blank on edit to keep the current password
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
            [DataType(DataType.Password)]
            public string? Password { get; set; }

            public bool IsBlocked { get; set; }
        }

        // One row in Admin's order management table (Admin/Orders)
        public class OrderVM
        {
            public int OrderId { get; set; }
            public required string CustomerName { get; set; }
            public string? TableName { get; set; }
            public DateTime OrderDate { get; set; }
            public decimal TotalAmount { get; set; }
            public required string OrderStatus { get; set; }

            public string? DiningOption { get; set; }
            public string OrderTypeLabel => Models.OrderType.Label(DiningOption);
            public string StatusLabel => Models.OrderStatus.Label(OrderStatus);
            public string StatusBadgeClass => Models.OrderStatus.BadgeClass(OrderStatus);

            public string? PaymentMethod { get; set; }
            public string? DeliveryAddress { get; set; }
            public string? CancellationReason { get; set; }
            public DateTime? StatusUpdatedDate { get; set; }

            public List<OrderDetailVM> Details { get; set; } = new();

            /// <summary>Statuses this order may legally move to (drives the action buttons).</summary>
            public IReadOnlyList<string> AllowedNextStatuses => Models.OrderStatus.NextStatuses(OrderStatus);
        }

        // A single line item within OrderVM
        public class OrderDetailVM
        {
            public required string ProductName { get; set; }
            public int Quantity { get; set; }
            public decimal PriceAtOrder { get; set; }

            public string? SpecialInstructions { get; set; }
            public List<string> Addons { get; set; } = new();

            public decimal Subtotal => Quantity * PriceAtOrder;
        }

        // ==========================================
        // Report ViewModels
        // ==========================================

        public class ReportItemVM
        {
            public int ProductId { get; set; }
            public required string ProductName { get; set; }
            public int Quantity { get; set; }
            public decimal Revenue { get; set; }
        }

        public class DailyReportVM
        {
            public DateTime Date { get; set; }
            public int TotalOrders { get; set; }
            public decimal TotalRevenue { get; set; }
            public decimal AvgOrderValue { get; set; }
            public List<ReportItemVM> Items { get; set; } = new();
        }

        public class DaySummaryVM
        {
            public DateTime Day { get; set; }
            public int Orders { get; set; }
            public decimal Revenue { get; set; }
        }

        public class MonthlyReportVM
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public required string MonthName { get; set; }
            public int TotalOrders { get; set; }
            public decimal TotalRevenue { get; set; }
            public List<DaySummaryVM> DailySummaries { get; set; } = new();
            public List<ReportItemVM> TopItems { get; set; } = new();
        }

        public class TopSellingVM
        {
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            public int TotalOrders { get; set; }
            public decimal TotalRevenue { get; set; }
            public List<ReportItemVM> Items { get; set; } = new();
        }
    }
}
