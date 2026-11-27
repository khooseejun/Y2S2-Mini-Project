# Restaurant Management System -- Function Workflow Design

> **Implementation status (updated 2026-11-27).** This document was originally a *forward-looking design
> brief*. The system has since been built, so each section now carries a status marker showing what is
> implemented and what is deliberately deferred.
>
> | Marker | Meaning |
> | --- | --- |
> | ✅ **Implemented** | Built and verified in the running application |
> | 🟡 **Partial** | Core behaviour exists; enhancements listed in the section are missing |
> | ⬜ **Not implemented** | Deferred (see §7) |
>
> Where the built system differs from the original design, the difference is called out explicitly under
> **"As built"**. The design intent above the marker is preserved unchanged so the original requirements
> remain readable.
>
> For implementation detail see `Agent.md` (architecture, conventions) and `database.md` (schema and data).

## 1. System Overview ✅ Implemented

The Restaurant Management System is a web-based application for managing
restaurant operations. It has two main interfaces:

1.  **Customer Website** -- Customers browse menus, place orders, make
    reservations, pay, and track orders.
2.  **Admin Dashboard** -- Administrators and staff manage orders,
    menus, tables, reservations, payments, inventory, staff, and
    reports.

**As built:** one ASP.NET Core MVC application serving both interfaces. Authentication is a single
cookie-based login page with a **Member / Admin switch** that separates the two portals — an ADMIN account
cannot sign in through the Member tab. Roles are `ADMIN` and `MEMBER` only; the richer staff roles shown
in §3.7 are not implemented.

### Overall System Structure

``` mermaid
flowchart TD
    A[Restaurant Management System]
    A --> B[Customer Website]
    A --> C[Admin Dashboard]

    B --> D[Browse Menu]
    B --> E[Make Reservation]
    B --> F[Place Order]
    B --> G[Online Payment]
    B --> H[Track Order]

    C --> I[Dashboard]
    C --> J[Order Management]
    C --> K[Menu Management]
    C --> L[Table Management]
    C --> M[Reservation Management]
    C --> N[Staff Management]
    C --> O[Inventory Management]
    C --> P[Payment Management]
    C --> Q[Reports and Analytics]

    D --> K
    E --> M
    F --> J
    G --> P
    H --> J
```

Of the nine admin modules above, **seven are built** (Dashboard, Order, Menu, Table, Reservation, Payment,
Reports). **Staff Management** and **Inventory Management** are deferred.

------------------------------------------------------------------------

## 2. Customer Website Workflow

The customer website should be simple, attractive, and easy to navigate.

### 2.1 Customer Main Page ✅ Implemented

**Functions** - View restaurant information and opening hours. - Browse
food categories and menu items. - Search for food. - View food images,
descriptions, prices, and promotions. - Register or log in. - Access the
shopping cart. - Make a table reservation.

**Workflow**

``` mermaid
flowchart TD
    A[Visit Restaurant Website] --> B[Home Page]
    B --> C[View Restaurant Information]
    B --> D[Browse Food Menu]
    B --> E[Login or Register]
    B --> F[Make Reservation]
    D --> G[Select Food Category]
    G --> H[View Food Details]
    H --> I[Add to Cart]
    I --> J[Shopping Cart]
```

**As built:** Home page (`/`) shows restaurant info plus a "top selling" carousel built from real order
data. Menu (`/Home/Menu`) is AJAX-driven with search, category filter, price range, sorting and pagination
(9 per page). Product detail pages show an image, description, price and available add-ons. Navigation
exposes Home / Our Menu / Tables / Reservations plus a cart and favourites badge.

**Not implemented:** *promotions* — there is no promotions module, so no promotional pricing is displayed
(the `Order.DiscountAmount` column exists as a hook only).

### 2.2 Customer Registration and Login ✅ Implemented

**Functions** - Create a customer account. - Log in with email and
password. - Log out. - Reset password. - Edit customer profile. - View
order history.

**Workflow**

``` mermaid
flowchart TD
    A[Customer Account] --> B{Already Registered?}
    B -->|Yes| C[Login]
    B -->|No| D[Register Account]
    D --> E[Enter Personal Information]
    E --> F[Validate Information]
    F --> G{Information Valid?}
    G -->|No| E
    G -->|Yes| H[Create Account]
    H --> C
    C --> I[Verify Login Details]
    I --> J{Correct?}
    J -->|No| C
    J -->|Yes| K[Customer Dashboard]
```

**As built:**

- Registration validates username/email uniqueness before creating the account; the role is always
  `MEMBER` for self-registration.
- **Login is split into two portals.** `/Account/Login` has a Member / Admin switch. The choice is enforced
  server-side: an ADMIN account cannot sign in via the Member tab, and a MEMBER account cannot sign in via
  the Admin tab. A mismatch is rejected with a message telling the user which tab to use.
