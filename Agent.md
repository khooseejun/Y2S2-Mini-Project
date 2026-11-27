# Agent.md — RestaurantSystem-Final

Guidance for AI coding agents working in this repository. Read this first, then only the files relevant to your task.

> **Portability:** this project is self-contained and runs on any Windows machine with the .NET SDK and
> SQL Server Express LocalDB — no machine-specific paths, no edits needed after copying. See
> [Section 12.1](#121-porting-this-project-to-another-computer) for the checklist and the pitfalls.

---

## 1. What this project is

**FFG Restaurant ("Emerald Kitchen")** — an ASP.NET Core MVC restaurant ordering web app built for the
AMIT2014 assignment. It is a **single-project, server-rendered Razor MVC app** with no Web API layer, no
SPA framework, and no build pipeline beyond `dotnet build`.

Two audiences share one app:

| Audience | Entry point | Access |
| --- | --- | --- |
| Customers (`MEMBER`) | `/` — browse menu, cart, checkout, orders | Cookie-authenticated |
| Administrators (`ADMIN`) | `/Admin/Dashboard` | Cookie-authenticated **and** in the `ADMIN` role |

**Core user journeys**

- Browse menu (AJAX search/filter/sort/paginate) → product detail (choose add-ons + special
  instructions) → favorites → cart → checkout (Dine-in / Takeaway / Delivery) → payment (Cash / E-Wallet
  mock) → confirmation → live order tracking → PDF receipt.
- Reserve a table (date, time, guests, optional table) with automatic double-booking prevention.
- Admin: dashboard, kitchen queue, order management with a 7-state lifecycle, category & product CRUD,
  user management, table management, reservation management, payments, and reports + PDF export.

### Workflow.md coverage

`Workflow.md` (in this folder) is the requirements/design document the project is being built towards.
Current state — **Phase 1 complete, Phase 2 core complete**:

| Workflow.md section | Status |
| --- | --- |
| §2.1 Main page, §2.2 Registration/login, §2.3 Ordering, §2.4 Checkout & payment, §2.6 Order tracking | **Implemented** (3 order types, add-ons, instructions, 7 statuses) |
| §2.5 Table reservation | **Implemented** (conflict prevention per §8.4) |
| §3.1 Login/roles, §3.2 Dashboard, §3.3 Order management (+ kitchen queue) | **Implemented** |
| §3.4 Menu management, §3.5 Table management, §3.6 Reservations | **Implemented** |
| §3.9 Payment management (transactions, receipts) | **Implemented** — refunds are recorded, not initiated |
| §3.10 Reports | **Implemented** (daily/monthly/top-selling + PDF) |
| §3.7 Staff management (multi-role), §3.8 Inventory | **Not implemented** (Phase 3) |
| §5 promotions, notifications, activity log | **Not implemented** (Phase 3) — `Order.DiscountAmount` and `Orders.CancellationReason` exist as hooks |

Roles remain `ADMIN` / `MEMBER`; the richer staff roles (Manager, Cashier, Kitchen, Waiter) are Phase 3.
  (create/edit/block/delete + PDF export), and sales reports (Daily / Monthly / Top-Selling, each with
  PDF export).

---

## 2. Ground rules for agents

1. **This directory is not a git repository.** There is no `git diff`, no `git checkout`, and no undo.
   Treat every delete or overwrite as permanent. Prefer additive edits; when you must rewrite a file,
   read it first and preserve its contents exactly.
2. **Do not run destructive EF Core tooling carelessly.** See [Section 9](#9-database-&-migrations).
   `dotnet ef migrations remove` will happily delete migration files and revert the model snapshot.
3. **Preserve the assignment-demonstrated patterns.** Several design choices exist specifically to satisfy
   marking criteria (see [Section 6](#6-architecture-rules-that-must-be-preserved)). Do not "modernise"
   them away — e.g. do not replace manual cookie auth with ASP.NET Core Identity, do not convert
   data annotations to Fluent API, do not move the cart out of Session.
4. **Match the existing code style.** See [Section 8](#8-code-style-conventions).
5. **Verify your change compiles** before declaring done: `dotnet build`. Zero errors is the bar.
6. **Do not commit secrets or invent new ones.** `appsettings.json` currently contains live-looking SMTP
   credentials (see [Section 10](#10-security-observations)). Do not add further credentials to source.

---

## 3. Technology stack

| Concern | Choice |
| --- | --- |
| Runtime / framework | **.NET 10.0** (`net10.0`), ASP.NET Core MVC |
| ORM | EF Core 10.0.9, **SQL Server Express LocalDB** |
| Database file | `DB.mdf` + `DB_log.ldf`, attached via `AttachDbFilename=\|DataDirectory\|` |
| Auth | **Manual cookie authentication** (not Identity) with `PasswordHasher<object>` |
| Session | ASP.NET Core Session — holds the shopping cart |
| Email | **MailKit** 4.17.0 (SMTP/Gmail) for password-recovery links |
| PDF | **QuestPDF** 2026.7.3 (Community licence) for receipts and reports |
| CSS | Bootstrap 5 (local) + Font Awesome 6.7.2 (CDN) + hand-written design system |
| JS | jQuery + Bootstrap bundle (local); all page JS is **inline in `.cshtml`** |

Notable absences — do not assume they exist: no `Areas/`, no Web API controllers, no `Services/` folder,
no `Repositories/`, no unit-test project, no `package.json`/npm, no bundler, no `wwwroot/js/site.js`
content (it is an empty scaffold stub; all JavaScript lives inline in views).

---

## 4. Repository layout

```
RestaurantSystem-Final/
├── RestaurantSystem.slnx              # Solution (new-style .slnx, single project)
├── .gitignore                         # Excludes .vs/, bin/, obj/, *.user, local secrets
├── .vs/                               # Visual Studio state — NOT shipped, auto-recreated on open
└── RestaurantSystem/
    ├── RestaurantSystem.csproj        # net10.0, Nullable+ImplicitUsings enabled
    ├── RestaurantSystem.csproj.user   # VS debug profile prefs — ignore (git-ignored)
    ├── Program.cs                     # DI registration + middleware pipeline + startup DB guard
    ├── appsettings.json               # Portable connection string + blank email placeholders
    ├── appsettings.Development.json   # Logging overrides only
    ├── appsettings.Development.Local.json.example  # Template for per-machine SMTP creds (git-ignored when used)
    ├── Helper.cs                      # Password hashing, cookie sign-in/out, random password
    ├── EmailServices.cs               # IEmailService + EmailService (MailKit SMTP)
    ├── DB.mdf / DB_log.ldf            # LocalDB database (binary; ~8 MB each; ships with the project)
    ├── Properties/launchSettings.json # http :5007, https :7089
    ├── Controllers/
    │   ├── HomeController.cs          # catalog, cart, add-ons, favorites, tables
    │   ├── AccountController.cs       # auth, profile, password reset
    │   ├── OrderController.cs         # checkout, payment, history, tracking, PDF receipt
    │   ├── ReservationController.cs   # customer reservations + conflict prevention
    │   └── AdminController.cs         # all admin features, kitchen queue, reports
    ├── Models/
    │   ├── DB.cs                      # DbContext + entities
    │   ├── Statuses.cs                # OrderStatus / OrderType / TableState / ReservationStatus constants
    │   ├── Pricing.cs                 # PricingSettings + Pricing.Calculate (single source of totals)
    │   ├── ViewModels.cs              # All view models (nested in `ViewModels` class)
    │   └── ErrorViewModel.cs
    ├── App_Data/backup-20261127/      # DB backup taken before the Phase 2 migration
    ├── Migrations/                    # 5 migrations + snapshot (see Section 9)
    ├── Views/
    │   ├── _ViewImports.cshtml        # Global usings incl. `@using static ...ViewModels`
    │   ├── _ViewStart.cshtml
    │   ├── Shared/ _Layout.cshtml, Error.cshtml, _ValidationScriptsPartial.cshtml
    │   ├── Home/     Index, Menu, ProductDetail, Cart, Favorites, Tables
    │   ├── Account/  Login, Register, Profile, EditProfile, ResetPassword,
    │   │             PasswordRecovery, PasswordRecoveryReset, AccessDenied, Orders
    │   ├── Order/    Checkout, Payment, EWalletCheckout, Confirmation, Details, History
    │   ├── Reservation/  Index, Create
    │   └── Admin/    Dashboard, Orders, Kitchen, Reservations, Tables, Payments,
    │                 Categories, Products, UserManage, Daily, Monthly, TopSelling, Report,
    │                 + Create/Edit/Delete variants, UserCreate, UserEdit,
    │                 + _AdminLayout.cshtml, _Sidebar.cshtml
    └── wwwroot/
        ├── css/  site.css (1031), dark-mode.css (259), index.css, menu.css,
        │         admin.css, tables.css
        ├── js/   site.js (empty stub)
        ├── images/  ~16 dish photos + avatars/
        └── lib/  bootstrap, jquery (vendored — do not edit)
```

**Size map:** `AdminController.cs` is by far the largest file. If a task touches admin, read only the
action group you need (it has clear `// =====` section banners).

---

## 5. Request pipeline & wiring (`Program.cs`)

Registration order matters — follow it when adding services:

```csharp
QuestPDF.Settings.License = LicenseType.Community;
AppDomain.CurrentDomain.SetData("DataDirectory", builder.Environment.ContentRootPath); // enables |DataDirectory|
builder.Services.AddDbContext<RestaurantDbContext>(... UseSqlServer("DefaultConnection"));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => { o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/AccessDenied"; });
builder.Services.AddSession(o => { /* 30 min idle, HttpOnly, IsEssential */ });
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<Helper>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmailService, EmailService>();
```

Pipeline: `UseHttpsRedirection` → `UseStaticFiles` → `UseRouting` → **`UseSession`** →
`UseAuthentication` → `UseAuthorization` → `MapStaticAssets` → default route
`{controller=Home}/{action=Index}/{id?}`.

> **`UseSession()` must stay after `UseRouting()` and before `UseAuthentication()`.** The cart and the
> multi-step checkout both depend on it. Removing or reordering it breaks checkout silently.

`Helper` is registered **scoped** but holds `PasswordHasher<object>` and an `IHttpContextAccessor`; all
cookie operations go through it. Reach for `Helper` rather than calling `HttpContext.SignInAsync` directly.

---

## 6. Architecture rules that must be preserved

These are deliberate, assignment-driven decisions. Comments in the source explain each one — respect them.

- **Manual cookie authentication.** `Helper.SignIn` builds `ClaimTypes.Name` (username) +
  `ClaimTypes.Role` (`ADMIN`/`MEMBER`). There is no Identity user store. Roles are matched with
  `[Authorize(Roles = "ADMIN")]` and `User.IsInRole("ADMIN")`.
- **Passwords are hashed via `Helper.HashPassword` / `VerifyPassword`** (`PasswordHasher<object>`,
  "Slide 11" pattern). Never compare plaintext. Never store unhashed passwords.
- **Data annotations on entities, not Fluent API.** `Models/DB.cs` uses `[Required]`, `[StringLength]`,
  `[Range]`, `[EmailAddress]`, `[Phone]`, `[Column(TypeName = "decimal(18, 2)")]`. The **only** Fluent API
  use is `Product → Category` with `OnDelete(DeleteBehavior.Restrict)`, which annotations cannot express
  (documented in a comment; keep it that way).
- **Cart lives in Session, not the database.** Serialized as JSON under the key `"OrderingCart"`.
  The **string literal `"OrderingCart"` is duplicated in three places** — `HomeController.CART_KEY`,
  `OrderController.CART_KEY`, and `_Layout.cshtml`. If you change it, change all three or the cart badge
  silently desyncs.
- **No navigation properties on `Order` / `OrderDetail`.** Admin queries deliberately join in memory via
  dictionaries (`ToDictionary`) instead of `.Include()`. Only `Product.Category` and
  `Category.Products` exist as navigation properties (`Product.Category` is also `[BindNever]`).
- **`User` identity is resolved by username.** Controllers map `User.Identity.Name` → `User.UserId` via a
  private `GetCurrentUserId()` helper (duplicated in `HomeController` and `OrderController`, with a third
  inline copy in `AccountController`). This is intentional; keep the pattern rather than introducing a
  claims-based `UserId` (it would change what existing auth code demonstrates).
- **TempData for flash messages**, read by `_AdminLayout.cshtml` (`TempData["Info"]`, `TempData["Error"]`)
  and by layouts/views for customer pages (`TempData["Info"]`).
- **View models are nested classes** inside `public class ViewModels`, imported globally via
  `@using static RestaurantSystem.Models.ViewModels`. Views declare e.g. `@model PaymentVM`, not
  `@model ViewModels.PaymentVM`. The few exceptions use fully-qualified list types
  (`@model List<RestaurantSystem.Models.ViewModels.OrderVM>`).

---

## 7. Domain model (`Models/DB.cs`, `Models/Statuses.cs`, `Models/Pricing.cs`)

`RestaurantDbContext` → 10 `DbSet`s: `Users`, `Tables`, `Categories`, `Products`, `ProductAddons`,
`Orders`, `OrderDetails`, `OrderItemAddons`, `Payments`, `Reservations`, `Favorites`.

| Entity | Key fields / notes |
| --- | --- |
| `User` | `Username`, `Email` (unique by controller check, **not** by DB constraint), `Password` (hash), `FullName`, `PhoneNumber`, `Role` (`ADMIN`/`MEMBER`), reset token + 30-min expiry, `ProfilePhotoUrl`, `IsBlocked`, `Address` |
| `Table` | `TableName`, `Capacity`, `Location`, `Status`, plus the legacy `IsOccupied`. **Always change status via `SetStatus(...)`** — it keeps `IsOccupied` in sync for the old views |
| `Category` / `Product` | `Product.ImageUrl` is **nullable on purpose** (form posts `IFormFile`; a non-nullable string trips implicit-required). `Product.IsActive` deactivates a dish without losing history. `Product.StockQuantity` |
| `ProductAddon` | `ProductId`, `Name`, `Price`, `IsActive` — customisation options offered per dish |
| `Order` | `UserId`, `TableId?`, `OrderDate`, `OrderStatus`, `DiningOption` (`DineIn`/`Takeaway`/`Delivery`), `PaymentMethod`, `DeliveryAddress`, **price breakdown** (`SubtotalAmount`, `ServiceCharge`, `TaxAmount`, `DeliveryFee`, `DiscountAmount`; `TotalAmount` = grand total), `StatusUpdatedDate`, `CancellationReason` |
| `OrderDetail` | `OrderId`, `ProductId`, `Quantity`, `PriceAtOrder` (snapshot), `SpecialInstructions`, `Addons` |
| `OrderItemAddon` | `OrderDetailId`, `Name`, `PriceAtOrder` — add-on **snapshot** so later edits never rewrite history |
| `Payment` | `OrderId`, `Method`, `Amount`, `Status` (`PAID`/`UNPAID`/`REFUNDED`), `Reference`, dates |
| `Reservation` | `UserId?`, `CustomerName`, `Phone`, `Email?`, `ReservationDate`, `ReservationTime`, `Guests`, `TableId?`, `Status`, `SpecialRequests`; `StartsAt`/`EndsAt` (`DurationMinutes` = 120) |
| `Favorite` | `UserId`, `ProductId`, `AddedDate` — no unique constraint |

### Status constants (`Models/Statuses.cs`)

All status strings live in one file. **Do not write status literals in controllers or views** — use
`OrderStatus.Pending`, `TableState.Occupied`, `ReservationStatus.Confirmed`, etc.

- **Order lifecycle** — `PENDING → CONFIRMED → PREPARING → READY → COMPLETED`, with `CANCELLED` reachable
  from `PENDING`/`CONFIRMED`/`PREPARING`. `OrderStatus.CanTransition(from, to)` is the single authority;
  `NextStatuses(from)` drives which buttons a page renders.
- **Table states** — `Available`, `Reserved`, `Occupied`, `Cleaning`, `Unavailable`.
- **Reservation states** — `PENDING`, `CONFIRMED`, `CANCELLED`, `REJECTED`, `COMPLETED`.

### Pricing (`Models/Pricing.cs`)

`Pricing.Calculate(subtotal, orderType, settings)` returns the full `OrderTotals`. Rules come from
`appsettings.json → RestaurantSettings` (`TaxPercent`, `ServiceChargePercent`, `DeliveryFee`):

- service charge applies to **dine-in only**, tax is charged on `subtotal + service`,
- delivery fee applies to **delivery only**,
- the cart, checkout, payment, confirmation, receipt PDF and the checkout page's live estimate all use the
  same helper, so totals can never disagree.

### Important invariants

- **Validate totals server-side.** `OrderController.CreateOrder` recalculates from database prices and
  ignores anything the browser posted (Workflow.md §8.10).
- **Stock is decremented only when an order is created**, never while in a cart. Cancelling restores it —
  `ApplyStatusChange` guards against double-restoring by checking the previous status.
- **Table occupancy** follows the order: dine-in sets `Occupied`; completing sets `Cleaning`; cancelling,
  checking out or `AdminFreeTable` sets `Available`.
- **Saved address vs. order address:** `User.Address` is reusable; `Order.DeliveryAddress` is a per-order
  snapshot so editing the profile later does not rewrite order history. Keep both.
- **Reservation conflicts** (Workflow.md §8.4): one table cannot hold two overlapping active bookings.
## 8. Code style conventions

Observed consistently across the codebase — follow them so new code looks native.

- **C#**
  - `Nullable` and `ImplicitUsings` are **enabled**; required fields use the `required` modifier.
  - Two naming styles coexist: `HomeController` uses `_db`; `AccountController`, `OrderController`, and
    `AdminController` use `db` + `hp`. Match the file you are editing.
  - Dependencies are constructor-injected into **`private readonly`** fields.
  - Controllers return `IActionResult` (or `async Task<IActionResult>`); actions are annotated
    `[HttpGet]`/`[HttpPost]` (frequently omitted on simple GETs — acceptable but be consistent locally).
  - Synchronous EF calls (`ToList`, `FirstOrDefault`, `SaveChanges`) are the norm. Do **not** mass-convert
    to async; do use async where the API already is (`CreateProduct`'s file copy, email sending).
  - Query style is mixed: LINQ query syntax for multi-join projections (`Home/Index`,
    `BuildOrderSummary`), method syntax otherwise. Both are idiomatic here.
  - Private helpers sit at the bottom of each controller under a `// Private helpers` banner.
  - Section banners use `// ==========================================` … with `[GET/POST: Action]` headers.
  - Comments explain **why**, often referencing the assignment slide or a past bug. Preserve them.
- **Razor views**
  - Every view opens with a `@{ Layout = ...; ViewData["Title"] = "..."; }` block.
  - Admin views set `Layout = "~/Views/Admin/_AdminLayout.cshtml"` (or `"_AdminLayout"`) **and**
    `ViewBag.ActiveMenu = "<MenuKey>"` to highlight the sidebar (`_Sidebar.cshtml` reads it; keys are
    `Dashboard`, `UserManage`, `Orders`, `Categories`, `Products`, `Report`, `ReportDaily`,
    `ReportMonthly`, `ReportTopSelling`). **Add the ActiveMenu value when adding an admin page.**
  - Page-specific CSS is linked per-view: `<link rel="stylesheet" href="~/css/menu.css" asp-append-version="true" />`.
  - Bulk JavaScript is written **inline** in a `<script>` block at the bottom of the view (no modules, no
    bundling). Client data is passed in by Razor interpolation, e.g. `const userRole = "@userRole";`.
  - Use tag helpers everywhere: `asp-controller`, `asp-action`, `asp-route-*`, `asp-for`, `asp-validation-for`.
  - Forms that POST include `@Html.AntiForgeryToken()` where the controller validates it.
- **CSS**
  - All design tokens are CSS custom properties prefixed `--ffg-` (`--ffg-emerald-700`, `--ffg-gold-500`,
    `--ffg-cream`, `--ffg-ink`, `--ffg-muted`, `--ffg-line`, `--ffg-shadow`, `--ffg-font-body`,
    `--ffg-font-display`).
  - **Always use the `--ffg-*` variables instead of hard-coded hex values** — that is what makes dark mode
    work, because `dark-mode.css` redefines those variables under `html.dark-mode`.
  - `site.css` is the global design system and loads after Bootstrap. Feature CSS files are additive.
  - `dark-mode.css` **overrides variables and patches specific components** under `html.dark-mode`. If you
    add a component with a hard-coded colour, add a matching patch there or it will break in dark mode.
  - Utility class prefixes: `ffg-*` (site-wide), `admin-*` (admin panel), `btn-ffg`, `btn-outline-ffg`,
    `btn-admin`, `btn-outline-admin`.
- **Palette:** deep emerald (`#0d3226`–`#1d6b50`), warm gold (`#c9a227`), soft cream (`#faf7f0`).
  Typography: Cormorant Garamond (display) + Inter (body), loaded from Google Fonts.
  Currency is **RM (Malaysian Ringgit)**, formatted `RM @value.ToString("N2")` / `RM {x:F2}`.

---

## 9. Database & migrations

### Current state — the baseline is now clean

Connection: `Server=(localdb)\mssqllocaldb;AttachDbFilename=|DataDirectory|\DB.mdf;Database=RestaurantSystemDB;
Trusted_Connection=True;MultipleActiveResultSets=true`

`dotnet ef migrations has-pending-model-changes` reports **"No changes"** — the model, the migrations and
the shipped `DB.mdf` all agree.

| Migration | What it did |
| --- | --- |
| `20260708123322_CreateDB` | Initial schema (Users, Tables, Categories, Products, Orders, OrderDetails) |
| `20260715140421_UpdateDB` | Reworked Users/Tables/Orders, **dropped most FKs and all FK indexes**, added `Notes` |
| `20260830033453_UpdateDBv1` | Password-reset token, `ProfilePhotoUrl`, `StockQuantity`, `DiningOption`, `PaymentMethod`, `Favorites` |
| `20261127081948_SyncModelWithDb` | Reconciles the model with the DB that shipped (see below) |
| `20261127082133_Phase2Core` | Reservations, Payments, add-ons, order lifecycle + price breakdown, table status |

### History of the drift (now resolved — keep it that way)

The shipped `DB.mdf` had been edited **out-of-band** in Visual Studio, so it contained columns
(`Users.Address`, `Users.IsBlocked`, `Orders.DeliveryAddress`) that no migration ever created, and
`__EFMigrationsHistory` listed only `CreateDB`. `SyncModelWithDb` fixes that: every statement in it is
guarded with `IF COL_LENGTH(...) IS NULL` / `IF NOT EXISTS`, so it is safe on **both** the shipped database
(where the objects already existed) and a brand-new one. Its `Down()` is a deliberate no-op.

> **Lesson for future migrations:** EF runs one migration as a **single SQL batch**, and SQL Server
> compiles a batch *before* executing it. If a migration adds a column and then `UPDATE`s it, the `UPDATE`
> fails to compile ("Invalid column name"). The existing migrations work around this with `EXEC(...)`
> (dynamic SQL defers name resolution). Do the same, and always verify with
> `dotnet ef migrations script 0 --idempotent` replayed against a scratch database.

### Applying schema changes

```powershell
cd RestaurantSystem\RestaurantSystem
dotnet build                                        # must be green first
dotnet ef migrations add <DescriptiveName>          # generates .cs + .Designer.cs
dotnet ef migrations script 0 --idempotent          # review the SQL BEFORE touching the DB
dotnet ef database update
```

Also verify a **fresh** database builds correctly (this is what catches drift):

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE Scratch"
# replay the generated script into Scratch, check for Msg errors, then drop Scratch
```

### 🚫 Never do this

- **Do not run `Remove-Migration` / `dotnet ef migrations remove`.** It deletes migration files and
  rewrites the snapshot, and there is no git to undo it.
- **Do not run `DROP DATABASE`** on this database: for an `AttachDbFilename` database it deletes `DB.mdf`
  and `DB_log.ldf` from disk. To detach without losing files use
  `ALTER DATABASE [RestaurantSystemDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; EXEC sp_detach_db 'RestaurantSystemDB';`
- Back up `DB.mdf` + `DB_log.ldf` (together, app stopped) before any schema work. There is a copy from
  before the Phase 2 work in `RestaurantSystem/App_Data/backup-20261127/`.
## 10. Security observations

Recorded so agents neither re-introduce nor "silently fix" these mismatched to the assignment's intent.
**These are reported, not endorsed.**

- **SMTP credentials have been moved out of the shared config.** `appsettings.json` now ships with
  empty `EmailSettings.SenderEmail` / `SenderPassword`. Each developer supplies their own in the
  git-ignored `appsettings.Development.Local.json` (template: `appsettings.Development.Local.json.example`),
  which `Program.cs` loads as an optional overlay. With blank values the app still runs; password recovery
  reports "We couldn't send the email right now." **A Gmail app password was previously committed in this
  file and should be considered exposed — the owner should rotate/revoke it.** Do not add new secrets to
  `appsettings.json`.
- **CSRF coverage is partial.** `ValidateAntiForgeryToken` appears on all 5 POSTs in `OrderController` and
  6 of 13 in `AdminController`, but **0 of 8 in `AccountController`** and **0 of 9 in `HomeController`**.
  Notable gaps: `Account/Login`, `Account/Register`, `Account/Logout` (the `_Layout` logout posts via AJAX
  with no token), and all cart/favorite AJAX POSTs.
- **`Helper.SignIn`/`SignOut` fire-and-forget `SignInAsync`/`SignOutAsync`** without `await`. Works in
  practice for most flows, but is a latent race — the cookie write may not finish before the redirect.
- **Open redirect is mitigated**: `Account/Login` validates `returnURL` via `LocalRedirect(returnURL)`
  (throws on external URLs) rather than `Redirect`. Keep using `LocalRedirect`.
- **Password recovery** stores a random 256-bit token (`RandomNumberGenerator.GetBytes(32)`, hex) with a
  30-min expiry and clears it on use — good. It is passed in the query string and **not** yet
  single-use-at-first-GET (it is cleared on successful POST). Rate limiting is absent.
- **`AdminController.DeleteUser`** blocks self-deletion and refuses to delete users with orders (good).
  `ToggleBlock` refuses to block `ADMIN` accounts (good).
- **Uploads**: profile photos are limited to `.jpg/.jpeg/.png` and 2 MB and renamed to `user_{UserId}{ext}`
  (good). **Admin product images are not validated** — `CreateProduct`/`EditProduct` accept any
  `IFormFile` and use `Path.GetFileName` with the product id prefix; no extension or size check.
- **No global exception handling in Development**; `UseExceptionHandler("/Home/Error")` only runs
  when `!IsDevelopment()`. Only `AccountController` has a `try/catch` (around SMTP send).
- **`[HttpPost]` admin mutations without anti-forgery**: `UpdateOrderStatus`, `UpdateOrderStatusBatch`,
  `DeleteProductsBatch`, `ToggleBlock`, `DeleteUser`, `UserCreate`, `UserEdit`.
- **Username/Email uniqueness is enforced only in controller code**, not by a DB unique index — a race
  can create duplicates.

---

## 11. Known issues / technical debt

Found while analysing; none block the build. Useful context if a task touches these areas.

| Area | Issue |
| --- | --- |
| `Views/Home/Menu.cshtml` | *(Fixed)* Category filter pills are now rendered from the `Categories` table via `ViewBag.Categories`, so they track admin changes and are correct on any machine. |
| `Views/Home/Index.cshtml` | Also references category names as static tags. |
| `HomeController.SearchProducts` | Search is `Contains`-only (case-insensitive via DB collation), `pageSize` is a hard-coded `9`, and it materialises favourites + all category names per request. |
| `AdminController.Orders` | Loads **all** order rows into memory before applying the text `search` filter. Fine at demo scale, not at volume. |
| Repos-wide | **Zero `AsNoTracking()`** on read-only queries — every list page tracks entities. No pagination on admin lists. |
| `AdminController.CreateProduct`/`EditProduct` | *(Fixed)* Image paths now resolve through `IWebHostEnvironment.WebRootPath` via the `ImagesFolder` property, instead of `Directory.GetCurrentDirectory()`. Product image uploads still have **no validation** (see Section 10). |
| `AccountController.UploadPhoto` | Uses `env.WebRootPath` correctly — the two upload paths are inconsistent. |
| Duplicated logic | `GetCurrentUserId()` is copied in 3 controllers; `GetCartFromSession()` in 2; `CART_KEY` literal in 3 places. Acceptable here, but if you refactor, do it consistently and in one change. |
| `Report` action | Not wired into `_Sidebar.cshtml` (sidebar links straight to Daily/Monthly/TopSelling), though the page exists and sets `ActiveMenu = "Report"`. |
| `Views/Account/Orders.cshtml` | Vestigial — `Account/Orders` is now just a redirect to `Order/History`. |
| `wwwroot/js/site.js` | Empty scaffold; genuinely unused. |
| Reports | `Daily`/`Monthly`/`TopSelling` exclude `CANCELLED` orders but **include `PENDING`** ones in revenue. Confirm intent before "fixing". |
| Build warnings | `NU1902`/`NU1903`: transitive `System.Security.Cryptography.Pkcs` 10.0.0 has known vulnerabilities. Pre-existing; do not silently upgrade package versions without asking. |
| `_Layout.cshtml` | Injects `RestaurantDbContext` directly to count favourites on every page load — a DB query per request. |

---

## 12. Build & run

```powershell
# Build (expect: Build succeeded, 0 Errors, 4 NU190x warnings)
dotnet build .\RestaurantSystem\RestaurantSystem.csproj

# Run (Development; launch profile "https" is the default in .csproj.user)
dotnet run --project .\RestaurantSystem\RestaurantSystem.csproj
#   https://localhost:7089  and  http://localhost:5007
```

- Requires the **.NET SDK 10 or newer** and **SQL Server Express LocalDB** (`mssqllocaldb`). This machine
  has SDK 11.0.100 with the .NET 10.0.15 runtime, which builds `net10.0` fine. Visual Studio is optional —
  the `dotnet` CLI builds and runs the project on its own.
- `DB.mdf` is attached via `|DataDirectory|`, which `Program.cs` sets to the **content root**, so the
  database always resolves to `<project folder>\DB.mdf` **regardless of the working directory** the app is
  launched from. The `.mdf` must not be locked by another instance (stop `sqlservr`/other VS instances if
  attach fails). If the file is missing the app now **stops at startup with an explicit message** naming
  the path and the fix, instead of serving HTTP 500s.
- QuestPDF Community licence is set in `Program.cs`; keep that line or PDF generation throws.
- No automated tests exist. Verify manually in the browser; the main smoke path is
  register → login → add to cart → checkout → pay → confirmation → receipt PDF.

### 12.1 Porting this project to another computer

The project is designed to be copied and run with **no code or config edits**. Every claim below was
verified by copying the project to a different folder and running it there.

**Checklist for a new machine:**

1. **Prerequisites** — install, in this order of importance:
   - **.NET SDK 10 or newer** (the project targets `net10.0`; a machine with only the .NET 11 SDK builds
     it fine, because the .NET 10 runtime is present). Verify with `dotnet --list-sdks`.
   - **SQL Server Express LocalDB** — the database engine. Verify with `sqllocaldb info mssqllocaldb`;
     start it with `sqllocaldb start MSSQLLocalDB` if the state is `Stopped`.
   - **Visual Studio is optional.** It is the normal way to work on the project, but the app builds and
     runs from the command line with `dotnet run` on a machine with no Visual Studio at all. If VS is used,
     the *ASP.NET and web development* workload is what provides the tooling.
2. **Copy the folder** — include `DB.mdf` and `DB_log.ldf`. They are the data carrier for this assignment
   and are deliberately **not** git-ignored.
3. **Do not copy `.vs/`** — it is per-user Visual Studio cache and holds absolute paths from whichever
   machine created it. Delete it if a copy contains one; Visual Studio recreates it, and the `dotnet` CLI
   never creates it. `.gitignore` already excludes it.
4. **Open / run** — either open `RestaurantSystem.slnx` and press **F5**, or run
   `dotnet run --project RestaurantSystem/RestaurantSystem.csproj`. The database attaches automatically at
   the new location.
5. **Optional — password-recovery email.** Copy `RestaurantSystem/appsettings.Development.Local.json.example`
   to `RestaurantSystem/appsettings.Development.Local.json` and fill in your own SMTP sender. That file is
   git-ignored. Without it the app runs normally and password recovery simply reports that the email
   couldn't be sent.

**What is portable (verified):**

| Concern | Status |
| --- | --- |
| Source code paths | No absolute paths in any `.cs`/`.cshtml`/`.json`/`.csproj` |
| Database | `\|DataDirectory\|` is set to the content root, so it re-attaches at whatever folder the project sits in — the absolute path never appears in config |
| Working directory | The app resolves the DB and `wwwroot` correctly even when launched from an unrelated directory (e.g. `C:\Windows`) |
| Uploaded images | Stored as root-relative `/images/...` URLs and resolved via `IWebHostEnvironment.WebRootPath` |
| Menu category filter | Rendered from the `Categories` table, not hard-coded ids |
| Secrets | Blank in the shared `appsettings.json`; per-machine values live in a git-ignored file |
| Build without Visual Studio | Verified: `dotnet build`/`dotnet run` succeed with no `.vs/` and no VS |
| Visual Studio state | `.vs/` regenerates cleanly on open and references only the current machine's paths |

**Known cross-machine snags and how to clear them:**

- **The same database name is already attached elsewhere.** This is the one realistic trap, and it is
  *silent*: LocalDB registers a database by **name** (`RestaurantSystemDB`), not by file path. If another
  copy of the project on the same machine is already attached under that name, starting this copy
  **re-attaches the name to the new path** — the old copy's pages then read the new copy's data. It does not
  throw an error, so it is easy to miss. In practice this only matters if two copies are run on one machine
  at once. Fix: stop the other copy, then
  `sqllocaldb stop MSSQLLocalDB` (the next run re-attaches cleanly).
- **`DB.mdf` is locked by another process.** Symptom: attach fails with SQL Server error 5120 / OS error 32.
  Close other Visual Studio windows, running copies of the app, or SQL tooling holding the file.
- **Paths with spaces or non-ASCII characters** (the original developer's path was
  `OneDrive\文档\Desktop\...`) work, but if an attach ever fails there, move the project to a plain path such
  as `C:\Projects\` and retry.
- **Windows only.** LocalDB and `Trusted_Connection` are Windows-specific; there is no Linux/macOS path
  without switching the database to SQL Server or SQLite.

**⚠️ Never use `DROP DATABASE` on this database.** Unlike a normal server database, dropping a LocalDB
database that was created with `AttachDbFilename` **deletes `DB.mdf` and `DB_log.ldf` from disk** — there is
no recycle bin and no undo. To detach without losing files use:

```sql
USE master;
ALTER DATABASE [RestaurantSystemDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
EXEC sp_detach_db 'RestaurantSystemDB';
```

Back up `DB.mdf` + `DB_log.ldf` before any database surgery.

**Backing up / moving the data:** copy `DB.mdf` and `DB_log.ldf` **together** while the app is stopped.
Both files are required — the `.ldf` is the transaction log, and an `.mdf` without its log will not attach.

---

## 13. Task recipes

**Add an admin page**
1. Add the action to `AdminController` inside the relevant `// =====` section.
2. Create `Views/Admin/<Name>.cshtml` with `Layout = "_AdminLayout"`, `ViewData["Title"]`, and
   `ViewBag.ActiveMenu = "<key>"`.
3. If it is a new sidebar entry, add it to both `_Sidebar.cshtml` **and** the list of valid keys above.
4. Inline any page JS; link page CSS if new.
5. `dotnet build`.

**Add a customer-facing feature**
- Read the owning controller (`Home` for catalog/cart/favorites, `Order` for checkout/history).
- Add the view under the matching `Views/<Controller>/` folder; it inherits `_Layout` via `_ViewStart`.
- Remember `[Authorize]` for anything account-scoped, and re-use `GetCurrentUserId()`.
- If it shows a cart/favourite count, note `_Layout.cshtml` computes both server-side.

**Change the schema**
- Follow Section 9. Read `Models/DB.cs` first. Prefer annotations over Fluent API.
- If you add a `required` string to an entity that is posted from a form, consider whether model binding
  will trip implicit-required — `Product.ImageUrl` is nullable precisely because of this trap.

**Add an order status**
- Update `Models/Statuses.cs` (`OrderStatus.All`, `Flow`, `_allowed`, `Label`, `BadgeClass`, `Description`).
- Admin buttons and the customer timeline are all generated from those helpers, so they update automatically.
- Migrate existing rows if the value replaces an old one.

**Change pricing (tax / service / delivery)**
- Edit `RestaurantSettings` in `appsettings.json` — no code change needed. `Pricing.Calculate` is the only
  place totals are computed; verify the checkout page's live estimate still matches the server total.

**Work on reservations**
- All conflict logic is the single `FindConflict` helper in `ReservationController`; keep one implementation
  and reuse it in admin actions rather than writing a second overlap check.
**Touch styling**
- Prefer editing the relevant CSS file and **using `--ffg-*` variables**.
- Add a `html.dark-mode` patch in `dark-mode.css` for anything new that has a colour.

**Touch auth or roles**
- `Program.cs` (scheme/config), `Helper.cs` (claims/sign-in), `AccountController` (flows),
  `_Layout.cshtml` and `_Sidebar.cshtml` (conditional rendering). Change all layers together.
- Roles are the strings `"ADMIN"` / `"MEMBER"` — they appear in `[Authorize(Roles = ...)]`,
  `User.IsInRole(...)`, `ModelState`/view logic, and admin role dropdowns.

---

## 14. Quick reference

| Thing | Value |
| --- | --- |
| Project file | `RestaurantSystem\RestaurantSystem.csproj` (`net10.0`) |
| Launch URLs | https://localhost:7089 · http://localhost:5007 |
| Login path | `/Account/Login` · Access denied: `/Account/AccessDenied` |
| Admin entry | `/Admin/Dashboard` |
| Admin pages | `Orders`, `Kitchen`, `Reservations`, `Tables`, `Payments`, `Categories`, `Products`, `UserManage` |
| Admin reports | `/Admin/Report`, `/Admin/Report/Daily`, `/Admin/Report/Monthly`, `/Admin/Report/TopSelling` (+ `/Export`) |
| Customer reservation | `/Reservation/Index`, `/Reservation/Create` |
| Session cart key | `"OrderingCart"` (×3 literals: `HomeController`, `OrderController`, `_Layout.cshtml`) |
| Pending checkout keys | `Pending_DiningOption`, `Pending_TableId`, `Pending_PaymentMethod`, `Pending_Address` |
| Order statuses | `PENDING`, `CONFIRMED`, `PREPARING`, `READY`, `COMPLETED`, `CANCELLED` |
| Order types | `DineIn`, `Takeaway`, `Delivery` |
| Table states | `Available`, `Reserved`, `Occupied`, `Cleaning`, `Unavailable` |
| Reservation states | `PENDING`, `CONFIRMED`, `CANCELLED`, `REJECTED`, `COMPLETED` |
| Payment statuses | `PAID`, `UNPAID`, `REFUNDED` |
| Payment methods | `Cash`, `EWallet` |
| Pricing config | `appsettings.json → RestaurantSettings` (tax 6%, service 10% dine-in, delivery RM 5) |
| Roles | `ADMIN`, `MEMBER` |
| Theme storage key | `ffg-theme` (legacy: `bite-theme`), class `html.dark-mode` |
| Currency | RM, `N2` format |
| EF tool | `dotnet-ef` 10.0.11 installed globally |
| DB backup | `RestaurantSystem/App_Data/backup-20261127/` (pre-Phase-2 snapshot) |
