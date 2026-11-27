using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RestaurantSystem.Models;
using System.Linq;
using static RestaurantSystem.Models.ViewModels;

namespace RestaurantSystem.Controllers
{
    [Authorize(Roles = "ADMIN")] // Only logged-in admins can access every action in this controller
    public class AdminController : Controller
    {
        private readonly RestaurantDbContext db;
        private readonly Helper hp;
        private readonly IWebHostEnvironment env;

        public AdminController(RestaurantDbContext db, Helper hp, IWebHostEnvironment env)
        {
            this.db = db;
            this.hp = hp;
            this.env = env;
        }

        // Absolute path of the folder product images are stored in.
        // Uses WebRootPath (not Directory.GetCurrentDirectory()) so uploads land in
        // the same wwwroot the app serves, no matter which working directory the
        // app was launched from — e.g. Visual Studio, `dotnet run`, or IIS Express.
        private string ImagesFolder => Path.Combine(env.WebRootPath, "images");

        public IActionResult Dashboard()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // Today's revenue: sum of non-cancelled orders placed today
            ViewBag.TodayRevenue = db.Orders
                .Where(o => o.OrderDate >= today && o.OrderDate < tomorrow && o.OrderStatus != "CANCELLED")
                .Sum(o => (decimal?)o.TotalAmount) ?? 0m;

            // Active orders: still pending (not yet completed or cancelled)
            ViewBag.ActiveOrders = db.Orders.Count(o => o.OrderStatus == "PENDING");

            var allTables = db.Tables.OrderBy(t => t.TableId).ToList();
            ViewBag.Tables = allTables;
            ViewBag.OccupiedCount = allTables.Count(t => t.IsOccupied);
            ViewBag.TotalTables = allTables.Count;

            return View();
        }

        // ==========================================
        // [Category CRUD] Category Management
        // ==========================================

        public IActionResult Categories()
        {
            var categories = db.Categories.ToList();
            return View(categories);
        }