- Two password-reset paths exist: a logged-in change (current password required) and an emailed
  time-limited link (30 minutes) via SMTP.
- There is **no separate "customer dashboard"** — after login a member lands on the home page, with
  Profile and My Orders available from the account menu.

### 2.3 Food Ordering System ✅ Implemented

**Functions** - Browse food menu. - Select food items and quantities. -
Choose food options or add-ons. - Add special instructions. - Add items
to the cart. - Update quantities or remove items. - Calculate subtotal
and total price.

**Workflow**

``` mermaid
flowchart TD
    A[Browse Menu] --> B[Select Food]
    B --> C[View Food Details]
    C --> D[Choose Quantity]
    D --> E[Choose Add-ons]
    E --> F[Add Special Instructions]
    F --> G[Add to Cart]
    G --> H[Shopping Cart]
    H --> I{Continue Shopping?}
    I -->|Yes| A
    I -->|No| J[Review Order]
    J --> K[Confirm Order]
    K --> L[Proceed to Checkout]
```

**As built:** each dish can offer optional **add-ons** (e.g. Extra Cheese +RM 2.00) with a free-text
**special-instruction** note (max 200 chars). Two cart lines for the same dish with different add-ons or
notes are kept separate. Quantities are capped at available stock, and stock is decremented only when the
order is actually placed — not while sitting in a cart.

### 2.4 Checkout and Payment ✅ Implemented

**Functions** - Review order summary. - Select dine-in, takeaway, or
delivery. - Select a table for dine-in, where applicable. - Enter
delivery address for delivery orders. - Apply discount codes. -
Calculate subtotal, tax, service charges, and total. - Select payment
method. - Process payment and generate order confirmation.

**Workflow**

``` mermaid
flowchart TD
    A[Checkout] --> B[Review Order]
    B --> C[Select Order Type]
    C --> D{Order Type}
    D -->|Dine-in| E[Select Table or Seating]
    D -->|Takeaway| F[Confirm Pickup Details]
    D -->|Delivery| G[Enter Delivery Address]
    E --> H[Calculate Total]
    F --> H
    G --> H
    H --> I[Select Payment Method]
    I --> J[Process Payment]
    J --> K{Payment Successful?}
    K -->|No| L[Retry or Choose Another Method]
    L --> I
    K -->|Yes| M[Generate Order Number]
    M --> N[Send Order Confirmation]
    N --> O[Track Order]
```

**As built:** all three order types work, with the price breakdown calculated server-side:

| Component | Rule |
| --- | --- |
| Subtotal | Σ (unit price + add-ons) × quantity |
| Service charge | 10 %, **dine-in only** |
| Tax | 6 % on (subtotal + service charge) |
| Delivery fee | RM 5.00, **delivery only** |

Payment methods are **Cash**, **Card** and **E-Wallet** (e-wallet is a mock checkout screen). The server
always recomputes totals from database prices and never trusts what the browser posts.

**Not implemented:**

- **Discount codes** — no promotions module.
- **Payment failure/retry loop** — there is no payment gateway. E-wallet is simulated as an immediate
  success, so the `{Payment Successful?}` branch never fails.
- **Order confirmation email** — no email is sent when an order is placed. (Email is used only for
  password recovery.)
- The confirmation step is an on-screen page plus a downloadable **PDF receipt**, not a notification.

### 2.5 Table Reservation ✅ Implemented

**Functions** - Select reservation date and time. - Enter number of
guests. - Check table availability. - Select an available table, if
table selection is enabled. - Enter customer details and special
requests. - Submit, modify, or cancel a reservation. - Receive
reservation confirmation.

**Workflow**

``` mermaid
flowchart TD
    A[Reservation Page] --> B[Select Date]
    B --> C[Select Time]
    C --> D[Enter Number of Guests]
    D --> E[Check Table Availability]
    E --> F{Table Available?}
    F -->|No| G[Choose Another Time or Date]
    G --> B
    F -->|Yes| H[Select Table]
    H --> I[Enter Customer Details]
    I --> J[Confirm Reservation]
    J --> K[Save Reservation]
    K --> L[Send Confirmation]
```

**As built:** `/Reservation/Create` takes date, time, guest count, an optional preferred table and special
requests. Validation rejects past dates, times outside **11:00–21:00**, and parties larger than the chosen
table's capacity.

**Conflict prevention works exactly as §8 rule 4 requires:** a booking holds its table for **120 minutes**,
and any overlapping active booking on the same table is rejected with a message naming the clashing time —
verified with genuine overlap and near-miss cases. A new reservation starts as `PENDING` and an
administrator approves, rejects, cancels or completes it.

**Not implemented:**

