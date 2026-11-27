using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantSystem.Models;
using static RestaurantSystem.Models.ViewModels;

namespace RestaurantSystem.Controllers
{
    /// <summary>
    /// Customer-facing table reservations (Workflow.md §2.5).
    ///
    /// The critical rule is §8.4 — the system must prevent conflicting reservations for the same
    /// table and time. <see cref="HasConflict"/> is the single implementation of that rule and is
    /// used by both the customer form and the admin screens.
    /// </summary>
    [Authorize]
    public class ReservationController : Controller
    {
        private readonly RestaurantDbContext db;

        // Opening hours the restaurant accepts bookings for (whole hours).
        private static readonly TimeSpan OpensAt = new(11, 0, 0);
        private static readonly TimeSpan LastBooking = new(21, 0, 0);

        public ReservationController(RestaurantDbContext db)
        {
            this.db = db;
        }

        // ==========================================
        // [GET: My reservations]
        // ==========================================
        [HttpGet]
        public IActionResult Index()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var mine = db.Reservations
                .Where(r => r.UserId == userId.Value)
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.ReservationTime)
                .ToList();

            return View(mine);
        }

        // ==========================================
        // [GET/POST: Create a reservation]
        // ==========================================
        [HttpGet]
        public IActionResult Create()
        {
            var vm = new ReservationVM
            {
                CustomerName = "",
                Phone = "",
                ReservationDate = DateTime.Today.AddDays(1),
                ReservationTime = "19:00",
                Guests = 2,
                AvailableTables = FreeTables()
            };

            // Pre-fill from the signed-in account when possible
            var user = CurrentUser();
            if (user != null)
            {
                vm.CustomerName = user.FullName;
                vm.Phone = user.PhoneNumber;
                vm.Email = user.Email;
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ReservationVM vm)
        {
            Validate(vm);

            if (!ModelState.IsValid)
            {
                vm.AvailableTables = FreeTables();
                return View(vm);
            }

            var reservation = new Reservation
            {
                UserId = GetCurrentUserId(),
                CustomerName = vm.CustomerName.Trim(),
                Phone = vm.Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
                ReservationDate = vm.ReservationDate.Date,
                ReservationTime = vm.ParsedTime!.Value,
                Guests = vm.Guests,
                TableId = vm.TableId,
                Status = ReservationStatus.Pending,
                SpecialRequests = vm.SpecialRequests,
                CreatedDate = DateTime.Now
            };

            db.Reservations.Add(reservation);
            db.SaveChanges();

            TempData["Info"] = "Your reservation request has been received. We'll confirm it shortly.";
            return RedirectToAction("Index");
        }

        // ==========================================
        // [GET/POST: Edit an existing reservation]
        // ==========================================
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var r = db.Reservations.FirstOrDefault(x => x.ReservationId == id && x.UserId == userId.Value);
            if (r == null) return NotFound();

            if (r.Status != ReservationStatus.Pending)
            {
                TempData["Info"] = "Only a pending reservation can be changed.";
                return RedirectToAction("Index");
            }

            return View("Create", new ReservationVM
            {
                ReservationId = r.ReservationId,
                CustomerName = r.CustomerName,
                Phone = r.Phone,
                Email = r.Email,
                ReservationDate = r.ReservationDate,
                ReservationTime = r.ReservationTime.ToString(@"hh\:mm"),
                Guests = r.Guests,
                TableId = r.TableId,
                SpecialRequests = r.SpecialRequests,
                Status = r.Status,
                AvailableTables = FreeTables()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(ReservationVM vm, int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var r = db.Reservations.FirstOrDefault(x => x.ReservationId == id && x.UserId == userId.Value);
            if (r == null) return NotFound();

            if (r.Status != ReservationStatus.Pending)
            {
                TempData["Info"] = "Only a pending reservation can be changed.";
                return RedirectToAction("Index");
            }

            vm.ReservationId = id;
            Validate(vm, excludeReservationId: id);

            if (!ModelState.IsValid)
            {
                vm.AvailableTables = FreeTables();
                return View("Create", vm);
            }

            r.CustomerName = vm.CustomerName.Trim();
            r.Phone = vm.Phone.Trim();
            r.Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
            r.ReservationDate = vm.ReservationDate.Date;
            r.ReservationTime = vm.ParsedTime!.Value;
            r.Guests = vm.Guests;
            r.TableId = vm.TableId;
            r.SpecialRequests = vm.SpecialRequests;
            db.SaveChanges();

            TempData["Info"] = "Your reservation has been updated.";
            return RedirectToAction("Index");
        }

        // ==========================================
        // [POST: Cancel my reservation]
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var r = db.Reservations.FirstOrDefault(x => x.ReservationId == id && x.UserId == userId.Value);
            if (r == null) return NotFound();

            if (!r.IsActive)
            {
                TempData["Info"] = "This reservation is already closed.";
                return RedirectToAction("Index");
            }

            r.Status = ReservationStatus.Cancelled;
            db.SaveChanges();

            TempData["Info"] = "Your reservation has been cancelled.";
            return RedirectToAction("Index");
        }

        // ==========================================
        // Helpers
        // ==========================================

        /// <summary>
        /// Shared validation for create/edit: date not in the past, within opening hours, and no
        /// clashing booking for the chosen table (Workflow.md §8.4).
        /// </summary>
        private void Validate(ReservationVM vm, int? excludeReservationId = null)
        {
            var time = vm.ParsedTime;
            if (time == null)
            {
                ModelState.AddModelError(nameof(vm.ReservationTime), "Please enter a valid time.");
                return;
            }

            var startsAt = vm.ReservationDate.Date + time.Value;

            if (vm.ReservationDate.Date < DateTime.Today)
            {
                ModelState.AddModelError(nameof(vm.ReservationDate), "Please choose today or a future date.");
            }

            if (time.Value < OpensAt || time.Value > LastBooking)
            {
                ModelState.AddModelError(nameof(vm.ReservationTime),
                    $"Bookings are taken between {OpensAt:hh\\:mm} and {LastBooking:hh\\:mm}.");
            }

            // Only check conflicts for a well-formed request
            if (ModelState.IsValid && vm.TableId.HasValue)
            {
                var table = db.Tables.FirstOrDefault(t => t.TableId == vm.TableId.Value);
                if (table == null)
                {
                    ModelState.AddModelError(nameof(vm.TableId), "That table no longer exists.");
                }
                else
                {
                    if (table.Status == TableState.Unavailable)
                    {
                        ModelState.AddModelError(nameof(vm.TableId), $"{table.TableName} is currently unavailable.");
                    }
                    if (vm.Guests > table.Capacity)
                    {
                        ModelState.AddModelError(nameof(vm.TableId),
                            $"{table.TableName} seats up to {table.Capacity} guests. Please pick a larger table.");
                    }

                    var clash = FindConflict(vm.TableId.Value, startsAt, excludeReservationId);
                    if (clash != null)
                    {
                        ModelState.AddModelError(nameof(vm.TableId),
                            $"{table.TableName} is already booked at {clash.StartsAt:HH:mm} on {clash.StartsAt:dd MMM}. " +
                            "Please choose another table or time.");
                    }
                }
            }
        }

        /// <summary>
        /// Returns the first active reservation that overlaps the proposed slot on the given table,
        /// or null when the slot is free. A booking occupies its table for
        /// <see cref="Reservation.DurationMinutes"/> (2 hours), so a 19:00 booking blocks 19:00-21:00.
        /// </summary>
        private Reservation? FindConflict(int tableId, DateTime startsAt, int? excludeReservationId = null)
        {
            var proposedStart = startsAt;
            var proposedEnd = startsAt.AddMinutes(Reservation.DurationMinutes);

            var sameDay = db.Reservations
                .Where(r => r.TableId == tableId && r.ReservationDate == proposedStart.Date)
                .ToList();

            if (excludeReservationId.HasValue)
            {
                sameDay = sameDay.Where(r => r.ReservationId != excludeReservationId.Value).ToList();
            }

            return sameDay.FirstOrDefault(r =>
                r.IsActive &&
                proposedStart < r.EndsAt &&
                r.StartsAt < proposedEnd);
        }

        private List<Table> FreeTables() =>
            db.Tables
              .Where(t => t.Status != TableState.Unavailable && t.Status != TableState.Occupied)
              .OrderBy(t => t.TableName)
              .ToList();

        private User? CurrentUser()
        {
            var username = User?.Identity?.Name;
            return username == null ? null : db.Users.FirstOrDefault(u => u.Username == username);
        }

        private int? GetCurrentUserId() => CurrentUser()?.UserId;
    }
}