        public IActionResult CreateCategory()
        {
            return View(new Category { CategoryName = "" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateCategory(Category category)
        {
            if (!ModelState.IsValid) return View(category);

            db.Categories.Add(category);
            db.SaveChanges();
            TempData["Info"] = "Category created successfully.";
            return RedirectToAction("Categories");
        }

        public IActionResult EditCategory(int id)
        {
            var category = db.Categories.Find(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditCategory(Category category)
        {
            if (!ModelState.IsValid) return View(category);

            var existing = db.Categories.Find(category.CategoryId);
            if (existing == null) return NotFound();

            existing.CategoryName = category.CategoryName;
            db.SaveChanges();
            TempData["Info"] = "Category updated successfully.";
            return RedirectToAction("Categories");
        }

        public IActionResult DeleteCategory(int id)
        {
            var category = db.Categories.Find(id);
            if (category == null) return NotFound();

            ViewBag.HasProducts = db.Products.Any(p => p.CategoryId == id);
            return View(category);
        }

        [HttpPost, ActionName("DeleteCategory")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategoryConfirmed(int id)
        {
            var category = db.Categories.Find(id);
            if (category == null) return NotFound();

            if (db.Products.Any(p => p.CategoryId == id))
            {
                ViewBag.HasProducts = true;
                return View("DeleteCategory", category);
            }

            db.Categories.Remove(category);
            db.SaveChanges();
            TempData["Info"] = "Category deleted successfully.";
            return RedirectToAction("Categories");
        }

        // ==========================================
        // [Product CRUD] Product Management
        // ==========================================

        public IActionResult Products()
        {
            var products = db.Products.Include(p => p.Category).ToList();
            return View(products);
        }

        public IActionResult CreateProduct()
        {
            ViewBag.Categories = db.Categories.ToList();
            return View(new Product { ProductName = "" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product, IFormFile? imageFile)
        {
            ViewBag.Categories = db.Categories.ToList();
            if (!ModelState.IsValid) return View(product);

            // Save first so product.ProductId is actually generated by the DB —
            // the previous version tried to name the image file using ProductId
            // BEFORE this SaveChanges, so it always got saved as "0_filename.jpg"
            // and the image ended up broken after creation.
            db.Products.Add(product);
            db.SaveChanges();

            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = $"{product.ProductId}_{Path.GetFileName(imageFile.FileName)}";
                var path = Path.Combine(ImagesFolder, fileName);
                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                product.ImageUrl = "/images/" + fileName;
                db.SaveChanges();
            }

            TempData["Info"] = "Product created successfully.";
            return RedirectToAction("Products");
        }

        public IActionResult EditProduct(int id)
        {
            var product = db.Products.Find(id);
            if (product == null) return NotFound();

            ViewBag.Categories = db.Categories.ToList();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(Product product, IFormFile? imageFile)
        {
            ViewBag.Categories = db.Categories.ToList();
            if (!ModelState.IsValid) return View(product);

            var existing = db.Products.Find(product.ProductId);
            if (existing == null) return NotFound();

            existing.ProductName = product.ProductName;
            existing.CategoryId = product.CategoryId;
            existing.Price = product.Price;
            existing.StockQuantity = product.StockQuantity; // was product.Stock — renamed to match our schema
            existing.Description = product.Description;

            if (imageFile != null && imageFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(existing.ImageUrl))
                {
                    var oldPath = Path.Combine(ImagesFolder, Path.GetFileName(existing.ImageUrl));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                var fileName = $"{existing.ProductId}_{Path.GetFileName(imageFile.FileName)}";
                var path = Path.Combine(ImagesFolder, fileName);
                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                existing.ImageUrl = "/images/" + fileName;
            }

            db.SaveChanges();
            TempData["Info"] = "Product updated successfully.";
            return RedirectToAction("Products");
        }

        public IActionResult DeleteProduct(int id)
        {
            var product = db.Products.Include(p => p.Category).FirstOrDefault(p => p.ProductId == id);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost, ActionName("DeleteProduct")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteProductConfirmed(int id)
        {
            var product = db.Products.Find(id);
            if (product == null) return NotFound();

            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                var path = Path.Combine(ImagesFolder, Path.GetFileName(product.ImageUrl));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }

            db.Products.Remove(product);
            db.SaveChanges();
            TempData["Info"] = "Product deleted successfully.";
            return RedirectToAction("Products");
        }

        // ==========================================
        // [POST: Admin/DeleteProductsBatch] Delete multiple products at once
        // ==========================================
        [HttpPost]
        public IActionResult DeleteProductsBatch(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                TempData["Error"] = "No products selected.";
                return RedirectToAction("Products");
            }

            var products = db.Products.Where(p => ids.Contains(p.ProductId)).ToList();
            foreach (var product in products)
            {
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var path = Path.Combine(ImagesFolder, Path.GetFileName(product.ImageUrl));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
            }

            db.Products.RemoveRange(products);
            db.SaveChanges();

            TempData["Info"] = $"{products.Count} product(s) deleted successfully.";
            return RedirectToAction("Products");
        }

        // ==========================================
        // [GET: Admin/Orders] Order management list (status filter + search + order details)
        // ==========================================
        public IActionResult Orders(string? status, string? search)
        {
            IQueryable<Order> query = db.Orders;

            // Status filter: ALL / PENDING / COMPLETED / CANCELLED
            if (!string.IsNullOrEmpty(status) && status != "ALL")
            {
                query = query.Where(o => o.OrderStatus == status);
            }

            var orders = query.OrderByDescending(o => o.OrderDate).ToList();
            var orderVMs = BuildOrderRows(orders, includeDetails: true);

            // Keyword search: order number / customer name / table name
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                orderVMs = orderVMs.Where(o =>
                    o.OrderId.ToString().Contains(search) ||
                    o.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (o.TableName != null && o.TableName.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            ViewBag.SelectedStatus = string.IsNullOrEmpty(status) ? "ALL" : status;
            ViewBag.Search = search;

            return View(orderVMs);
        }

        /// <summary>
        /// Projects orders into OrderVM rows, loading users / tables / details / add-ons in bulk.
        /// Order and OrderDetail intentionally have no navigation properties (see Agent.md), so
        /// the joins are done in memory with dictionaries.
        /// </summary>
        private List<OrderVM> BuildOrderRows(List<Order> orders, bool includeDetails)
        {
            var userIds = orders.Select(o => o.UserId).Distinct().ToList();
            var tableIds = orders.Where(o => o.TableId.HasValue).Select(o => o.TableId!.Value).Distinct().ToList();
            var orderIds = orders.Select(o => o.OrderId).Distinct().ToList();

            var users = db.Users.Where(u => userIds.Contains(u.UserId)).ToDictionary(u => u.UserId);
            var tables = db.Tables.Where(t => tableIds.Contains(t.TableId)).ToDictionary(t => t.TableId);

            var allDetails = includeDetails
                ? db.OrderDetails.Where(d => orderIds.Contains(d.OrderId)).ToList()
                : new List<OrderDetail>();

            var productIds = allDetails.Select(d => d.ProductId).Distinct().ToList();
            var products = db.Products.Where(p => productIds.Contains(p.ProductId)).ToDictionary(p => p.ProductId);

            var detailIds = allDetails.Select(d => d.OrderDetailId).ToList();
            var addonLookup = db.OrderItemAddons
                .Where(a => detailIds.Contains(a.OrderDetailId))
                .ToList()
                .GroupBy(a => a.OrderDetailId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.Name).ToList());

            return orders.Select(o => new OrderVM
            {
                OrderId = o.OrderId,
                CustomerName = users.TryGetValue(o.UserId, out var user) ? user.Username : "Unknown",
                TableName = o.TableId.HasValue && tables.TryGetValue(o.TableId.Value, out var tbl) ? tbl.TableName : null,
                OrderDate = o.OrderDate,
                TotalAmount = o.TotalAmount,
                OrderStatus = o.OrderStatus,
                DiningOption = o.DiningOption,
                PaymentMethod = o.PaymentMethod,
                DeliveryAddress = o.DeliveryAddress,
                CancellationReason = o.CancellationReason,
                StatusUpdatedDate = o.StatusUpdatedDate,
                Details = allDetails
                    .Where(d => d.OrderId == o.OrderId)
                    .Select(d => new OrderDetailVM
                    {
                        ProductName = products.TryGetValue(d.ProductId, out var prod) ? prod.ProductName : "Unknown",
                        Quantity = d.Quantity,
                        PriceAtOrder = d.PriceAtOrder,
                        SpecialInstructions = d.SpecialInstructions,
                        Addons = addonLookup.TryGetValue(d.OrderDetailId, out var names) ? names : new List<string>()
                    }).ToList()
            }).ToList();
        }

        // ==========================================
        // [POST: Admin/UpdateOrderStatus] Move one order through the lifecycle.
        // Workflow.md §3.3: accept -> preparing -> ready -> completed, or reject with a reason.
        // ==========================================
        [HttpPost]
        public IActionResult UpdateOrderStatus(int id, string status, string? reason = null)
        {
            var order = db.Orders.Find(id);
            if (order == null)
            {
                return NotFound();
            }

            if (!OrderStatus.IsValid(status))
            {
                TempData["Error"] = "Invalid order status.";
                return RedirectToAction("Orders");
            }

            // Only legal transitions are accepted — e.g. a completed order cannot go back to pending.
            if (!OrderStatus.CanTransition(order.OrderStatus, status))
            {
                TempData["Error"] =
                    $"Order #{order.OrderId} is {OrderStatus.Label(order.OrderStatus)} and cannot be changed to " +
                    $"{OrderStatus.Label(status)}.";
                return RedirectToAction("Orders");
            }

            ApplyStatusChange(order, status, reason);
            db.SaveChanges();

            TempData["Info"] = $"Order #{order.OrderId} is now {OrderStatus.Label(status)}.";
            return RedirectToAction("Orders");
        }

        /// <summary>
        /// Applies a status change plus everything that must follow from it: timestamp, table
        /// release/occupancy, stock restoration on cancellation, payment settlement and reason.
        /// Shared by the single and batch update actions and the kitchen queue.
        /// </summary>
        private void ApplyStatusChange(Order order, string status, string? reason = null)
        {
            var previous = order.OrderStatus;

            order.OrderStatus = status;
            order.StatusUpdatedDate = DateTime.Now;

            if (status == OrderStatus.Cancelled)
            {
                order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by staff" : reason.Trim();

                // Free the table this order held
                if (order.TableId.HasValue)
                {
                    var table = db.Tables.FirstOrDefault(t => t.TableId == order.TableId.Value);
                    table?.SetStatus(TableState.Available);
                }

                // Restore stock once, only if the order was still live (never double-restore)
                bool wasLive = previous != OrderStatus.Cancelled && previous != OrderStatus.Completed;
                if (wasLive)
                {
                    foreach (var detail in db.OrderDetails.Where(d => d.OrderId == order.OrderId).ToList())
                    {
                        var product = db.Products.FirstOrDefault(p => p.ProductId == detail.ProductId);
                        if (product != null) product.StockQuantity += detail.Quantity;
                    }
                }

                var cancelledPayment = db.Payments.FirstOrDefault(p => p.OrderId == order.OrderId);
                if (cancelledPayment != null && cancelledPayment.Status == PaymentStatus.Paid)
                {
                    cancelledPayment.Status = PaymentStatus.Refunded;
                }
            }
            else if (status == OrderStatus.Completed)
            {
                // A completed order releases its table and settles any cash payment.
                if (order.TableId.HasValue)
                {
                    var table = db.Tables.FirstOrDefault(t => t.TableId == order.TableId.Value);
                    table?.SetStatus(TableState.Cleaning);
                }

                var payment = db.Payments.FirstOrDefault(p => p.OrderId == order.OrderId);
                if (payment != null && payment.Status == PaymentStatus.Unpaid)
                {
                    payment.Status = PaymentStatus.Paid;
                    payment.PaidDate = DateTime.Now;
                }
            }
        }

        // ==========================================
        // [POST: Admin/UpdateOrderStatusBatch] Update multiple orders' status at once
        // ==========================================
        [HttpPost]
        public IActionResult UpdateOrderStatusBatch(int[] ids, string status)
        {
            if (!OrderStatus.IsValid(status))
            {
                TempData["Error"] = "Invalid order status.";
                return RedirectToAction("Orders");
            }

            if (ids == null || ids.Length == 0)
            {
                TempData["Error"] = "No orders selected.";
                return RedirectToAction("Orders");
            }

            var orders = db.Orders.Where(o => ids.Contains(o.OrderId)).ToList();
            int changed = 0, skipped = 0;
            foreach (var order in orders)
            {
                // Batch updates respect the same transition rules as single updates.
                if (!OrderStatus.CanTransition(order.OrderStatus, status))
                {
                    skipped++;
                    continue;
                }
                ApplyStatusChange(order, status);
                changed++;
            }
            db.SaveChanges();

            TempData["Info"] = $"{changed} order(s) updated to {OrderStatus.Label(status)}." +
                               (skipped > 0 ? $" {skipped} skipped (invalid transition)." : "");
            return RedirectToAction("Orders");
        }

        // ==========================================
        // [GET: Admin/Kitchen] Kitchen preparation queue (Workflow.md §3.3 / §6 "Kitchen Orders")
        // Shows confirmed / preparing / ready orders, oldest first.
        // ==========================================
        [HttpGet]
        public IActionResult Kitchen()
        {
            ViewBag.ActiveMenu = "Kitchen";

            var activeStatuses = OrderStatus.Active.ToArray();
            var orders = db.Orders
                .Where(o => activeStatuses.Contains(o.OrderStatus))
                .OrderBy(o => o.OrderDate)
                .ToList();

            return View(BuildOrderRows(orders, includeDetails: true));
        }

        // ==========================================
        // [Reservations] Admin reservation management (Workflow.md §3.6)
        // ==========================================
        [HttpGet]
        public IActionResult Reservations(string? status, DateTime? date)
        {
            ViewBag.ActiveMenu = "Reservations";

            IQueryable<Reservation> query = db.Reservations;

            if (!string.IsNullOrEmpty(status) && status != "ALL")
            {
                query = query.Where(r => r.Status == status);
            }

            if (date.HasValue)
            {
                query = query.Where(r => r.ReservationDate == date.Value.Date);
            }

            var reservations = query
                .OrderBy(r => r.ReservationDate)
                .ThenBy(r => r.ReservationTime)
                .ToList();

            var tableIds = reservations.Where(r => r.TableId.HasValue).Select(r => r.TableId!.Value).Distinct().ToList();
            var tables = db.Tables.Where(t => tableIds.Contains(t.TableId)).ToDictionary(t => t.TableId);

            var rows = reservations.Select(r => new ReservationRowVM
            {
                ReservationId = r.ReservationId,
                CustomerName = r.CustomerName,
                Phone = r.Phone,
                Email = r.Email,
                ReservationDate = r.ReservationDate,
                ReservationTime = r.ReservationTime,
                Guests = r.Guests,
                TableId = r.TableId,
                TableName = r.TableId.HasValue && tables.TryGetValue(r.TableId.Value, out var t) ? t.TableName : null,
                Status = r.Status,
                SpecialRequests = r.SpecialRequests,
                CreatedDate = r.CreatedDate
            }).ToList();

            ViewBag.SelectedStatus = string.IsNullOrEmpty(status) ? "ALL" : status;
            ViewBag.SelectedDate = date?.ToString("yyyy-MM-dd");

            return View(rows);
        }

        // [POST] Approve / reject / cancel / complete a reservation
        [HttpPost]
        public IActionResult UpdateReservationStatus(int id, string status, int? tableId = null)
        {
            var reservation = db.Reservations.Find(id);
            if (reservation == null) return NotFound();

            if (!RestaurantSystem.Models.ReservationStatus.IsValid(status))
            {
                TempData["Error"] = "Invalid reservation status.";
                return RedirectToAction("Reservations");
            }

            // Re-check the table clash before confirming (Workflow.md §8.4)
            if (status == RestaurantSystem.Models.ReservationStatus.Confirmed)
            {
                int? targetTableId = tableId ?? reservation.TableId;
                if (targetTableId.HasValue)
                {
                    var conflict = db.Reservations
                        .Where(r => r.TableId == targetTableId.Value
                                    && r.ReservationDate == reservation.ReservationDate
                                    && r.ReservationId != reservation.ReservationId)
                        .ToList()
                        .FirstOrDefault(r => r.IsActive &&
                                             reservation.StartsAt < r.EndsAt &&
                                             r.StartsAt < reservation.EndsAt);

                    if (conflict != null)
                    {
                        TempData["Error"] =
                            $"Cannot confirm — that table is already booked at {conflict.StartsAt:HH:mm} for {conflict.CustomerName}.";
                        return RedirectToAction("Reservations");
                    }
                }
            }

            reservation.Status = status;
            if (tableId.HasValue) reservation.TableId = tableId;

            // Keep the physical table state in step with the booking
            if (reservation.TableId.HasValue)
            {
                var table = db.Tables.FirstOrDefault(t => t.TableId == reservation.TableId.Value);
                if (table != null && table.Status == TableState.Available && reservation.IsActive)
                {
                    table.SetStatus(TableState.Reserved);
                }
                else if (table != null && table.Status == TableState.Reserved && !reservation.IsActive)
                {
                    table.SetStatus(TableState.Available);
                }
            }

            db.SaveChanges();

            TempData["Info"] = $"Reservation #{reservation.ReservationId} is now {RestaurantSystem.Models.ReservationStatus.Label(status)}.";
            return RedirectToAction("Reservations");
        }

        // [POST] Assign a table to a reservation
        [HttpPost]
        public IActionResult AssignReservationTable(int id, int tableId)
        {
            var reservation = db.Reservations.Find(id);
            if (reservation == null) return NotFound();

            var table = db.Tables.FirstOrDefault(t => t.TableId == tableId);
            if (table == null)
            {
                TempData["Error"] = "That table does not exist.";
                return RedirectToAction("Reservations");
            }

            if (reservation.Guests > table.Capacity)
            {
                TempData["Error"] = $"{table.TableName} seats only {table.Capacity}. Please pick a larger table.";
                return RedirectToAction("Reservations");
            }

            var conflict = db.Reservations
                .Where(r => r.TableId == tableId
                            && r.ReservationDate == reservation.ReservationDate
                            && r.ReservationId != reservation.ReservationId)
                .ToList()
                .FirstOrDefault(r => r.IsActive && reservation.StartsAt < r.EndsAt && r.StartsAt < reservation.EndsAt);

            if (conflict != null)
            {
                TempData["Error"] = $"{table.TableName} is already booked at {conflict.StartsAt:HH:mm}.";
                return RedirectToAction("Reservations");
            }

            reservation.TableId = tableId;
            db.SaveChanges();

            TempData["Info"] = $"{table.TableName} assigned to reservation #{reservation.ReservationId}.";
            return RedirectToAction("Reservations");
        }

        // ==========================================
        // [Tables] Table management (Workflow.md §3.5)
        // ==========================================
        [HttpGet]
        public IActionResult Tables()
        {
            ViewBag.ActiveMenu = "Tables";

            var rows = db.Tables
                .OrderBy(t => t.TableId)
                .Select(t => new TableRowVM
                {
                    TableId = t.TableId,
                    TableName = t.TableName,
                    Capacity = t.Capacity,
                    Location = t.Location,
                    Status = t.Status,
                    IsOccupied = t.IsOccupied
                })
                .ToList();

            return View(rows);
        }

        // [POST] Change a table's status
        [HttpPost]
        public IActionResult UpdateTableStatus(int id, string status)
        {
            var table = db.Tables.Find(id);
            if (table == null) return NotFound();

            if (!TableState.IsValid(status))
            {
                TempData["Error"] = "Invalid table status.";
                return RedirectToAction("Tables");
            }

            table.SetStatus(status);
            db.SaveChanges();

            TempData["Info"] = $"{table.TableName} is now {status}.";
            return RedirectToAction("Tables");
        }

        // [POST] Create a new table
        [HttpPost]
        public IActionResult CreateTable(string tableName, int capacity, string? location)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                TempData["Error"] = "Please enter a table name.";
                return RedirectToAction("Tables");
            }

            if (capacity < 1)
            {
                TempData["Error"] = "Capacity must be at least 1.";
                return RedirectToAction("Tables");
            }

            db.Tables.Add(new Table
            {
                TableName = tableName.Trim(),
                Capacity = capacity,
                Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
                Status = TableState.Available
            });
            db.SaveChanges();

            TempData["Info"] = $"Table '{tableName.Trim()}' created.";
            return RedirectToAction("Tables");
        }

        // [POST] Edit an existing table's details
        [HttpPost]
        public IActionResult EditTable(int id, string tableName, int capacity, string? location)
        {
            var table = db.Tables.Find(id);
            if (table == null) return NotFound();

            if (string.IsNullOrWhiteSpace(tableName) || capacity < 1)
            {
                TempData["Error"] = "Please provide a table name and a capacity of at least 1.";
                return RedirectToAction("Tables");
            }

            table.TableName = tableName.Trim();
            table.Capacity = capacity;
            table.Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
            db.SaveChanges();

            TempData["Info"] = $"Table '{table.TableName}' updated.";
            return RedirectToAction("Tables");
        }

        // [POST] Delete a table that has no open reservations
        [HttpPost]
        public IActionResult DeleteTable(int id)
        {
            var table = db.Tables.Find(id);
            if (table == null) return NotFound();

            if (table.IsOccupied)
            {
                TempData["Error"] = $"{table.TableName} is currently occupied and cannot be deleted.";
                return RedirectToAction("Tables");
            }

            var hasActiveReservations = db.Reservations.Any(r =>
                r.TableId == id &&
                (r.Status == RestaurantSystem.Models.ReservationStatus.Pending
                 || r.Status == RestaurantSystem.Models.ReservationStatus.Confirmed));

            if (hasActiveReservations)
            {
                TempData["Error"] = $"{table.TableName} still has active reservations. Cancel them first.";
                return RedirectToAction("Tables");
            }

            if (db.Orders.Any(o => o.TableId == id))
            {
                TempData["Error"] = $"{table.TableName} appears in past orders and cannot be deleted. Mark it Unavailable instead.";
                return RedirectToAction("Tables");
            }

            db.Tables.Remove(table);
            db.SaveChanges();

            TempData["Info"] = $"Table '{table.TableName}' deleted.";
            return RedirectToAction("Tables");
        }

        // ==========================================
        // [Payments] Payment management (Workflow.md §3.9, read-only view + receipts)
        // ==========================================
        [HttpGet]
        public IActionResult Payments(string? status)
        {
            ViewBag.ActiveMenu = "Payments";

            IQueryable<RestaurantSystem.Models.Payment> query = db.Payments;

            if (!string.IsNullOrEmpty(status) && status != "ALL")
            {
                query = query.Where(p => p.Status == status);
            }

            var payments = query.OrderByDescending(p => p.CreatedDate).ToList();

            var userIds = db.Orders
                .Where(o => payments.Select(p => p.OrderId).Contains(o.OrderId))
                .ToDictionary(o => o.OrderId, o => o.UserId);

            var users = db.Users.ToDictionary(u => u.UserId, u => u.Username);

            var rows = payments.Select(p => new PaymentRowVM
            {
                PaymentId = p.PaymentId,
                OrderId = p.OrderId,
                Method = p.Method,
                Amount = p.Amount,
                Status = p.Status,
                Reference = p.Reference,
                CreatedDate = p.CreatedDate,
                PaidDate = p.PaidDate,
                CustomerName = userIds.TryGetValue(p.OrderId, out var uid) && users.TryGetValue(uid, out var un)
                    ? un : "Unknown"
            }).ToList();

            ViewBag.SelectedStatus = string.IsNullOrEmpty(status) ? "ALL" : status;
            ViewBag.TotalPaid = payments.Where(p => p.Status == RestaurantSystem.Models.PaymentStatus.Paid).Sum(p => p.Amount);
            ViewBag.TotalUnpaid = payments.Where(p => p.Status == RestaurantSystem.Models.PaymentStatus.Unpaid).Sum(p => p.Amount);
            ViewBag.TotalRefunded = payments.Where(p => p.Status == RestaurantSystem.Models.PaymentStatus.Refunded).Sum(p => p.Amount);

            return View(rows);
        }

        // [POST] Mark a cash payment as settled
        [HttpPost]
        public IActionResult MarkPaymentPaid(int id)
        {
            var payment = db.Payments.Find(id);
            if (payment == null) return NotFound();

            if (payment.Status != RestaurantSystem.Models.PaymentStatus.Unpaid)
            {
                TempData["Error"] = "Only an unpaid payment can be settled.";
                return RedirectToAction("Payments");
            }

            payment.Status = RestaurantSystem.Models.PaymentStatus.Paid;
            payment.PaidDate = DateTime.Now;
            db.SaveChanges();

            TempData["Info"] = $"Payment #{payment.PaymentId} marked as paid.";
            return RedirectToAction("Payments");
        }

        // ==========================================
        // [GET: Admin/ExportOrdersPdf] Export filtered orders as PDF
        // ==========================================
        [HttpGet]
        public IActionResult ExportOrdersPdf(string? status, string? search)
        {
            IQueryable<Order> query = db.Orders;

            if (!string.IsNullOrEmpty(status) && status != "ALL")
            {
                query = query.Where(o => o.OrderStatus == status);
            }

            var orders = query.OrderByDescending(o => o.OrderDate).ToList();

            var userIds = orders.Select(o => o.UserId).Distinct().ToList();
            var tableIds = orders.Where(o => o.TableId.HasValue).Select(o => o.TableId!.Value).Distinct().ToList();

            var users = db.Users.Where(u => userIds.Contains(u.UserId)).ToDictionary(u => u.UserId);
            var tables = db.Tables.Where(t => tableIds.Contains(t.TableId)).ToDictionary(t => t.TableId);

            var orderVMs = orders.Select(o => new OrderVM
            {
                OrderId = o.OrderId,
                CustomerName = users.TryGetValue(o.UserId, out var user) ? user.Username : "Unknown",
                TableName = o.TableId.HasValue && tables.TryGetValue(o.TableId.Value, out var tbl) ? tbl.TableName : null,
                OrderDate = o.OrderDate,
                TotalAmount = o.TotalAmount,
                OrderStatus = o.OrderStatus
            }).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                orderVMs = orderVMs.Where(o =>
                    o.OrderId.ToString().Contains(search) ||
                    o.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (o.TableName != null && o.TableName.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            string statusLabel = string.IsNullOrEmpty(status) || status == "ALL" ? "All" : status;
            var now = DateTime.Now;

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(30);
                    page.MarginVertical(30);

                    page.Content().Column(col =>
                    {
                        col.Item().Text("Order Management Report")
                            .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

                        col.Item().PaddingTop(2).Text($"Status: {statusLabel} | Generated: {now:dd MMM yyyy, HH:mm}")
                            .FontSize(10).FontColor(Colors.Grey.Medium);

                        if (!string.IsNullOrWhiteSpace(search))
                        {
                            col.Item().Text($"Search: \"{search}\"")
                                .FontSize(10).FontColor(Colors.Grey.Medium);
                        }

                        col.Item().PaddingVertical(8);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Total Orders").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{orderVMs.Count}").FontSize(16).Bold();
                            });
                            row.ConstantItem(10);
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Total Revenue").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"RM {orderVMs.Sum(o => o.TotalAmount):N2}").FontSize(16).Bold();
                            });
                            row.ConstantItem(10);
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Filter").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text(statusLabel).FontSize(16).Bold();
                            });
                        });

                        col.Item().PaddingVertical(8);

                        if (orderVMs.Count > 0)
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("#").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Order ID").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Customer").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Table").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Date").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignRight().Text("Total").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Status").FontColor(Colors.White).Bold();
                                });

                                for (int i = 0; i < orderVMs.Count; i++)
                                {
                                    var o = orderVMs[i];
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text($"{i + 1}");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text($"#{o.OrderId}");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(o.CustomerName);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(o.TableName ?? "Takeaway");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(o.OrderDate.ToString("dd MMM yyyy, HH:mm"));
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .AlignRight().Text($"RM {o.TotalAmount:N2}");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(o.OrderStatus);
                                }
                            });

                            col.Item().Background(Colors.Grey.Darken3).Padding(6).Row(row =>
                            {
                                row.RelativeItem().Text($"Total ({orderVMs.Count} orders)").FontColor(Colors.White).Bold();
                                row.RelativeItem().AlignRight().Text($"RM {orderVMs.Sum(o => o.TotalAmount):N2}").FontColor(Colors.White).Bold();
                            });
                        }
                        else
                        {
                            col.Item().Text("No orders found matching the current filters.").Italic().FontColor(Colors.Grey.Medium);
                        }
                    });

                    page.Footer().AlignCenter().Text("FFG Restaurant — Order Management Report")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });

            var pdfBytes = doc.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Orders_Report_{now:yyyyMMdd_HHmmss}.pdf");
        }

        // ==========================================
        // [GET: Admin/ExportUsersPdf] Export filtered users as PDF
        // ==========================================
        [HttpGet]
        public IActionResult ExportUsersPdf(string? role, string? search)
        {
            IQueryable<User> query = db.Users;

            if (!string.IsNullOrEmpty(role) && role != "ALL")
            {
                query = query.Where(u => u.Role == role);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(u =>
                    u.Username.Contains(search) ||
                    u.FullName.Contains(search) ||
                    u.Email.Contains(search) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
            }

            var users = query.OrderBy(u => u.Role).ThenBy(u => u.Username).ToList();

            string roleLabel = string.IsNullOrEmpty(role) || role == "ALL" ? "All" : role;
            var now = DateTime.Now;
            int totalMembers = users.Count(u => u.Role == "MEMBER");
            int totalAdmins = users.Count(u => u.Role == "ADMIN");

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(30);
                    page.MarginVertical(30);

                    page.Content().Column(col =>
                    {
                        col.Item().Text("User Management Report")
                            .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

                        col.Item().PaddingTop(2).Text($"Role: {roleLabel} | Generated: {now:dd MMM yyyy, HH:mm}")
                            .FontSize(10).FontColor(Colors.Grey.Medium);

                        if (!string.IsNullOrWhiteSpace(search))
                        {
                            col.Item().Text($"Search: \"{search}\"")
                                .FontSize(10).FontColor(Colors.Grey.Medium);
                        }

                        col.Item().PaddingVertical(8);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Total Users").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{users.Count}").FontSize(16).Bold();
                            });
                            row.ConstantItem(10);
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Members").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{totalMembers}").FontSize(16).Bold();
                            });
                            row.ConstantItem(10);
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Admins").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{totalAdmins}").FontSize(16).Bold();
                            });
                        });

                        col.Item().PaddingVertical(8);

                        if (users.Count > 0)
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(1.5f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("#").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Username").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Full Name").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Email").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Phone").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Role").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Status").FontColor(Colors.White).Bold();
                                });

                                for (int i = 0; i < users.Count; i++)
                                {
                                    var u = users[i];
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text($"{i + 1}");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(u.Username);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(u.FullName);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(u.Email);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(u.PhoneNumber ?? "-");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(u.Role == "ADMIN" ? "Admin" : "Member");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(u.IsBlocked ? "Blocked" : "Active");
                                }
                            });

                            col.Item().Background(Colors.Grey.Darken3).Padding(6).Row(row =>
                            {
                                row.RelativeItem().Text($"Total: {users.Count} user(s)").FontColor(Colors.White).Bold();
                            });
                        }
                        else
                        {
                            col.Item().Text("No users found matching the current filters.").Italic().FontColor(Colors.Grey.Medium);
                        }
                    });

                    page.Footer().AlignCenter().Text("FFG Restaurant — User Management Report")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });

            var pdfBytes = doc.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Users_Report_{now:yyyyMMdd_HHmmss}.pdf");
        }

        // ==========================================
        // [GET: Admin/UserManage] User management list (role filter + search)
        // ==========================================
        public IActionResult UserManage(string? role, string? search)
        {
            IQueryable<User> query = db.Users;

            // Role filter: ALL / MEMBER / ADMIN
            if (!string.IsNullOrEmpty(role) && role != "ALL")
            {
                query = query.Where(u => u.Role == role);
            }

            // Keyword search: username / full name / email / phone
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(u =>
                    u.Username.Contains(search) ||
                    u.FullName.Contains(search) ||
                    u.Email.Contains(search) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
            }

            ViewBag.SelectedRole = string.IsNullOrEmpty(role) ? "ALL" : role;
            ViewBag.Search = search;

            return View(query.OrderBy(u => u.Role).ThenBy(u => u.Username).ToList());
        }

        // ==========================================
        // [GET: Admin/UserCreate] New user form
        // ==========================================
        [HttpGet]
        public IActionResult UserCreate()
        {
            return View(new UserVM { Username = "", Email = "", FullName = "" });
        }

        // ==========================================
        // [POST: Admin/UserCreate] Submit new user
        // ==========================================
        [HttpPost]
        public IActionResult UserCreate(UserVM vm)
        {
            if (ModelState.IsValid)
            {
                // Password is required when creating
                if (string.IsNullOrWhiteSpace(vm.Password))
                {
                    ModelState.AddModelError("Password", "Please enter password for the new user.");
                    return View(vm);
                }

                // Uniqueness checks
                if (db.Users.Any(u => u.Username == vm.Username))
                {
                    ModelState.AddModelError("Username", "This username is already taken.");
                    return View(vm);
                }
                if (db.Users.Any(u => u.Email == vm.Email))
                {
                    ModelState.AddModelError("Email", "This email address is already registered.");
                    return View(vm);
                }

                var user = new User
                {
                    Username = vm.Username,
                    Email = vm.Email,
                    FullName = vm.FullName,
                    PhoneNumber = vm.PhoneNumber ?? "",
                    Address = vm.Address,
                    Role = vm.Role,
                    IsBlocked = false,
                    Password = hp.HashPassword(vm.Password)
                };

                db.Users.Add(user);
                db.SaveChanges();

                TempData["Info"] = $"User '{user.Username}' created successfully.";
                return RedirectToAction("UserManage");
            }
            return View(vm);
        }

        // ==========================================
        // [GET: Admin/UserEdit] Edit user form
        // ==========================================
        [HttpGet]
        public IActionResult UserEdit(int id)
        {
            var user = db.Users.Find(id);
            if (user == null)
            {
                return NotFound();
            }

            var vm = new UserVM
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                Role = user.Role,
                IsBlocked = user.IsBlocked
            };
            return View(vm);
        }

        // ==========================================
        // [POST: Admin/UserEdit] Submit edited user
        // ==========================================
        [HttpPost]
        public IActionResult UserEdit(UserVM vm)
        {
            var user = db.Users.Find(vm.UserId);
            if (user == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Uniqueness checks (excluding self)
                if (db.Users.Any(u => u.Username == vm.Username && u.UserId != vm.UserId))
                {
                    ModelState.AddModelError("Username", "This username is already taken.");
                    return View(vm);
                }
                if (db.Users.Any(u => u.Email == vm.Email && u.UserId != vm.UserId))
                {
                    ModelState.AddModelError("Email", "This email address is already registered.");
                    return View(vm);
                }

                user.Username = vm.Username;
                user.Email = vm.Email;
                user.FullName = vm.FullName;
                user.PhoneNumber = vm.PhoneNumber ?? "";
                user.Address = vm.Address;
                user.Role = vm.Role;

                // Blank password = don't change it
                if (!string.IsNullOrWhiteSpace(vm.Password))
                {
                    user.Password = hp.HashPassword(vm.Password);
                }

                db.SaveChanges();
                TempData["Info"] = $"User '{user.Username}' updated successfully.";
                return RedirectToAction("UserManage");
            }
            return View(vm);
        }

        // ==========================================
        // [POST: Admin/ToggleBlock] Block / unblock a MEMBER account
        // ==========================================
        [HttpPost]
        public IActionResult ToggleBlock(int id)
        {
            var user = db.Users.Find(id);
            if (user == null)
            {
                return NotFound();
            }

            // Only MEMBER accounts can be blocked — ADMIN accounts are protected
            if (user.Role != "MEMBER")
            {
                TempData["Error"] = "Only member accounts can be blocked or unblocked.";
                return RedirectToAction("UserManage");
            }

            user.IsBlocked = !user.IsBlocked;
            db.SaveChanges();

            TempData["Info"] = user.IsBlocked
                ? $"Member '{user.Username}' has been blocked."
                : $"Member '{user.Username}' has been unblocked.";

            return RedirectToAction("UserManage");
        }

        // ==========================================
        // [POST: Admin/DeleteUser] Delete a user
        // ==========================================
        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            var user = db.Users.Find(id);
            if (user == null)
            {
                return NotFound();
            }

            // Prevent an admin from accidentally deleting their own account
            if (user.Username == User.Identity?.Name)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction("UserManage");
            }

            if (db.Orders.Any(o => o.UserId == id))
            {
                TempData["Error"] = $"Cannot delete '{user.Username}' — they have existing orders. Block the account instead.";
                return RedirectToAction("UserManage");
            }

            db.Users.Remove(user);
            db.SaveChanges();
            TempData["Info"] = $"User '{user.Username}' has been deleted.";
            return RedirectToAction("UserManage");
        }

        // ==========================================
        // [Report Module] — lives inside AdminController on purpose, so the
        // URLs come out as /Admin/Report/... via attribute routing below,
        // instead of needing a separate ReportController.
        // ==========================================

        // Landing page for the Reports section — links out to Daily / Monthly / Top-Selling
        [Route("Admin/Report")]
        public IActionResult Report()
        {
            return View();
        }

        private Dictionary<int, string> ProductNames()
        {
            return db.Products.ToDictionary(p => p.ProductId, p => p.ProductName);
        }

        // ---------- Daily Sales Report ----------
        [Route("Admin/Report/Daily")]
        public IActionResult Daily(DateTime? date)
        {
            DateTime selectedDate = (date ?? DateTime.Today).Date;

            var orders = db.Orders
                .Where(o => o.OrderDate.Date == selectedDate && o.OrderStatus != "CANCELLED")
                .ToList();

            var names = ProductNames();

            var details = db.OrderDetails
                .Where(d => orders.Select(o => o.OrderId).Contains(d.OrderId))
                .ToList();

            var vm = new DailyReportVM
            {
                Date = selectedDate,
                TotalOrders = orders.Count,
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                Items = details
                    .GroupBy(d => d.ProductId)
                    .Select(g => new ReportItemVM
                    {
                        ProductId = g.Key,
                        ProductName = names.TryGetValue(g.Key, out var n) ? n : "Unknown",
                        Quantity = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.Quantity * x.PriceAtOrder)
                    })
                    .OrderByDescending(x => x.Quantity)
                    .ToList()
            };
            vm.AvgOrderValue = vm.TotalOrders > 0 ? vm.TotalRevenue / vm.TotalOrders : 0;

            ViewBag.PrevDate = selectedDate.AddDays(-1);
            ViewBag.NextDate = selectedDate.AddDays(1);
            ViewBag.Today = DateTime.Today;
            return View(vm);
        }

        [Route("Admin/Report/Daily/Export")]
        public IActionResult ExportDailyPdf(DateTime? date)
        {
            DateTime selectedDate = (date ?? DateTime.Today).Date;

            var orders = db.Orders
                .Where(o => o.OrderDate.Date == selectedDate && o.OrderStatus != "CANCELLED")
                .ToList();

            var names = ProductNames();

            var details = db.OrderDetails
                .Where(d => orders.Select(o => o.OrderId).Contains(d.OrderId))
                .ToList();

            var totalOrders = orders.Count;
            var totalRevenue = orders.Sum(o => o.TotalAmount);
            var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

            var items = details
                .GroupBy(d => d.ProductId)
                .Select(g => new ReportItemVM
                {
                    ProductId = g.Key,
                    ProductName = names.TryGetValue(g.Key, out var n) ? n : "Unknown",
                    Quantity = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.Quantity * x.PriceAtOrder)
                })
                .OrderByDescending(x => x.Quantity)
                .ToList();

            var dateStr = selectedDate.ToString("MMMM dd, yyyy");

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(30);
                    page.MarginVertical(30);

                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Daily Sales Report — {dateStr}")
                            .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

                        col.Item().PaddingVertical(5);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Total Revenue").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"RM {totalRevenue:N2}").FontSize(16).Bold();
                            });
                            row.ConstantItem(10);
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Total Orders").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{totalOrders}").FontSize(16).Bold();
                            });
                            row.ConstantItem(10);
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Avg. Order Value").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"RM {avgOrderValue:N2}").FontSize(16).Bold();
                            });
                        });

                        col.Item().PaddingVertical(10);

                        col.Item().Text($"Items Sold ({items.Count})").FontSize(14).Bold().FontColor(Colors.Grey.Darken3);
                        col.Item().PaddingBottom(5);

                        if (items.Count > 0)
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("#").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Product").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignCenter().Text("Qty").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignRight().Text("Revenue").FontColor(Colors.White).Bold();
                                });

                                for (int i = 0; i < items.Count; i++)
                                {
                                    var item = items[i];
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text($"{i + 1}");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(item.ProductName);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .AlignCenter().Text($"{item.Quantity}");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .AlignRight().Text($"RM {item.Revenue:N2}");
                                }
                            });

                            col.Item().Background(Colors.Grey.Darken3).Padding(6).Row(row =>
                            {
                                row.RelativeItem().Text("Total").FontColor(Colors.White).Bold();
                                row.RelativeItem().AlignCenter().Text($"{items.Sum(x => x.Quantity)}").FontColor(Colors.White).Bold();
                                row.RelativeItem().AlignRight().Text($"RM {items.Sum(x => x.Revenue):N2}").FontColor(Colors.White).Bold();
                            });
                        }
                        else
                        {
                            col.Item().Text("No sales recorded for this day.").Italic().FontColor(Colors.Grey.Medium);
                        }
                    });
                });
            });

            var pdfBytes = doc.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"DailyReport_{selectedDate:yyyy-MM-dd}.pdf");
        }

        // ---------- Monthly Sales Report ----------
        [Route("Admin/Report/Monthly")]
        public IActionResult Monthly(int? year, int? month)
        {
            int y = year ?? DateTime.Today.Year;
            int m = month ?? DateTime.Today.Month;

            var orders = db.Orders
                .Where(o => o.OrderDate.Year == y && o.OrderDate.Month == m && o.OrderStatus != "CANCELLED")
                .ToList();

            var names = ProductNames();

            var details = db.OrderDetails
                .Where(d => orders.Select(o => o.OrderId).Contains(d.OrderId))
                .ToList();

            var vm = new MonthlyReportVM
            {
                Year = y,
                Month = m,
                MonthName = new DateTime(y, m, 1).ToString("MMMM yyyy"),
                TotalOrders = orders.Count,
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                DailySummaries = orders
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new DaySummaryVM
                    {
                        Day = g.Key,
                        Orders = g.Count(),
                        Revenue = g.Sum(o => o.TotalAmount)
                    })
                    .OrderBy(x => x.Day)
                    .ToList(),
                TopItems = details
                    .GroupBy(d => d.ProductId)
                    .Select(g => new ReportItemVM
                    {
                        ProductId = g.Key,
                        ProductName = names.TryGetValue(g.Key, out var n) ? n : "Unknown",
                        Quantity = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.Quantity * x.PriceAtOrder)
                    })
                    .OrderByDescending(x => x.Quantity)
                    .Take(10)
                    .ToList()
            };

            ViewBag.PrevMonth = new DateTime(y, m, 1).AddMonths(-1);
            ViewBag.NextMonth = new DateTime(y, m, 1).AddMonths(1);
            ViewBag.Today = DateTime.Today;
            return View(vm);
        }

        [Route("Admin/Report/Monthly/Export")]
        public IActionResult ExportMonthlyPdf(int? year, int? month)
        {
            int y = year ?? DateTime.Today.Year;
            int m = month ?? DateTime.Today.Month;

            var orders = db.Orders
                .Where(o => o.OrderDate.Year == y && o.OrderDate.Month == m && o.OrderStatus != "CANCELLED")
                .ToList();

            var names = ProductNames();

            var details = db.OrderDetails
                .Where(d => orders.Select(o => o.OrderId).Contains(d.OrderId))
                .ToList();

            var monthName = new DateTime(y, m, 1).ToString("MMMM yyyy");
            var totalOrders = orders.Count;
            var totalRevenue = orders.Sum(o => o.TotalAmount);

            var dailySummaries = orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new DaySummaryVM
                {
                    Day = g.Key,
                    Orders = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(x => x.Day)
                .ToList();

            var topItems = details
                .GroupBy(d => d.ProductId)
                .Select(g => new ReportItemVM
                {
                    ProductId = g.Key,
                    ProductName = names.TryGetValue(g.Key, out var n) ? n : "Unknown",
                    Quantity = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.Quantity * x.PriceAtOrder)
                })
                .OrderByDescending(x => x.Quantity)
                .Take(10)
                .ToList();

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(30);
                    page.MarginVertical(30);

                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Monthly Sales Report — {monthName}")
                            .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

                        col.Item().PaddingVertical(5);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Total Revenue").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"RM {totalRevenue:N2}").FontSize(16).Bold();
                            });
                            row.ConstantItem(10);
                            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                            {
                                c.Item().Text("Total Orders").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{totalOrders}").FontSize(16).Bold();
                            });
                        });

                        col.Item().PaddingVertical(10);

                        col.Item().Text("Daily Breakdown").FontSize(14).Bold().FontColor(Colors.Grey.Darken3);
                        col.Item().PaddingBottom(5);

                        if (dailySummaries.Count > 0)
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Day").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignCenter().Text("Orders").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignRight().Text("Revenue").FontColor(Colors.White).Bold();
                                });

                                foreach (var d in dailySummaries)
                                {
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(d.Day.ToString("dddd, MMM dd"));
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .AlignCenter().Text($"{d.Orders}");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .AlignRight().Text($"RM {d.Revenue:N2}");
                                }
                            });
                        }
                        else
                        {
                            col.Item().PaddingBottom(10).Text("No sales recorded for this month.").Italic().FontColor(Colors.Grey.Medium);
                        }

                        col.Item().PaddingVertical(10);

                        col.Item().Text("Top 10 Items").FontSize(14).Bold().FontColor(Colors.Grey.Darken3);
                        col.Item().PaddingBottom(5);

                        if (topItems.Count > 0)
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("#").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).Text("Product").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignCenter().Text("Qty").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignRight().Text("Revenue").FontColor(Colors.White).Bold();
                                });

                                for (int i = 0; i < topItems.Count; i++)
                                {
                                    var item = topItems[i];
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text($"{i + 1}");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .Text(item.ProductName);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .AlignCenter().Text($"{item.Quantity}");
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                                        .AlignRight().Text($"RM {item.Revenue:N2}");
                                }
                            });
                        }
                        else
                        {
                            col.Item().Text("No items sold this month.").Italic().FontColor(Colors.Grey.Medium);
                        }
                    });
                });
            });

            var pdfBytes = doc.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"MonthlyReport_{y}_{m:D2}.pdf");
        }

        // ---------- Top-Selling Foods ----------
        [Route("Admin/Report/TopSelling")]
        public IActionResult TopSelling(DateTime? from, DateTime? to)
        {
            DateTime f = (from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
            DateTime t = (to ?? DateTime.Today).Date;

            var orders = db.Orders
                .Where(o => o.OrderDate.Date >= f && o.OrderDate.Date <= t && o.OrderStatus != "CANCELLED")
                .ToList();

            var names = ProductNames();

            var details = db.OrderDetails
                .Where(d => orders.Select(o => o.OrderId).Contains(d.OrderId))
                .ToList();

            var vm = new TopSellingVM
            {
                From = f,
                To = t,
                TotalOrders = orders.Count,
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                Items = details
                    .GroupBy(d => d.ProductId)
                    .Select(g => new ReportItemVM
                    {
                        ProductId = g.Key,
                        ProductName = names.TryGetValue(g.Key, out var n) ? n : "Unknown",
                        Quantity = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.Quantity * x.PriceAtOrder)
                    })
                    .OrderByDescending(x => x.Quantity)
                    .ThenByDescending(x => x.Revenue)
                    .ToList()
            };

            return View(vm);
        }
    }
}