- **Calendar view** (§3.6) — reservations are listed and filterable by date and status, not shown on a
  calendar grid.
- **Confirmation email** — the customer sees an on-screen confirmation; no email is sent.
- Reservations are not linked to an order; a table is released by completing the order or checking out.
  Reservations can also hold a table (`Reserved` state) independently.

### 2.6 Order Tracking 🟡 Partial

Customers should be able to see the latest status of their order.

  Order Status   Description
  -------------- ------------------------------------------------------
  Pending        Order submitted and awaiting restaurant confirmation
  Confirmed      Restaurant has accepted the order
  Preparing      Kitchen is preparing the food
  Ready          Food is ready for serving or pickup
  Serving        Food is being served, where applicable
  Completed      Order has been completed
  Cancelled      Order has been cancelled

**As built:** six of the seven statuses are implemented and enforced. The customer sees live status in
**My Orders** (filterable by status) and a visual **tracking timeline** on the order detail page.

| Status | Implemented | Notes |
| --- | --- | --- |
| Pending | ✅ | Set when the order is placed |
| Confirmed | ✅ | Restaurant accepts the order |
| Preparing | ✅ | Kitchen starts; appears in the kitchen queue |
| Ready | ✅ | Food ready for serving/pickup |
| Serving | ⬜ | **Not implemented** — see below |
| Completed | ✅ | Order finished; sales records updated |
| Cancelled | ✅ | Cancellable by customer while Pending/Confirmed, or by staff |

Legal transitions (`PENDING → CONFIRMED → PREPARING → READY → COMPLETED`, with `CANCELLED` reachable from
the first three) are enforced server-side — an illegal jump such as `CONFIRMED → COMPLETED` is refused.

**Not implemented:** the **Serving** state. It was omitted deliberately: because dine-in, takeaway and
delivery all share one status field, "Serving" is only meaningful for dine-in and would leave takeaway and
delivery orders stranded at `Ready`. Adding it would require a per-order-type status model.

------------------------------------------------------------------------

## 3. Admin Dashboard Workflow

The admin dashboard is the control centre for restaurant management.

### 3.1 Admin and Staff Login 🟡 Partial

**Functions** - Admin and staff login. - Role-based access control. -
Password management. - Logout. - Restrict access to modules according to
permissions.

**Workflow**

``` mermaid
flowchart TD
    A[Login Page] --> B[Enter Username and Password]
    B --> C[Verify Account]
    C --> D{Valid Account?}
    D -->|No| E[Show Error Message]
    E --> B
    D -->|Yes| F[Check User Role]
    F --> G{User Role}
    G -->|Administrator| H[Admin Dashboard]
    G -->|Manager| I[Manager Dashboard]
    G -->|Staff| J[Staff Dashboard]
```

**As built:** there is **one** administrator path, not three. The login page's Admin tab accepts only
`ADMIN` accounts; signing in there with a member account is rejected. Every `/Admin/*` page is guarded by
`[Authorize(Roles = "ADMIN")]`, and non-admins are redirected to the access-denied page. Admins are blocked
from using the Member tab.

**Not implemented:**

- **Manager and Staff dashboards** — the roles `Manager` / `Staff` do not exist; only `ADMIN` and `MEMBER`.
- **Per-module permissions** — every admin can reach every module. There is no permission matrix.

### 3.2 Admin Dashboard 🟡 Partial

**Main dashboard information** - Total sales and revenue. - Total
orders. - Pending and completed orders. - Total reservations. -
Available, occupied, and reserved tables. - Low-stock notifications. -
Daily and monthly sales charts. - Recent transactions and activities.

**As built:** shows today's revenue (excluding cancelled orders), the count of active/pending orders, and a
table-availability breakdown (occupied vs total). Reservations and detailed counts are available on their
own pages.

**Not implemented:** low-stock notifications, sales charts, and a recent-activity feed. *(Stock levels are
visible per dish in Menu Management, but nothing alerts on them.)*

### 3.3 Order Management ✅ Implemented

**Functions** - View all customer orders. - View order details and
customer instructions. - Accept or reject orders. - Update order
status. - Send orders to the kitchen. - Print receipts. - Cancel orders
and record reasons. - View order history.

**Workflow**

``` mermaid
flowchart TD
    A[New Customer Order] --> B[Receive Order Notification]
    B --> C[Staff Reviews Order]
    C --> D{Accept Order?}
    D -->|No| E[Reject Order and Record Reason]
    D -->|Yes| F[Confirm Order]
    F --> G[Send Order to Kitchen]
    G --> H[Preparing]
    H --> I[Ready]
    I --> J[Serve, Pickup, or Delivery]
    J --> K[Complete Order]
    K --> L[Update Sales Records]
```

