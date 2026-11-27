using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSystem.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using static RestaurantSystem.Models.ViewModels;

namespace RestaurantSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly RestaurantDbContext _db;
        private const string CART_KEY = "OrderingCart"; // Must match the constant used in OrderController

        public HomeController(RestaurantDbContext db)
        {
            _db = db;
        }

        // ==========================================
        // Home Page: hero banner + Top Selling carousel
        // ==========================================
        // Home/Index
        public IActionResult Index()
        {
            var topSelling = (from od in _db.OrderDetails
                              join p in _db.Products on od.ProductId equals p.ProductId
                              join c in _db.Categories on p.CategoryId equals c.CategoryId
                              group new { od } by new
                              {
                                  p.ProductId,
                                  p.ProductName,
                                  p.ImageUrl,
                                  p.CategoryId,
                                  CategoryName = c.CategoryName
                              }
                               into g
                              orderby g.Sum(x => x.od.Quantity) descending
                              select new TopSellingItemVM
                              {
                                  ProductId = g.Key.ProductId,
                                  ProductName = g.Key.ProductName,
                                  ImageUrl = g.Key.ImageUrl,
                                  CategoryId = g.Key.CategoryId,
                                  CategoryName = g.Key.CategoryName,
                                  TotalSold = g.Sum(x => x.od.Quantity)
                              })
                               .Take(5)
                               .ToList();

            return View(topSelling);
        }

        // ==========================================
        // Menu Page: Fetch products from DB
        // ==========================================
        // Home/Menu
        public IActionResult Menu()
        {
            // The product grid itself is now fully AJAX-driven (see SearchProducts below),
            // so this action just returns the page shell — but the category filter pills
            // are rendered from the real Categories table, so they stay correct on any
            // machine/database instead of relying on hard-coded ids.
            ViewBag.Categories = _db.Categories
                .OrderBy(c => c.CategoryId)
                .ToList();

            return View();
        }

        // ==========================================
        // [GET] AJAX: search / filter / sort / paginate the product catalog.
        // Used by the Menu page's search box, category pills, price range,
        // sort dropdown, and pagination controls — all funnel through this
        // single endpoint instead of separate client-side tricks.
        // ==========================================
        [HttpGet]
        public IActionResult SearchProducts(string? search, int? categoryId, decimal? minPrice, decimal? maxPrice, string sortBy = "name_asc", int page = 1)
        {
            const int pageSize = 9;
            if (page < 1) page = 1;

            // Deactivated dishes (Admin → Menu Management) stay out of the customer catalogue.
            var query = _db.Products.Where(p => p.IsActive).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.ProductName.Contains(search));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            query = sortBy switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "name_desc" => query.OrderByDescending(p => p.ProductName),
                _ => query.OrderBy(p => p.ProductName) // "name_asc" and any unrecognised value
            };

            int totalCount = query.Count();
            int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page > totalPages) page = totalPages;

            var pagedProducts = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var userId = GetCurrentUserId();
            HashSet<int> favoriteIds = userId.HasValue
                ? _db.Favorites.Where(f => f.UserId == userId.Value).Select(f => f.ProductId).ToHashSet()
                : new HashSet<int>();

            var categoryNames = _db.Categories.ToDictionary(c => c.CategoryId, c => c.CategoryName);

            var products = pagedProducts.Select(p => new
            {
                productId = p.ProductId,
                productName = p.ProductName,
                description = p.Description,
                price = p.Price,
                imageUrl = p.ImageUrl,
                categoryId = p.CategoryId,
                categoryName = categoryNames.TryGetValue(p.CategoryId, out var name) ? name : "Other",
                isFavorited = favoriteIds.Contains(p.ProductId),
                stockQuantity = p.StockQuantity
            });

            return Json(new
            {
                products,
                totalCount,
                totalPages,
                currentPage = page
            });
        }

        // ==========================================
        // Product Detail Page: full info for a single product
        // ==========================================
        // Home/ProductDetail/{id}
        public IActionResult ProductDetail(int id)
        {
            var product = _db.Products.FirstOrDefault(p => p.ProductId == id);
            if (product == null)
            {
                return NotFound();
            }

            var category = _db.Categories.FirstOrDefault(c => c.CategoryId == product.CategoryId);
            ViewBag.CategoryName = category?.CategoryName ?? "Uncategorized";

            var userId = GetCurrentUserId();
            ViewBag.IsFavorited = userId.HasValue
                && _db.Favorites.Any(f => f.UserId == userId.Value && f.ProductId == id);

            // Customisation options offered for this dish (Workflow.md §2.3)
            ViewBag.Addons = _db.ProductAddons
                .Where(a => a.ProductId == id && a.IsActive)
                .OrderBy(a => a.AddonId)
                .ToList();

            ViewBag.RelatedProducts = _db.Products
                .Where(p => p.CategoryId == product.CategoryId && p.ProductId != product.ProductId && p.IsActive)
                .Take(3)
                .ToList();

            return View(product);
        }

        // Tables Page: Load table occupancy status from DB
        // Home/Tables
        public IActionResult Tables()
        {
            var restaurantTables = _db.Tables.OrderBy(t => t.TableId).ToList();
            return View(restaurantTables);
        }

        // [POST] Admin/counter staff manually frees up a table (e.g. customer forgot
        // to check out, or paid at the counter without going through My Orders)
        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        public IActionResult AdminFreeTable(int tableId)
        {
            var table = _db.Tables.FirstOrDefault(t => t.TableId == tableId);
            if (table != null)
            {
                table.SetStatus(TableState.Available);
                _db.SaveChanges();
                TempData["Info"] = $"{table.TableName} has been marked as available.";
            }

            return RedirectToAction("Tables");
        }

        // ==========================================
        // Cart feature (merged in from the old CartController)
        // ==========================================

        // [GET] View the cart page — Home/Cart
        [Authorize]
        public IActionResult Cart()
        {
            var cart = GetCartFromSession();
            return View(cart);
        }

        // [POST] Add a dish to the cart (traditional synchronous form)
        [HttpPost]
        [Authorize]
        public IActionResult Add(int productId, int quantity = 1, int[]? addonIds = null, string? instructions = null)
        {
            var product = _db.Products.FirstOrDefault(p => p.ProductId == productId);
            if (product == null)
            {
                return NotFound();
            }

            if (product.StockQuantity <= 0)
            {
                TempData["Info"] = $"Sorry, {product.ProductName} is currently out of stock.";
                return RedirectToAction("Index", "Home");
            }

            var cart = GetCartFromSession();
            var addons = BuildAddons(productId, addonIds);
            var note = NormaliseInstructions(instructions);

            // Lines with different add-ons / instructions are separate cart entries, so the
            // customer can order the same dish twice with different customisations.
            var cartItem = FindMatchingLine(cart, productId, addons, note);
            int alreadyInCart = cart.Sum(i => i.Quantity);

            if (alreadyInCart + quantity > product.StockQuantity)
            {
                quantity = product.StockQuantity - alreadyInCart;
            }

            if (quantity <= 0)
            {
                TempData["Info"] = $"You already have the maximum available stock of {product.ProductName} in your cart.";
                return RedirectToAction("Index", "Home");
            }

            if (cartItem != null)
            {
                cartItem.Quantity += quantity;
            }
            else
            {
                cart.Add(new CartItemVM
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    Price = product.Price,
                    Quantity = quantity,
                    Addons = addons,
                    SpecialInstructions = note
                });
            }

            SaveCartToSession(cart);
            TempData["Info"] = $"{product.ProductName} added to cart!";
            return RedirectToAction("Index", "Home");
        }

        // [POST] Add a dish to the cart via AJAX (used by Menu / Product Detail pages)
        [HttpPost]
        [Authorize]
        public IActionResult AddAjax(int productId, int quantity = 1, int[]? addonIds = null, string? instructions = null)
        {
            var product = _db.Products.FirstOrDefault(p => p.ProductId == productId);
            if (product == null)
            {
                return Json(new { success = false, message = "Dish not found." });
            }

            if (product.StockQuantity <= 0)
            {
                return Json(new { success = false, message = $"Sorry, {product.ProductName} is currently out of stock." });
            }

            var cart = GetCartFromSession();
            var addons = BuildAddons(productId, addonIds);
            var note = NormaliseInstructions(instructions);

            var cartItem = FindMatchingLine(cart, productId, addons, note);
            int alreadyInCart = cart.Sum(i => i.Quantity);

            bool cappedToStock = false;
            if (alreadyInCart + quantity > product.StockQuantity)
            {
                quantity = product.StockQuantity - alreadyInCart;
                cappedToStock = true;
            }

            if (quantity <= 0)
            {
                return Json(new { success = false, message = $"You already have the maximum available stock of {product.ProductName} in your cart." });
            }

            if (cartItem != null)
            {
                cartItem.Quantity += quantity;
            }
            else
            {
                cart.Add(new CartItemVM
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    Price = product.Price,
                    Quantity = quantity,
                    Addons = addons,
                    SpecialInstructions = note
                });
            }

            SaveCartToSession(cart);
            int totalCount = cart.Sum(item => item.Quantity);

            return Json(new
            {
                success = true,
                cartCount = totalCount,
                productName = product.ProductName,
                cappedToStock
            });
        }

        // [POST] Update the quantity of a dish already in the cart
        [HttpPost]
        [Authorize]
        public IActionResult Update(int productId, int quantity)
        {
            var cart = GetCartFromSession();
            var cartItem = cart.FirstOrDefault(item => item.ProductId == productId);

            if (cartItem != null)
            {
                if (quantity > 0)
                {
                    cartItem.Quantity = quantity;
                    TempData["Info"] = "Cart updated.";
                }
                else
                {
                    cart.Remove(cartItem);
                    TempData["Info"] = "Item removed from cart.";
                }
            }

            SaveCartToSession(cart);
            return RedirectToAction("Cart");
        }

        // [POST] Remove a dish from the cart entirely
        [HttpPost]
        [Authorize]
        public IActionResult Remove(int productId)
        {
            var cart = GetCartFromSession();
            var cartItem = cart.FirstOrDefault(item => item.ProductId == productId);

            if (cartItem != null)
            {
                cart.Remove(cartItem);
                TempData["Info"] = "Item removed from cart.";
            }

            SaveCartToSession(cart);
            return RedirectToAction("Cart");
        }

        // ==========================================
        // Favorite feature: view / add / remove / transfer to cart
        // ==========================================

        // [GET] View my favorites list
        [Authorize]
        public IActionResult Favorites()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var favoriteProducts = (from f in _db.Favorites
                                    join p in _db.Products on f.ProductId equals p.ProductId
                                    where f.UserId == userId.Value
                                    orderby f.AddedDate descending
                                    select p).ToList();

            return View(favoriteProducts);
        }

        // [POST] AJAX: add a product to favorites (used by the Menu / Product Detail page)
        [HttpPost]
        [Authorize]
        public IActionResult AddFavoriteAjax(int productId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Json(new { success = false, message = "Please log in first." });
            }

            var product = _db.Products.FirstOrDefault(p => p.ProductId == productId);
            if (product == null)
            {
                return Json(new { success = false, message = "Dish not found." });
            }

            bool alreadyExists = _db.Favorites.Any(f => f.UserId == userId.Value && f.ProductId == productId);
            if (alreadyExists)
            {
                return Json(new { success = false, message = $"{product.ProductName} is already in your favorites." });
            }

            _db.Favorites.Add(new Favorite
            {
                UserId = userId.Value,
                ProductId = productId
            });
            _db.SaveChanges();

            int favoriteCount = _db.Favorites.Count(f => f.UserId == userId.Value);

            return Json(new
            {
                success = true,
                favoriteCount,
                productName = product.ProductName
            });
        }

        // [POST] AJAX: remove a single product from favorites (used by the Favorites page)
        [HttpPost]
        [Authorize]
        public IActionResult RemoveFavoriteAjax(int productId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Json(new { success = false, message = "Please log in first." });
            }

            var fav = _db.Favorites.FirstOrDefault(f => f.UserId == userId.Value && f.ProductId == productId);
            if (fav != null)
            {
                _db.Favorites.Remove(fav);
                _db.SaveChanges();
            }

            int favoriteCount = _db.Favorites.Count(f => f.UserId == userId.Value);

            return Json(new
            {
                success = true,
                message = "Removed from favorites.",
                favoriteCount
            });
        }

        // [POST] AJAX: move a single favorite item to cart.
        [HttpPost]
        [Authorize]
        public IActionResult TransferFavoriteToCart(int productId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Json(new { success = false, message = "Please log in first." });
            }

            var product = _db.Products.FirstOrDefault(p => p.ProductId == productId);
            if (product == null)
            {
                return Json(new { success = false, message = "Dish not found." });
            }

            var cart = GetCartFromSession();
            var cartItem = cart.FirstOrDefault(c => c.ProductId == productId);

            if (cartItem != null)
            {
                cartItem.Quantity += 1;
            }
            else
            {
                cart.Add(new CartItemVM
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    Price = product.Price,
                    Quantity = 1
                });
            }
            SaveCartToSession(cart);

            int cartCount = cart.Sum(c => c.Quantity);

            return Json(new
            {
                success = true,
                message = $"{product.ProductName} added to cart!",
                cartCount
            });
        }

        // [POST] AJAX: move ALL favorite items to cart.
        [HttpPost]
        [Authorize]
        public IActionResult TransferAllFavoritesToCart()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Json(new { success = false, message = "Please log in first." });
            }

            var favs = _db.Favorites.Where(f => f.UserId == userId.Value).ToList();
            var cart = GetCartFromSession();

            int movedCount = 0;
            foreach (var fav in favs)
            {
                var product = _db.Products.FirstOrDefault(p => p.ProductId == fav.ProductId);
                if (product == null) continue;

                var cartItem = cart.FirstOrDefault(c => c.ProductId == product.ProductId);
                if (cartItem != null)
                {
                    cartItem.Quantity += 1;
                }
                else
                {
                    cart.Add(new CartItemVM
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        Price = product.Price,
                        Quantity = 1
                    });
                }
                movedCount++;
            }

            SaveCartToSession(cart);

            int cartCount = cart.Sum(c => c.Quantity);

            return Json(new
            {
                success = true,
                message = $"{movedCount} item(s) added to cart!",
                cartCount
            });
        }

        // ==========================================
        // Private helper methods
        // ==========================================

        private int? GetCurrentUserId()
        {
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
            {
                return null;
            }

            var username = User.Identity.Name;
            var user = _db.Users.FirstOrDefault(u => u.Username == username);
            return user?.UserId;
        }

        private List<CartItemVM> GetCartFromSession()
        {
            var sessionData = HttpContext.Session.GetString(CART_KEY);
            if (string.IsNullOrEmpty(sessionData))
            {
                return new List<CartItemVM>();
            }
            return JsonSerializer.Deserialize<List<CartItemVM>>(sessionData) ?? new List<CartItemVM>();
        }

        private void SaveCartToSession(List<CartItemVM> cart)
        {
            var sessionData = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString(CART_KEY, sessionData);
        }

        // ==========================================
        // Customisation helpers (Workflow.md §2.3)
        // ==========================================

        /// <summary>
        /// Resolves the posted add-on ids into a validated snapshot. Prices come from the
        /// database, never from the client, and add-ons that don't belong to this dish (or are
        /// inactive) are silently ignored.
        /// </summary>
        private List<CartAddonVM> BuildAddons(int productId, int[]? addonIds)
        {
            if (addonIds == null || addonIds.Length == 0) return new List<CartAddonVM>();

            return _db.ProductAddons
                .Where(a => addonIds.Contains(a.AddonId) && a.ProductId == productId && a.IsActive)
                .OrderBy(a => a.AddonId)
                .Select(a => new CartAddonVM
                {
                    AddonId = a.AddonId,
                    Name = a.Name,
                    Price = a.Price
                })
                .ToList();
        }

        private static string? NormaliseInstructions(string? instructions)
        {
            if (string.IsNullOrWhiteSpace(instructions)) return null;
            instructions = instructions.Trim();
            return instructions.Length > 200 ? instructions[..200] : instructions;
        }

        /// <summary>
        /// Two cart lines are "the same" only when the dish, its add-ons and its instructions all
        /// match — otherwise ordering the same dish with different extras would merge them.
        /// </summary>
        private static CartItemVM? FindMatchingLine(
            List<CartItemVM> cart, int productId, List<CartAddonVM> addons, string? instructions)
        {
            foreach (var line in cart.Where(l => l.ProductId == productId))
            {
                bool sameAddons =
                    line.Addons.Count == addons.Count &&
                    line.Addons.Select(a => a.AddonId).OrderBy(i => i)
                        .SequenceEqual(addons.Select(a => a.AddonId).OrderBy(i => i));

                if (sameAddons &&
                    string.Equals(line.SpecialInstructions ?? "", instructions ?? "", StringComparison.Ordinal))
                {
                    return line;
                }
            }
            return null;
        }
    }
}