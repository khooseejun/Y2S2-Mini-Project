using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace RestaurantSystem.Models
{
    // ==========================================
    // 1. DbContext
    // ==========================================
    public class RestaurantDbContext : DbContext
    {
        public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductAddon> ProductAddons { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<OrderItemAddon> OrderItemAddons { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Favorite> Favorites { get; set; }

        // NOTE: Data Annotations can't express "block deleting a Category that still
        // has Products" on their own, so Fluent API is used here as a deliberate,
        // narrow exception (matches AdminController.DeleteCategory's own check).
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add-ons belong to a dish; deleting the dish removes its add-on definitions.
            modelBuilder.Entity<ProductAddon>()
                .HasOne<Product>()
                .WithMany(p => p.Addons)
                .HasForeignKey(a => a.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Add-on snapshots belong to an order line.
            modelBuilder.Entity<OrderItemAddon>()
                .HasOne<OrderDetail>()
                .WithMany(d => d.Addons)
                .HasForeignKey(a => a.OrderDetailId)
                .OnDelete(DeleteBehavior.Cascade);

            // Orders must never be deleted just because a customer account goes away:
            // AdminController.DeleteUser already refuses to delete users who have orders.
            modelBuilder.Entity<Order>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Reservations outlive table changes; keep them if the table is removed.
            modelBuilder.Entity<Reservation>()
                .HasOne<Table>()
                .WithMany()
                .HasForeignKey(r => r.TableId)
                .OnDelete(DeleteBehavior.SetNull);

            // A booking for the same table must be quick to look up when checking conflicts.
            modelBuilder.Entity<Reservation>()
                .HasIndex(r => new { r.TableId, r.ReservationDate });

            // One payment record per order.
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.OrderId);
        }
    }

    // ==========================================
    // 2. Entity Models with Full Validation
    // ==========================================

    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Please enter username")]
        [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Please enter email address")]
        [EmailAddress(ErrorMessage = "Invalid email address format")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Please enter password")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
        [DataType(DataType.Password)]
        public required string Password { get; set; }

        [Required(ErrorMessage = "Please enter full name")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
        public required string FullName { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format")]
        [StringLength(20)]
        public required string PhoneNumber { get; set; }

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "MEMBER"; // "ADMIN" or "MEMBER"

        // Password Recovery (Email) support — nullable, only populated while a reset request is pending
        [StringLength(200)]
        public string? PasswordResetToken { get; set; }

        public DateTime? PasswordResetTokenExpiry { get; set; }

        // Profile photo — nullable, falls back to a default avatar in the UI when empty
        [StringLength(250)]
        public string? ProfilePhotoUrl { get; set; }

        // Admin can block/unblock a MEMBER account (see AdminController.ToggleBlock).
        // ADMIN accounts can never be blocked — enforced in the controller, not here.
        [Required]
        public bool IsBlocked { get; set; } = false;

        // Saved shipping/pickup address for Takeaway orders — one per account,
        // auto-filled at Checkout and kept up to date whenever the customer enters a new one.
        [StringLength(250)]
        public string? Address { get; set; }
    }

    public class Table
    {
        [Key]
        public int TableId { get; set; }

        [Required]
        [StringLength(50)]
        public required string TableName { get; set; }

        // Kept for backwards compatibility: every existing view/controller reads IsOccupied.
        // It is kept in sync from Status (see SetStatus below) so the old and new views agree.
        [Required]
        public bool IsOccupied { get; set; } = false;

        // ------------------------------------------
        // Table management (Workflow.md §3.5)
        // ------------------------------------------
        [Range(1, 50, ErrorMessage = "Capacity must be between 1 and 50")]
        public int Capacity { get; set; } = 4;

        [StringLength(50)]
        public string? Location { get; set; } // e.g. "Window", "Terrace", "Main Hall"

        // "Available" | "Reserved" | "Occupied" | "Cleaning" | "Unavailable"
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = Models.TableState.Available;

        /// <summary>Sets Status and keeps the legacy IsOccupied flag consistent with it.</summary>
        public void SetStatus(string status)
        {
            Status = status;
            IsOccupied = status == Models.TableState.Occupied || status == Models.TableState.Reserved;
        }
    }

    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Please enter category name")]
        [StringLength(50, ErrorMessage = "Category name cannot exceed 50 characters")]
        public required string CategoryName { get; set; }

        // Navigation collection — no DB change, EF Core wires this up using the
        // existing Product.CategoryId FK. Lets Admin's .Include(p => p.Category) work.
        public List<Product>? Products { get; set; }
    }

    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Please enter product name")]
        [StringLength(100, ErrorMessage = "Product name cannot exceed 100 characters")]
        public required string ProductName { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please enter product price")]
        [Range(0.01, 999.99, ErrorMessage = "Price must be between 0.01 and 999.99")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }

        // Nullable on purpose: CreateProduct's form uploads the actual image via a
        // separate IFormFile, not this text field, so it's null at model-binding
        // time — a non-nullable string here would trip the "implicit required"
        // trap and block every product creation with "The ImageUrl field is required."
        [StringLength(250)]
        public string? ImageUrl { get; set; }

        [Required(ErrorMessage = "Please select a category")]
        public int CategoryId { get; set; }

        // Navigation property for the existing CategoryId FK — no DB change,
        // just wires up EF Core so .Include(p => p.Category) works.
        // [BindNever] stops ASP.NET Core from trying to model-bind this from
        // form data when a Product is posted (only CategoryId should bind).
        [ForeignKey("CategoryId")]
        [BindNever]
        public Category? Category { get; set; }

        // Available stock — decremented when an order is actually placed,
        // restored if that order is later cancelled. See OrderController.
        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative")]
        public int StockQuantity { get; set; } = 50;

        // Menu management (Workflow.md §3.4): a deactivated dish disappears from the customer
        // menu without losing its order history.
        public bool IsActive { get; set; } = true;

        // Optional customisation choices offered for this dish (e.g. "Extra Cheese +RM2").
        public List<ProductAddon>? Addons { get; set; }
    }

    // ==========================================
    // Add-ons (Workflow.md §3.4)
    // ==========================================
    public class ProductAddon
    {
        [Key]
        public int AddonId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Please enter add-on name")]
        [StringLength(100, ErrorMessage = "Add-on name cannot exceed 100 characters")]
        public required string Name { get; set; }

        [Required]
        [Range(0, 999.99, ErrorMessage = "Add-on price must be between 0 and 999.99")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [Required]
        public int UserId { get; set; }

        public int? TableId { get; set; } // nullable = takeaway order

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [StringLength(20)]
        public string OrderStatus { get; set; } = Models.OrderStatus.Pending; // see Models/Statuses.cs

        // ------------------------------------------
        // Checkout details (added for the Order/Checkout module)
        // ------------------------------------------
        [StringLength(20)]
        public string? DiningOption { get; set; } // "DineIn" | "Takeaway" | "Delivery"

        [StringLength(30)]
        public string? PaymentMethod { get; set; } // e.g. "Cash", "EWallet"

        // Snapshot of the address used for THIS order (Delivery orders).
        // Kept separate from User.Address so editing the saved address later
        // doesn't rewrite the history of past orders.
        [StringLength(250)]
        public string? DeliveryAddress { get; set; }

        // ------------------------------------------
        // Price breakdown (Workflow.md §2.4) — TotalAmount stays the grand total
        // ------------------------------------------
        [Column(TypeName = "decimal(18, 2)")]
        public decimal SubtotalAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal ServiceCharge { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal DeliveryFee { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal DiscountAmount { get; set; } // reserved for promotions (Phase 3)

        // ------------------------------------------
        // Order lifecycle (Workflow.md §2.6 / §3.3)
        // ------------------------------------------
        public DateTime? StatusUpdatedDate { get; set; }

        // Recorded when an order is rejected/cancelled (Workflow.md §3.3, §8.9)
        [StringLength(250)]
        public string? CancellationReason { get; set; }
    }

    public class OrderDetail
    {
        [Key]
        public int OrderDetailId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100")]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal PriceAtOrder { get; set; } // snapshot of the price at order time

        // Customer's notes for this line, e.g. "no onions" (Workflow.md §2.3)
        [StringLength(200)]
        public string? SpecialInstructions { get; set; }

        // Snapshot of the add-ons chosen for this line (name + price at order time)
        public List<OrderItemAddon>? Addons { get; set; }
    }

    // ==========================================
    // Add-on snapshot recorded against an order line.
    // Price is copied so later edits to ProductAddon never rewrite order history.
    // ==========================================
    public class OrderItemAddon
    {
        [Key]
        public int OrderItemAddonId { get; set; }

        [Required]
        public int OrderDetailId { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal PriceAtOrder { get; set; }
    }

    // ==========================================
    // Payment record (Workflow.md §3.9)
    // ==========================================
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [StringLength(30)]
        public string? Method { get; set; } // "Cash" | "EWallet"

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = Models.PaymentStatus.Paid; // UNPAID | PAID | REFUNDED

        [StringLength(50)]
        public string? Reference { get; set; } // mock transaction id

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? PaidDate { get; set; }
    }

    // ==========================================
    // Table reservation (Workflow.md §2.5 / §3.6)
    // ==========================================
    public class Reservation
    {
        [Key]
        public int ReservationId { get; set; }

        // Nullable: the restaurant can also take reservations at the counter.
        public int? UserId { get; set; }

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
        public DateTime ReservationDate { get; set; }

        [Required(ErrorMessage = "Please choose a time")]
        public TimeSpan ReservationTime { get; set; }

        [Required(ErrorMessage = "Please enter the number of guests")]
        [Range(1, 50, ErrorMessage = "Guests must be between 1 and 50")]
        public int Guests { get; set; } = 2;

        // Assigned table — filled in by the customer's choice or by an admin approving the booking.
        public int? TableId { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = Models.ReservationStatus.Pending;

        [StringLength(250)]
        public string? SpecialRequests { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>Date + time as a single value, used for overlap checks.</summary>
        public DateTime StartsAt => ReservationDate.Date + ReservationTime;

        /// <summary>A booking holds its table for this long (used when detecting conflicts).</summary>
        public const int DurationMinutes = 120;

        public DateTime EndsAt => StartsAt.AddMinutes(DurationMinutes);

        /// <summary>True while the booking still reserves a table.</summary>
        public bool IsActive =>
            Status == Models.ReservationStatus.Pending || Status == Models.ReservationStatus.Confirmed;
    }

    // ==========================================
    // Favorite — records which member favorited which product, persisted forever
    // ==========================================
    public class Favorite
    {
        [Key]
        public int FavoriteId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime AddedDate { get; set; } = DateTime.Now;
    }
}