**As built:** `/Admin/Orders` lists every order with status filters, keyword search and expandable line
details (including add-ons, special instructions, delivery address and cancellation reason). Actions are
**transition-aware**: the page only offers buttons for legal next steps (Accept → Prepare → Ready →
Complete, or Cancel). Cancelling records a reason and restores stock. `/Admin/Kitchen` gives the kitchen a
three-column preparation board (To Prepare / Preparing / Ready).

**Partial:** *Receive Order Notification* — there is no push/live notification. New orders appear when the
Orders page is refreshed; the kitchen board has a Refresh button. *Print Receipts* — the receipt is
downloadable as a **PDF** rather than printed directly.

### 3.4 Menu Management ✅ Implemented

**Functions** - Add, edit, and deactivate food items. - Upload food
images. - Manage food categories. - Set food prices and descriptions. -
Set item availability. - Manage add-ons and customisation options. -
Manage promotions and featured items.

**Workflow**

``` mermaid
flowchart TD
    A[Menu Management] --> B[View Food List]
    B --> C{Choose Action}
    C -->|Add| D[Add New Food]
    C -->|Edit| E[Select Existing Food]
    C -->|Deactivate| F[Deactivate Food Item]
    D --> G[Enter Food Details]
    E --> G
    G --> H[Upload Food Image]
    H --> I[Set Price and Category]
    I --> J[Save Changes]
    J --> K[Update Customer Website]
```

**As built:** full CRUD for categories and dishes, image upload, price/description/category editing, stock
quantity, and an **`IsActive`** flag that hides a dish from the customer menu without losing its order
history. Dishes can have add-ons (name + price + active flag). Every change takes effect on the customer
site immediately.

**Partial:** the *add-ons* backend is complete, but add-on definitions are **seeded directly in the
database** — there is no admin screen yet for creating or editing them.

**Not implemented:** **promotions and featured items** — no promotions module exists; the home page
"top selling" carousel is derived from real order data, not curated.

### 3.5 Table Management ✅ Implemented

**Functions** - Add, edit, and deactivate tables. - Set table capacity
and location. - View table availability. - Assign customers to tables. -
Update table status. - View a visual restaurant floor plan.

**Table Status**

  Status        Description
  ------------- --------------------------
  Available     Ready for customers
  Reserved      Reserved by a customer
  Occupied      Currently in use
  Cleaning      Waiting to be cleaned
  Unavailable   Cannot currently be used

**Workflow**

``` mermaid
flowchart TD
    A[Table Management] --> B[View Table Layout]
    B --> C[Select Table]
    C --> D[View Table Details]
    D --> E{Choose Action}
    E -->|Add| F[Create New Table]
    E -->|Edit| G[Update Table Details]
    E -->|Assign| H[Assign Customer]
    E -->|Update Status| I[Change Table Status]
    F --> J[Save Changes]
    G --> J
    H --> J
    I --> J
```
**As built:** `/Admin/Tables` supports **all five statuses** listed above, plus table creation, editing of
capacity/location, and deletion (guarded — refused while occupied, holding active reservations, or
referenced by past orders). Status changes are reflected immediately on the customer Tables page. Dine-in
orders set a table to `Occupied`; completing an order moves it to `Cleaning`.

**Not implemented:**

- **Visual floor plan** — tables are managed in a list/table view, not a drag-and-drop layout.
- **Assign customers to tables** — assignment happens through the dine-in checkout flow and reservation
  approval, not as a standalone admin action.

### 3.6 Reservation Management 🟡 Partial

**Functions** - View all reservations. - Approve or reject
reservations. - Edit reservation details. - Cancel reservations. -
Assign tables. - View reservations in a calendar. - Check for
overlapping bookings.

**Workflow**

``` mermaid
flowchart TD
    A[Customer Submits Reservation] --> B[Admin Receives Notification]
    B --> C[Check Table Availability]
    C --> D{Available?}
    D -->|No| E[Suggest Alternative Date or Time]
    D -->|Yes| F[Approve Reservation]
    F --> G[Assign Table]
    G --> H[Update Table Reservation]
    H --> I[Send Confirmation]
```

**As built:** `/Admin/Reservations` lists bookings with status and date filters. Admins can **approve**,
**reject**, **cancel** or **complete** a booking, and **assign a table**. The table-clash check from §2.5 is
re-run on approval and on assignment, so a conflicting booking cannot be confirmed.

**Not implemented:**

- **Calendar view** — bookings appear in a filterable list, not a calendar grid.
- **Admin notification** of a new booking — it appears on refresh; no alert is pushed.
- **Send Confirmation** — the customer sees the status change on their own reservations page; no email is
  sent.

### 3.7 Staff Management ⬜ Not implemented

**Functions** - Add, edit, and deactivate staff accounts. - Assign staff
roles. - Set permissions. - View staff activity. - Reset staff
passwords.

