# Inventory Management System (C# Windows Forms)

Vincent Canayan, BSIT-2A. A Windows desktop application written in C# (.NET 8, Windows Forms) that implements the modules in the project WBS. No third-party packages: data is stored in one JSON file.

## Open and run it in Visual Studio

1. You need **Visual Studio 2022 version 17.8 or newer** with the **.NET desktop development** workload (Tools > Get Tools and Features). Older Visual Studio: open `InventoryManagement.csproj` in a text editor and change `net8.0-windows` to the version you have, for example `net6.0-windows`.
2. Unzip the project. In Visual Studio choose **File > Open > Project/Solution** and pick `InventoryManagement.csproj`.
3. Press **F5** (or the green Start button) to build and run.
4. Log in with **admin / admin123** the first time, then change the password under *Account > Change my password*.
5. For sample data: *Help > Load sample data* (works on an empty database only).
6. For tests: *Help > Run self-test* (runs about 55 checks on a temporary copy, your data is not touched).

The screens are built in code, so there is no Form Designer view for them. Everything is in plain `.cs` files you can read and edit.

Your data is saved in **Documents\InventoryManagementSystem** (`inventory.json`, a `backups` folder, and a `reports` folder for CSV exports). *Administration > Open data folder* opens it.

## Screens

| Tab | What it does |
|---|---|
| Dashboard | Totals, low and out-of-stock counts, open orders, top products by value, recent transactions |
| Products | Search, register, edit, delete, assign supplier. LOW rows are yellow, OUT rows are red |
| Suppliers | Add, edit, delete suppliers; lower table shows that supplier's products |
| Stock In / Out | Receive stock (needs a supplier and a delivery or PO number), issue stock (cannot go below zero) |
| Transaction History | Filter by product, type and dates; export CSV |
| Reorder | Products at or below reorder level with a suggested quantity; create purchase orders |
| Purchase Orders | New order, Mark as Ordered, Receive (adds stock), Cancel |
| Reports | Stock levels, low stock, valuation by category, stock movement, purchase orders; export CSV |

## Roles

| Action | Admin | Staff |
|---|---|---|
| View everything, run reports | yes | yes |
| Register products, stock in/out, create and receive purchase orders | yes | yes |
| Edit/delete products, assign suppliers | yes | no |
| Add/edit/delete suppliers | yes | no |
| Mark orders Ordered, cancel orders | yes | no |
| Add users, backup, restore | yes | no |
| Change own password | yes | yes |

Staff see the Admin buttons greyed out, and the services refuse the action even if it were attempted.

## WBS coverage

| WBS task | Where it is implemented |
|---|---|
| 1 Set up the system | `InventoryApp.cs`, `Program.cs`, `.csproj` |
| 2 Design/create product database | `Models.cs`, `Database.cs` (JSON file, atomic saves) |
| 3 Product registration | `ProductService.Add`, Products tab |
| 4 Product search and listing | `ProductService.Search / List`, Products tab |
| 5 Supplier management | `SupplierService`, Suppliers tab |
| 6 Connect products and suppliers | `ProductService.AssignSupplier`, `Product.SupplierId` |
| 7 Stock-in / receiving | `StockService.StockIn`, Stock tab |
| 8 Stock-in validation | `StockService` (quantity range, reference required, supplier required, duplicate reference blocked, max stock level) |
| 9 Stock-out | `StockService.StockOut` |
| 10 Transaction history | `StockService.History`, History tab |
| 11 Reorder level monitoring | `ReorderService.IsLow / StatusOf / LowStock` |
| 12 Reorder recommendation | `ReorderService.Recommend`, Reorder tab |
| 13 Purchase / restocking workflow | `PurchaseService`, Purchase Orders tab |
| 14 Inventory reports | `ReportService`, Reports tab (CSV export) |
| 15 Dashboard | `ReportService.Dashboard`, `DashboardPage.cs` |
| 16 Authentication and roles | `AuthService`, `LoginForm.cs` (PBKDF2-hashed passwords) |
| 17 Integrate all modules | `MainForm.cs`, `InventoryApp.cs` |
| 18 Data validation and error handling | `Guard`, `InventoryException`, `Ui.Run` (shows a message instead of crashing) |
| 19-22 Testing | `SelfTest.cs` (Help > Run self-test) plus the checklist below |
| 23 Bug tracking and refinement | Add each failing case as a new `Check` / `Throws` line in `SelfTest.cs` |
| 24 Backup and recovery | `Database.Backup / Restore`, Administration menu |
| 25-26 Deployment | Visual Studio: **Build > Publish Selection**, or `dotnet publish -c Release -o publish`; copy the folder to another Windows PC that has the .NET 8 Desktop Runtime |
| 27 User manual | sections above and below |
| 28 Final demonstration | demo script below |

## Acceptance test checklist (task 22)

- [ ] Log in as admin, add a Staff user, log in as that user, confirm Admin buttons are disabled
- [ ] Register 3 products with a supplier, receive stock, issue stock, confirm balances
- [ ] Try to issue more than is on hand and confirm it is refused
- [ ] Drop a product to its reorder level, confirm the LOW color and warning, create a purchase order, mark Ordered, receive it
- [ ] Export the stock-levels report to CSV and open it in Excel
- [ ] Create a backup, change some data, restore, confirm the data came back
- [ ] Close and reopen the program, confirm the data is still there

## Demo script (task 28)

Start the app, log in as admin, *Help > Load sample data*. Show: Dashboard, Products (colored rows), Stock tab (issue stock until LOW), Reorder tab (create orders), Purchase Orders (mark Ordered, Receive), Transaction History, Reports with CSV export, *Administration > Create database backup*, *Help > Run self-test*.

## Project layout

```
Program.cs          entry point, login loop
LoginForm.cs        login window
MainForm.cs         main window, tabs, menus
DashboardPage.cs, ProductsPage.cs, SuppliersPage.cs, StockPage.cs,
HistoryPage.cs, ReorderPage.cs, PurchasePage.cs, ReportsPage.cs   one file per tab
UiKit.cs            shared helpers, pop-up dialogs, base page class
InventoryApp.cs     wires the services together, sample data
Models.cs           data classes, validation helpers
Database.cs         JSON storage, backup, restore
AuthService.cs      login, roles, password hashing
CatalogServices.cs  products and suppliers
StockServices.cs    stock in/out, history, reorder, purchase orders
ReportService.cs    reports, CSV, dashboard figures
SelfTest.cs         built-in test suite
```
