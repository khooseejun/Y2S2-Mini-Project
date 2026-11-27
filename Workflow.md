# Restaurant Management System -- Function Workflow Design

## 1. System Overview

The Restaurant Management System is a web-based application for managing
restaurant operations. It has two main interfaces:

1.  **Customer Website** -- Customers browse menus, place orders, make
    reservations, pay, and track orders.
2.  **Admin Dashboard** -- Administrators and staff manage orders,
    menus, tables, reservations, payments, inventory, staff, and
    reports.

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

------------------------------------------------------------------------

## 2. Customer Website Workflow

The customer website should be simple, attractive, and easy to navigate.

### 2.1 Customer Main Page

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

### 2.2 Customer Registration and Login

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

### 2.3 Food Ordering System

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

### 2.4 Checkout and Payment

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

### 2.5 Table Reservation

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

### 2.6 Order Tracking

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

------------------------------------------------------------------------

## 3. Admin Dashboard Workflow

The admin dashboard is the control centre for restaurant management.

### 3.1 Admin and Staff Login

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

### 3.2 Admin Dashboard

**Main dashboard information** - Total sales and revenue. - Total
orders. - Pending and completed orders. - Total reservations. -
Available, occupied, and reserved tables. - Low-stock notifications. -
Daily and monthly sales charts. - Recent transactions and activities.

### 3.3 Order Management

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

### 3.4 Menu Management

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

### 3.5 Table Management

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

### 3.6 Reservation Management

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

### 3.7 Staff Management

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

### 3.8 Inventory Management

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

### 3.9 Payment Management

**Functions** - View payment transactions. - Verify payment status. -
Process refunds with permission. - View payment methods. - Generate
payment receipts. - Track daily revenue. - Reconcile cash and electronic
payments.

**Possible Payment Methods** - Cash. - Credit or debit card. - Online
banking. - E-wallet. - QR payment.

### 3.10 Reports and Analytics

**Functions** - Daily, weekly, and monthly sales reports. - Best-selling
food items. - Revenue and payment reports. - Order reports. - Inventory
reports. - Staff performance reports. - Export reports to PDF or
spreadsheet.

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

## 5. Database Modules

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

## 6. Recommended Website Pages

### Customer Website

  Page                Main Functions
  ------------------- -------------------------------------------------
  Home Page           Restaurant introduction, promotions, navigation
  Menu Page           Food categories, products, prices, and details
  Cart and Checkout   Cart, order type, address, and payment
  Reservation Page    Date, time, guest count, and table booking
  My Orders           Order tracking, order history, and details
  Customer Profile    Personal details and account settings

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

------------------------------------------------------------------------

## 7. Recommended Development Priority

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

## 10. Suggested Future Enhancements

After the core system is working, consider adding: - Kitchen Display
System (KDS) for real-time kitchen order queues. - Point of Sale (POS)
integration for walk-in customers. - QR-code ordering at tables. -
Loyalty points and customer rewards. - Automated email or messaging
notifications. - Multi-branch restaurant management. - Ingredient
deduction based on recipes. - Sales forecasting and advanced analytics.