**Suggested Staff Roles**

  Role            Permissions
  --------------- ------------------------------------------------
  Administrator   Full system access
  Manager         Manage restaurant operations and reports
  Cashier         Manage orders and payments
  Kitchen Staff   View orders and update food preparation status
  Waiter          Manage table service and order serving status

**As built:** a basic **User Management** module exists (`/Admin/UserManage`) — admins can create, edit,
block/unblock and delete accounts, and set the role to `ADMIN` or `MEMBER`. That covers account
administration only.

**Not implemented:** the five-role model above, per-role permissions, staff activity tracking, and
manager/cashier/kitchen/waiter dashboards. Permissions are binary: admins can do everything, members can do
nothing administrative. The kitchen workflow is served by a dedicated queue page that any admin can use.

### 3.8 Inventory Management ⬜ Not implemented

**Functions** - Add and edit ingredients. - Update ingredient
quantities. - Track stock levels. - Set minimum stock limits. - Receive
low-stock notifications. - Record stock purchases and wastage. - Track
ingredient usage.

**Workflow**

``` mermaid
flowchart TD
    A[Inventory Management] --> B[View Ingredient List]
    B --> C[Select Ingredient]
    C --> D{Choose Action}
    D -->|Add Stock| E[Record Stock Purchase]
    D -->|Update| F[Update Stock Quantity]
    D -->|View| G[View Stock Details]
    E --> H[Update Inventory]
    F --> H
    H --> I{Stock Below Minimum?}
    I -->|Yes| J[Send Low Stock Alert]
    I -->|No| K[Stock Normal]
```

**Not implemented.** There is no ingredients table, no stock transactions, and no low-stock alerting.

> **Related but different:** dish-level **`Products.StockQuantity`** *is* implemented — it is decremented
> when an order is placed, restored when an order is cancelled, and shown in Menu Management with
> per-dish stock levels. That is finished-goods tracking, not ingredient/inventory tracking. Low-stock
> warnings are not surfaced anywhere.

### 3.9 Payment Management 🟡 Partial

**Functions** - View payment transactions. - Verify payment status. -
Process refunds with permission. - View payment methods. - Generate
payment receipts. - Track daily revenue. - Reconcile cash and electronic
payments.

**Possible Payment Methods** - Cash. - Credit or debit card. - Online
banking. - E-wallet. - QR payment.

**As built:** `/Admin/Payments` lists every transaction with its method, amount, status and reference, plus
totals for collected / outstanding / refunded amounts. Each order writes a payment record with a real
lifecycle:

- **Cash** starts `UNPAID` and becomes `PAID` when the order is completed (or via "Mark Paid").
- **E-Wallet** and **Card** are recorded `PAID` immediately.
- Cancelling an already-paid order flips its payment to `REFUNDED`.
- Receipts are downloadable as **PDF** per order.

Payment methods supported: **Cash, Card, E-Wallet** — three of the five listed above.

**Not implemented:**

- **Online banking and QR payment** — not offered.
- **Refunds as an admin action with permission** — a refund is recorded automatically on cancellation; there
  is no manual "process refund" button and no permission gate.
- **Reconciliation** of cash vs electronic takings — totals are shown per status, but there is no
  reconciliation report.
- **Daily revenue tracking** — available via the Daily report (§3.10) rather than on the payments screen.
- No real payment gateway: E-Wallet and Card are recorded, never charged.

### 3.10 Reports and Analytics 🟡 Partial

**Functions** - Daily, weekly, and monthly sales reports. - Best-selling
food items. - Revenue and payment reports. - Order reports. - Inventory
reports. - Staff performance reports. - Export reports to PDF or
spreadsheet.

**As built:** four reports, each exportable to **PDF**:

| Report | Path | Contents |
| --- | --- | --- |
| Daily sales | `/Admin/Report/Daily` | Revenue, order count, average order value, items sold |
| Monthly sales | `/Admin/Report/Monthly` | Daily breakdown + top 10 items |
| Top selling | `/Admin/Report/TopSelling` | Best-selling dishes over a date range |
| Order / User exports | `/Admin/Orders`, `/Admin/UserManage` | Filtered PDF exports |

Cancelled orders are excluded from revenue in every report.

**Not implemented:** **weekly** reports, inventory reports, staff performance reports, and **spreadsheet**
(CSV/Excel) export — PDF only.

------------------------------------------------------------------------

## 4. Complete Restaurant System Workflow

This workflow connects customers, administrators, kitchen staff, and the
database.

