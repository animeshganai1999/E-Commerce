# ECommerceBackend

A modular backend API for an e-commerce platform, built with ASP.NET Core (.NET 8), Entity Framework Core, and supporting features such as authentication, cart management, checkout, invoice generation, and email notifications.

---

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [API Endpoints](#api-endpoints)
- [Database](#database)
- [Development](#development)
- [License](#license)

---

## Features

- **User Authentication**: Register, login, and refresh JWT tokens.
- **Cart Management**: Add, update, remove, and fetch cart items.
- **Checkout & Invoice**: Generate PDF invoices, send via email, and persist invoice data.
- **Email Service**: Send contact and invoice emails.
- **Entity Framework Core**: SQL Server database integration with migrations.
- **Swagger**: API documentation and testing.

---

## Tech Stack

- **.NET 8**
- **ASP.NET Core Web API**
- **Entity Framework Core (EF Core)**
- **AutoMapper**
- **JWT Authentication**
- **QuestPDF** (PDF generation)
- **Azure Storage Blobs** (optional, for file storage)
- **Swashbuckle** (Swagger UI)

---

## Project Structure

- **Controllers**: Handle HTTP requests (e.g., Auth, Cart, Checkout, Email, OrderedItems).
- **Services**: Business logic (e.g., AuthService, CartService, CheckoutService, OrderedItemService).
- **Repositories**: Data access (e.g., UserRepository, CartRepository, InvoiceRepository).
- **Entities**: Core domain models (e.g., User, CartItem, UserInvoice, RefreshToken).
- **DTOs/Models**: Data transfer objects and request/response models.

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
- (Optional) Azure account for Blob Storage

### Setup

1. **Clone the repository**
    ```sh
    git clone <your-repo-url>
    cd ECommerceBackend
    ```

2. **Configure the database connection**
    - Update `ECommerceBackend.API/appsettings.json` with your SQL Server connection string.

3. **Apply EF Core Migrations**
    ```sh
    dotnet ef database update --project ECommerceBackend.Infrastructure
    ```

4. **Run the API**
    ```sh
    dotnet run --project ECommerceBackend.API
    ```

5. **Access Swagger UI**
    - Navigate to `http://localhost:<port>/swagger` for interactive API documentation.

---

## Configuration

- **appsettings.json**: Contains connection strings, email settings, JWT secrets, etc.
- **Environment Variables**: Can override sensitive settings for production.

Example `appsettings.json` section:

---

## API Endpoints

### Auth

- `POST /api/auth/login` — User login
- `POST /api/auth/register` — User registration
- `POST /api/auth/refresh-token` — Refresh JWT token

### Cart

- `POST /api/cart/update` — Update cart items
- `GET /api/cart/getItems?userId={userId}` — Get cart items for a user

### Checkout

- `POST /api/checkout/generate-invoice` — Generate invoice, send email, and save invoice

### Email

- `POST /api/email/send` — Send contact email

### Ordered Items

- `GET /api/ordereditems/getInvoices?userId={userId}` — Get all invoices for a user

---

## Database

- **Entities**: User, CartItem, RefreshToken, UserInvoice
- **Migrations**: Managed via EF Core, located in `ECommerceBackend.Infrastructure/Migrations`
- **DbContext**: `AppDbContext` configures all entity sets and relationships.

---

## Development

- **Hot Reload**: Supported via Visual Studio 2022 or `dotnet watch`.
- **Testing**: Add unit/integration tests in a separate test project.
- **Swagger**: Enabled for API exploration.

---

## License

This project is licensed under the MIT License.

---

## Contact

For questions or support, please open an issue or contact the maintainer.
