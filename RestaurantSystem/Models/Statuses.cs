using System;
using System.Collections.Generic;

namespace RestaurantSystem.Models
{
    /// <summary>
    /// Central definition of every status string used by the system.
    ///
    /// Why this exists: the status values were previously written as bare string literals all over
    /// the controllers ("PENDING", "CANCELLED", ...). That made it easy for the admin dropdown, the
    /// report filters and the customer pages to drift apart. Everything now references these
    /// constants, and the allowed transitions live here in one place (see Workflow.md §2.6 / §3.3).
    /// </summary>
    public static class OrderStatus
    {
        public const string Pending = "PENDING";     // submitted, awaiting restaurant confirmation
        public const string Confirmed = "CONFIRMED"; // accepted by the restaurant
        public const string Preparing = "PREPARING"; // kitchen is preparing the food
        public const string Ready = "READY";         // ready for serving or pickup
        public const string Completed = "COMPLETED"; // finished
        public const string Cancelled = "CANCELLED"; // cancelled / rejected

        /// <summary>Statuses in the order they progress through.</summary>
        public static readonly string[] Flow =
        {
            Pending, Confirmed, Preparing, Ready, Completed
        };

        /// <summary>Every status the system recognises.</summary>
        public static readonly string[] All =
        {
            Pending, Confirmed, Preparing, Ready, Completed, Cancelled
        };

        /// <summary>Statuses that are still "live" and appear in the kitchen queue.</summary>
        public static readonly string[] Active =
        {
            Confirmed, Preparing, Ready
        };

        /// <summary>
        /// Which transitions are legal. A status can always stay the same (no-op edits), and any
        /// live order may be cancelled — but a completed or cancelled order is final.
        /// </summary>
        private static readonly Dictionary<string, string[]> _allowed = new()
        {
            [Pending] = new[] { Confirmed, Cancelled },
            [Confirmed] = new[] { Preparing, Cancelled },
            [Preparing] = new[] { Ready, Cancelled },
            [Ready] = new[] { Completed },
            [Completed] = Array.Empty<string>(),
            [Cancelled] = Array.Empty<string>(),
        };

        public static bool IsValid(string? status) =>
            status != null && Array.IndexOf(All, status) >= 0;

        /// <summary>True if moving from <paramref name="from"/> to <paramref name="to"/> is allowed.</summary>
        public static bool CanTransition(string? from, string? to)
        {
            if (!IsValid(to)) return false;
            if (string.Equals(from, to, StringComparison.Ordinal)) return true; // no-op
            if (from == null || !_allowed.TryGetValue(from, out var allowed)) return false;
            return Array.IndexOf(allowed, to!) >= 0;
        }

        /// <summary>The statuses the given status may move to (used to render the correct buttons).</summary>
        public static IReadOnlyList<string> NextStatuses(string? from)
        {
            if (from == null || !_allowed.TryGetValue(from, out var allowed)) return Array.Empty<string>();
            return allowed;
        }

        /// <summary>Customer-facing label and badge colour for each status.</summary>
        public static string Label(string? status) => status switch
        {
            Pending => "Pending",
            Confirmed => "Confirmed",
            Preparing => "Preparing",
            Ready => "Ready",
            Completed => "Completed",
            Cancelled => "Cancelled",
            _ => status ?? "Unknown",
        };

        public static string BadgeClass(string? status) => status switch
        {
            Pending => "bg-warning text-dark",
            Confirmed => "bg-info text-dark",
            Preparing => "bg-primary",
            Ready => "bg-success",
            Completed => "bg-secondary",
            Cancelled => "bg-danger",
            _ => "bg-light text-dark",
        };

        /// <summary>Short helper text shown to the customer on the tracking timeline.</summary>
        public static string Description(string? status) => status switch
        {
            Pending => "Order submitted and awaiting restaurant confirmation.",
            Confirmed => "The restaurant has accepted your order.",
            Preparing => "Our kitchen is preparing your food.",
            Ready => "Your food is ready for serving or pickup.",
            Completed => "Order completed. Thank you for dining with us!",
            Cancelled => "This order was cancelled.",
            _ => "",
        };
    }

    /// <summary>How the customer receives the order (Workflow.md §2.4).</summary>
    public static class OrderType
    {
        public const string DineIn = "DineIn";
        public const string Takeaway = "Takeaway";
        public const string Delivery = "Delivery";

        public static readonly string[] All = { DineIn, Takeaway, Delivery };

        public static bool IsValid(string? type) => type != null && Array.IndexOf(All, type) >= 0;

        public static string Label(string? type) => type switch
        {
            DineIn => "Dine-in",
            Takeaway => "Takeaway",
            Delivery => "Delivery",
            _ => type ?? "Unknown",
        };
    }

    /// <summary>Payment method and status options (Workflow.md §3.9).</summary>
    public static class PaymentMethod
    {
        public const string Cash = "Cash";
        public const string EWallet = "EWallet";

        public static readonly string[] All = { Cash, EWallet };
    }

    public static class PaymentStatus
    {
        public const string Unpaid = "UNPAID";
        public const string Paid = "PAID";
        public const string Refunded = "REFUNDED";

        public static readonly string[] All = { Unpaid, Paid, Refunded };
    }

    /// <summary>Table availability states (Workflow.md §3.5).</summary>
    public static class TableState
    {
        public const string Available = "Available";
        public const string Reserved = "Reserved";
        public const string Occupied = "Occupied";
        public const string Cleaning = "Cleaning";
        public const string Unavailable = "Unavailable";

        public static readonly string[] All = { Available, Reserved, Occupied, Cleaning, Unavailable };

        public static bool IsValid(string? state) => state != null && Array.IndexOf(All, state) >= 0;

        /// <summary>True when the table can be offered to a walk-in / dine-in customer.</summary>
        public static bool IsFree(string? state) =>
            state == Available || state == Cleaning;

        public static string BadgeClass(string? state) => state switch
        {
            Available => "bg-success",
            Reserved => "bg-info text-dark",
            Occupied => "bg-danger",
            Cleaning => "bg-warning text-dark",
            Unavailable => "bg-secondary",
            _ => "bg-light text-dark",
        };
    }

    /// <summary>Reservation lifecycle (Workflow.md §2.5 / §3.6).</summary>
    public static class ReservationStatus
    {
        public const string Pending = "PENDING";
        public const string Confirmed = "CONFIRMED";
        public const string Cancelled = "CANCELLED";
        public const string Rejected = "REJECTED";
        public const string Completed = "COMPLETED";

        public static readonly string[] All = { Pending, Confirmed, Cancelled, Rejected, Completed };

        public static bool IsValid(string? status) => status != null && Array.IndexOf(All, status) >= 0;

        public static string Label(string? status) => status switch
        {
            Pending => "Pending",
            Confirmed => "Confirmed",
            Cancelled => "Cancelled",
            Rejected => "Rejected",
            Completed => "Completed",
            _ => status ?? "Unknown",
        };

        public static string BadgeClass(string? status) => status switch
        {
            Pending => "bg-warning text-dark",
            Confirmed => "bg-success",
            Cancelled => "bg-secondary",
            Rejected => "bg-danger",
            Completed => "bg-dark",
            _ => "bg-light text-dark",
        };
    }
}