``` mermaid
flowchart TD
    A[Start Restaurant System]
    A --> B[Customer Website]
    A --> C[Admin Dashboard]

    B --> D[Browse Menu]
    D --> E[Select Food]
    E --> F[Add to Cart]
    F --> G[Checkout]
    G --> H[Payment]

    H --> I{Payment Successful?}
    I -->|No| G
    I -->|Yes| J[Create Order]

    J --> K[(Database)]
    K --> L[Admin Receives Order]
    L --> M[Confirm Order]
    M --> N[Kitchen Receives Order]
    N --> O[Prepare Food]
    O --> P[Update Order Status]
    P --> Q[Customer Tracks Order]
    Q --> R[Food Ready]
    R --> S[Serve or Handover]
    S --> T[Complete Order]
    T --> U[Update Sales Records]
    U --> V[Generate Reports]
```

------------------------------------------------------------------------

## 5. Database Modules 🟡 Partial

The system should use a central database to store and manage
information.

  Database Table         Purpose
  ---------------------- -----------------------------------------
  `users`                Store customer and staff login accounts
  `roles`                Store user roles and permissions
  `customers`            Store customer profiles
  `categories`           Store food categories
  `products`             Store food information
  `product_addons`       Store food add-ons
  `orders`               Store customer orders
  `order_items`          Store individual items in each order
  `payments`             Store payment transaction information
  `reservations`         Store table reservations
  `tables`               Store restaurant table information
  `inventory`            Store ingredient information
  `stock_transactions`   Record stock changes
  `staff`                Store staff information
  `promotions`           Store discounts and promotions
  `notifications`        Store system notifications

### As built — actual tables

The implementation uses **PascalCase** table names (EF Core's default) and consolidates several of the
proposed tables. Twelve tables exist; see `database.md` for full column-level detail.

| Proposed table | Actual table | Status |
| --- | --- | --- |
| `users`, `customers`, `staff` | **`Users`** | ✅ Merged — one table with a `Role` column (`ADMIN`/`MEMBER`); profile data lives on the same row |
| `roles` | — | ⬜ Not implemented — role is a plain string column, no permissions table |
| `categories` | **`Categories`** | ✅ |
| `products` | **`Products`** | ✅ |
| `product_addons` | **`ProductAddons`** | ✅ (plus `OrderItemAddons`, which the design did not anticipate — it snapshots chosen add-ons so later edits don't rewrite order history) |
| `orders` | **`Orders`** | ✅ Extended with a price breakdown (`SubtotalAmount`, `ServiceCharge`, `TaxAmount`, `DeliveryFee`, `DiscountAmount`), `StatusUpdatedDate`, `CancellationReason` |
| `order_items` | **`OrderDetails`** | ✅ Plus `SpecialInstructions` |
| `payments` | **`Payments`** | ✅ |
| `reservations` | **`Reservations`** | ✅ |
| `tables` | **`Tables`** | ✅ Extended with `Capacity`, `Location`, `Status` |
| `inventory`, `stock_transactions` | — | ⬜ Not implemented |
| `promotions` | — | ⬜ Not implemented (`Orders.DiscountAmount` exists as a hook) |
| `notifications` | — | ⬜ Not implemented |

Additional table not in the design: **`Favorites`** (members saving dishes) and
**`__EFMigrationsHistory`** (EF bookkeeping).

### Database Relationship Overview

``` mermaid
erDiagram
    USERS ||--o{ ORDERS : places
    USERS ||--o{ RESERVATIONS : makes
    CATEGORIES ||--o{ PRODUCTS : contains
    ORDERS ||--|{ ORDER_ITEMS : contains
    PRODUCTS ||--o{ ORDER_ITEMS : included_in
    ORDERS ||--o{ PAYMENTS : has
    TABLES ||--o{ RESERVATIONS : assigned_to
    USERS ||--o{ NOTIFICATIONS : receives
    PRODUCTS ||--o{ PRODUCT_ADDONS : has
    INVENTORY ||--o{ STOCK_TRANSACTIONS : records
```

------------------------------------------------------------------------

## 6. Recommended Website Pages 🟡 Partial

### Customer Website

  Page                Main Functions
  ------------------- -------------------------------------------------
  Home Page           Restaurant introduction, promotions, navigation
  Menu Page           Food categories, products, prices, and details
  Cart and Checkout   Cart, order type, address, and payment
  Reservation Page    Date, time, guest count, and table booking
  My Orders           Order tracking, order history, and details
  Customer Profile    Personal details and account settings

**As built** — all six pages exist, plus three the design did not list:

| Page | Route |
| --- | --- |
| Home | `/` |
| Menu | `/Home/Menu` |
| Product detail | `/Home/ProductDetail/{id}` |
| Cart | `/Home/Cart` |
| Checkout → Payment | `/Order/Checkout` → `/Order/Payment` |
| Confirmation | `/Order/Confirmation/{id}` |
| My Orders | `/Order/History` (+ `/Order/Details/{id}`) |
| Reservations | `/Reservation/Index` |
| Profile | `/Account/Profile` (+ `EditProfile`, `ResetPassword`) |
| **Favourites** *(not in design)* | `/Home/Favorites` |
| **Live tables** *(not in design)* | `/Home/Tables` |
| Login / Register | `/Account/Login` (Member/Admin switch), `/Account/Register` |

### Admin Dashboard

  -----------------------------------------------------------------------
  Page                                Main Functions
  ----------------------------------- -----------------------------------
  Dashboard                           Sales overview, statistics, and
                                      notifications

  Order Management                    Incoming orders, order details, and
                                      status

  Kitchen Orders                      Food preparation queue and status
                                      updates

  Menu Management                     Food items, categories, prices, and
                                      promotions

  Table Management                    Table layout, status, and
                                      availability

  Reservation Management              Bookings, calendar, and table
                                      assignment

  Staff Management                    Staff accounts, roles, and
                                      permissions

  Inventory Management                Ingredients, stock levels, and
                                      alerts

  Payment Management                  Transactions, receipts, and refunds

  Reports                             Sales, revenue, products, and
                                      performance

  System Settings                     Restaurant details, opening hours,
                                      tax, and service charges
  -----------------------------------------------------------------------

**As built** — nine admin pages are live:

| Page | Route | Note |
| --- | --- | --- |
| Dashboard | `/Admin/Dashboard` | ✅ |
| Order Management | `/Admin/Orders` | ✅ Includes status filters, search, PDF export |
| Kitchen Orders | `/Admin/Kitchen` | ✅ Three-column board |
| Menu Management | `/Admin/Categories`, `/Admin/Products` | ✅ Promotions not implemented |
| Table Management | `/Admin/Tables` | 🟡 No floor plan |
| Reservation Management | `/Admin/Reservations` | 🟡 No calendar |
| Staff Management | `/Admin/UserManage` | 🟡 Accounts only, no roles/permissions |
| Inventory Management | — | ⬜ |
| Payment Management | `/Admin/Payments` | 🟡 No refund action or reconciliation |
| Reports | `/Admin/Report` (+ Daily / Monthly / TopSelling) | 🟡 PDF only |
| **System Settings** | — | ⬜ Not implemented — tax, service charge and delivery fee are configuration values (`appsettings.json`), not an admin screen |

------------------------------------------------------------------------

## 7. Recommended Development Priority 🟡 Phase 1 & 2 core complete

Develop the system in phases to keep the first version manageable.

  -----------------------------------------------------------------------
  Phase                   Modules                 Priority
  ----------------------- ----------------------- -----------------------
  Phase 1                 Login, customer         Essential
                          website, menu, cart,    
                          ordering, admin         
                          dashboard, order        
                          management              

  Phase 2                 Reservations, table     Important
                          management, payments,   
                          staff management        

  Phase 3                 Inventory, promotions,  Advanced
                          analytics,              
                          notifications, advanced 
                          reports                 
  -----------------------------------------------------------------------

### Actual progress

| Phase | Status |
| --- | --- |
| **Phase 1** (Essential) | ✅ **Complete** — login with Member/Admin split, customer site, menu, cart, ordering, admin dashboard, order management + kitchen queue |
| **Phase 2** (Important) | 🟡 **Core complete** — reservations ✅, table management ✅, payments 🟡 (no manual refund/reconciliation), staff management ⬜ (accounts only, no roles) |
| **Phase 3** (Advanced) | ⬜ **Not started** — inventory, promotions, notifications, activity log; advanced reports partially covered by Daily/Monthly/Top-selling |

Beyond the plan, the build also added: **order add-ons**, **special instructions**, **three order types with
a real price breakdown**, **order tracking timeline**, **a kitchen queue**, and **PDF receipts/reports**.

------------------------------------------------------------------------

## 8. Important System Interaction Rules

1.  When a customer submits an order, the admin or order-management
    screen should receive a new-order notification.
2.  When an order is accepted, the kitchen should receive the order
    details.
3.  When kitchen staff update the order status, the customer should see
    the latest status.
4.  The system should prevent conflicting reservations for the same
    table and time.
5.  When an order is completed, the system should update sales records.
6.  When an ingredient falls below its minimum stock level, the system
    should notify authorised staff.
7.  When payment fails, the system must not mark the order as paid.
8.  Staff should only access functions permitted by their roles.
9.  Important actions, such as refunds, cancellations, and price
    changes, should be recorded in an activity log.
10. The system should validate prices, availability, and totals on the
    server before confirming an order.

### Rule compliance

| # | Rule | Status | How it is met |
| --- | --- | --- | --- |
| 1 | New-order notification | 🟡 | New orders appear on the Orders page and Kitchen board on refresh; no push notification |
| 2 | Kitchen receives accepted order | ✅ | Accepting moves the order to `CONFIRMED`; it appears in `/Admin/Kitchen` with full details |
| 3 | Customer sees latest status | ✅ | My Orders + tracking timeline reflect every status change |
| 4 | Prevent conflicting reservations | ✅ | 120-minute hold per table; overlaps rejected on booking **and** re-checked on admin approval |
| 5 | Sales records update on completion | ✅ | Completed orders feed the Daily/Monthly/Top-selling reports; cancelled orders are excluded from revenue |
| 6 | Low-stock notification | ⬜ | No ingredient/inventory module; dish stock levels exist but nothing alerts |
| 7 | Failed payment must not mark order paid | ✅ | Payments are only recorded on successful order creation; a failed order never reaches that point |
| 8 | Staff only access permitted functions | 🟡 | Binary only: `ADMIN` vs `MEMBER`. No per-role permissions |
| 9 | Activity log for refunds/cancellations/price changes | 🟡 | Cancellations store a `CancellationReason` and payments record `REFUNDED`, but there is no general audit log of price edits |
| 10 | Server-side price/availability/total validation | ✅ | `OrderController.CreateOrder` recalculates from database prices and ignores posted totals; stock and table availability re-checked before payment |

------------------------------------------------------------------------

## 9. Recommended System Architecture

``` mermaid
flowchart TD
    A[Frontend - Website]
    A --> B[Customer Website]
    A --> C[Admin Dashboard]

    B --> D[Backend - System Logic]
    C --> D

    D --> E[Authentication and Permissions]
    D --> F[Order Management]
    D --> G[Menu Management]
    D --> H[Reservation and Table Management]
    D --> I[Payment Management]
    D --> J[Inventory and Reporting]

    E --> K[(Central Database)]
    F --> K
    G --> K
    H --> K
    I --> K
    J --> K

    D --> L[External Services]
    L --> M[Payment Gateway]
    L --> N[Email and Notifications]
```

**Recommended approach:** Build a desktop-first restaurant management
website with a responsive customer interface and a separate admin
dashboard. Start with the ordering and order-management workflow because
these are the core functions connecting customers, kitchen staff, and
restaurant administrators.

### As built

The recommended approach was followed: a single ASP.NET Core MVC app with a responsive customer site and a
separate admin dashboard, built around the ordering → order-management loop first.

| Component | Implementation |
| --- | --- |
| Frontend | Server-rendered Razor views; Bootstrap 5 + a custom emerald/gold design system; inline JS per page |
| Backend | ASP.NET Core MVC controllers (`Home`, `Account`, `Order`, `Reservation`, `Admin`) |
| Authentication | Cookie authentication, roles `ADMIN` / `MEMBER`, separate Member and Admin login portals |
| Central database | SQL Server Express LocalDB, single `DB.mdf` attached via `\|DataDirectory\|` |
| Email | **MailKit SMTP** — used for password recovery only, not notifications |
| Payment gateway | ⬜ **None** — no external gateway is integrated; Card/E-Wallet are recorded, never charged |
| Reporting | QuestPDF generates receipts and report PDFs server-side |

## 10. Suggested Future Enhancements

After the core system is working, consider adding: - Kitchen Display
System (KDS) for real-time kitchen order queues. - Point of Sale (POS)
integration for walk-in customers. - QR-code ordering at tables. -
Loyalty points and customer rewards. - Automated email or messaging
notifications. - Multi-branch restaurant management. - Ingredient
deduction based on recipes. - Sales forecasting and advanced analytics.

### As built

| Enhancement | Status | Note |
| --- | --- | --- |
| Kitchen Display System (KDS) | 🟡 Partial | A polling `/Admin/Kitchen` board exists; it is not real-time (refresh required) |
| Point of Sale (POS) | ⬜ | Not built |
| QR-code ordering at tables | ⬜ | Not built |
| Loyalty points / rewards | ⬜ | Not built |
| Automated notifications | ⬜ | Only password-recovery email exists |
| Multi-branch management | ⬜ | Single restaurant |
| Ingredient deduction by recipe | ⬜ | No recipes or ingredients modelled |
| Sales forecasting / advanced analytics | ⬜ | Fixed reports only |

### Recommended next steps

Ranked by value relative to effort, based on what the build revealed:

1. **Promotions module** — the only thing blocking the "apply discount code" requirement in §2.4;
   `Orders.DiscountAmount` already exists as the hook.
2. **Real-time kitchen updates** — SignalR would turn the polling board into a genuine KDS.
3. **Per-role permissions** — would deliver §3.7 and make rule 8 fully satisfied.
4. **Ingredient inventory** — the largest remaining module; would also enable rule 6.
5. **Admin screen for add-ons** — the data model is complete, only the UI is missing.
