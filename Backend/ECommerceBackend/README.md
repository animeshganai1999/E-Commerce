# E-Commerce Backend API

A secure, scalable, and feature-rich backend API for an e-commerce platform built with **ASP.NET Core (.NET 8)**. This project demonstrates modern backend architecture with clean code principles, JWT authentication, rate limiting, and comprehensive security features.

---

## ?? Features

### Core Functionality
- **?? JWT Authentication & Authorization**
  - Secure user registration and login
  - Access token (15-minute expiry) & refresh token (7-day expiry)
  - HTTP-only cookie-based refresh token storage
  - Token rotation for enhanced security

- **?? Shopping Cart Management**
  - Add, update, and remove cart items
  - Per-user cart isolation
  - Real-time cart synchronization

- **?? Checkout & Payment Flow**
  - Generate professional PDF invoices using QuestPDF
  - Email invoices to customers
  - Store invoice metadata in database

- **?? Email Notifications**
  - Invoice delivery via Gmail SMTP
  - Contact form submissions
  - Configurable email templates

- **?? Order Management**
  - Track customer orders and invoices
  - Secure invoice storage in Azure Blob Storage
  - Invoice history retrieval

### Security Features
- **??? Rate Limiting**
  - Per-IP rate limiting to prevent abuse
  - Sliding window algorithm for authentication endpoints
  - Fixed window for general API endpoints
  - Customizable limits per endpoint

- **?? Security Best Practices**
  - Password hashing using ASP.NET Core Identity
  - CORS configuration for frontend integration
  - HTTPS enforcement
  - Input validation and model binding
  - JWT signature verification with Issuer/Audience validation

---

## ??? Tech Stack

| Category | Technology |
|----------|-----------|
| **Framework** | .NET 8, ASP.NET Core Web API |
| **Database** | SQL Server, Entity Framework Core 8 |
| **Authentication** | JWT Bearer Tokens |
| **PDF Generation** | QuestPDF |
| **Cloud Storage** | Azure Blob Storage |
| **Email** | SMTP (Gmail) |
| **API Documentation** | Swagger/OpenAPI |
| **Object Mapping** | AutoMapper |
| **Security** | ASP.NET Core Rate Limiting, CORS |

---

## ?? Project Structure

```
ECommerceBackend/
??? ECommerceBackend.API/              # Web API layer
?   ??? Controllers/                   # API endpoints
?   ?   ??? AuthController.cs          # Authentication endpoints
?   ?   ??? CartController.cs          # Cart management
?   ?   ??? CheckoutController.cs      # Order checkout
?   ?   ??? EmailController.cs         # Email services
?   ?   ??? OrderedItemsController.cs  # Invoice retrieval
?   ??? Configuration/                 # AutoMapper profiles
?   ?   ??? AutoMapperConfig.cs        # DTO mappings
?   ??? Program.cs                     # Application startup & middleware
?   ??? appsettings.json              # Configuration settings
?
??? ECommerceBackend.Application/      # Business logic layer
?   ??? Services/                      # Service implementations
?   ?   ??? AuthService.cs             # Authentication logic
?   ?   ??? CartService.cs             # Cart operations
?   ?   ??? CheckoutService.cs         # Invoice generation
?   ?   ??? EmailService.cs            # Email sending
?   ?   ??? OrderedItemService.cs      # Invoice management
?   ??? Interfaces/                    # Service contracts
?   ?   ??? IAuthService.cs
?   ?   ??? ICartService.cs
?   ?   ??? ICheckoutService.cs
?   ?   ??? IEmailService.cs
?   ?   ??? IOrderedItemService.cs
?   ??? DTOs/                          # Data transfer objects
?   ?   ??? AuthResponse.cs
?   ?   ??? CartItemDTO.cs
?   ?   ??? CartDiffDTO.cs
?   ?   ??? CartItemResponseDTO.cs
?   ?   ??? UserDTO.cs
?   ??? Models/                        # Request/response models
?   ?   ??? LoginModel.cs
?   ?   ??? RegisterModel.cs
?   ?   ??? InvoiceDataModel.cs
?   ?   ??? OrderItem.cs
?   ?   ??? OrderDetails.cs
?   ??? Factory/                       # Object creation patterns
?       ??? RefreshTokenFactory.cs
?
??? ECommerceBackend.Domain/           # Domain entities
?   ??? Entities/                      # Core business entities
?       ??? User.cs                    # User entity
?       ??? CartItem.cs                # Shopping cart item
?       ??? RefreshToken.cs            # JWT refresh token
?       ??? UserInvoice.cs             # Invoice metadata
?
??? ECommerceBackend.Infrastructure/   # Data access layer
    ??? Data/                          # EF Core context & configurations
    ?   ??? AppDbContext.cs            # Database context
    ?   ??? Config/                    # Entity configurations
    ?       ??? UserConfig.cs
    ?       ??? CartItemConfig.cs
    ?       ??? RefreshTokenConfig.cs
    ?       ??? UserInvoiceConfig.cs
    ??? Repositories/                  # Data access repositories
    ?   ??? IRepository.cs             # Generic repository interface
    ?   ??? Repository.cs              # Generic repository implementation
    ?   ??? IUserRepository.cs
    ?   ??? UserRepository.cs
    ?   ??? ICartRepository.cs
    ?   ??? CartRepository.cs
    ?   ??? ITokenRepository.cs
    ?   ??? TokenRepository.cs
    ?   ??? IInvoiceRepository.cs
    ?   ??? InvoiceRepository.cs
    ??? Migrations/                    # EF Core migrations
```

---

## ?? Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server 2019+](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) or SQL Server Express
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- (Optional) [Azure Storage Account](https://azure.microsoft.com/en-us/products/storage/blobs) for invoice storage
- Gmail account with App Password for email functionality

### Installation

1. **Clone the repository**

```bash
git clone https://github.com/animeshganai1999/E-Commerce.git
cd E-Commerce/Backend/ECommerceBackend
```

2. **Configure the database connection**

Update `ECommerceBackend.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "ECommerceBackendDBConnection": "Server=YOUR_SERVER;Database=ECommerceDb;Integrated Security=True;Trust Server Certificate=True"
  }
}
```

3. **Configure application settings**

Update `appsettings.json` with your settings:

```json
{
  "Jwt": {
    "Issuer": "ecommerce-backend-api",
    "Audience": "ecommerce-frontend-app",
    "Secret": "YOUR_SECRET_KEY_HERE_AT_LEAST_32_CHARACTERS",
    "AccessTokenExpiryMinutes": 15
  },
  "EmailSettings": {
    "SenderEmail": "your-email@gmail.com",
    "AppPassword": "your-app-password",
    "ReceiverEmail": "recipient@example.com"
  },
  "AzureBlobStorage": {
    "ConnectionString": "YOUR_AZURE_CONNECTION_STRING",
    "ContainerName": "customerinvoice"
  }
}
```

**?? Security Warning**: Never commit `appsettings.json` with real credentials to version control!

**Use User Secrets for development:**

```bash
dotnet user-secrets init --project ECommerceBackend.API
dotnet user-secrets set "Jwt:Secret" "your-secret-key" --project ECommerceBackend.API
dotnet user-secrets set "EmailSettings:AppPassword" "your-app-password" --project ECommerceBackend.API
dotnet user-secrets set "AzureBlobStorage:ConnectionString" "your-connection-string" --project ECommerceBackend.API
```

4. **Apply database migrations**

```bash
cd ECommerceBackend.Infrastructure
dotnet ef database update
```

Or from the solution root:

```bash
dotnet ef database update --project ECommerceBackend.Infrastructure --startup-project ECommerceBackend.API
```

5. **Run the application**

```bash
cd ECommerceBackend.API
dotnet run
```

The API will start on:
- **HTTPS**: `https://localhost:7244`
- **HTTP**: `http://localhost:5274`

6. **Access Swagger UI**

Navigate to **`https://localhost:7244/swagger`** for interactive API documentation.

---

## ?? Configuration

### Email Settings (Gmail SMTP)

1. Enable 2-Step Verification in your Google Account
2. Generate an [App Password](https://support.google.com/accounts/answer/185833)
3. Use the App Password in `appsettings.json` or User Secrets

### Azure Blob Storage Setup

1. Create an Azure Storage Account
2. Create a container named `customerinvoice` (or your preferred name)
3. Copy the connection string to `appsettings.json`

### JWT Configuration

- **Issuer**: Identifies your API (e.g., `"ecommerce-backend-api"`)
- **Audience**: Identifies your frontend (e.g., `"ecommerce-frontend-app"`)
- **Secret**: Strong secret key (minimum 32 characters, use a random generator)
- **AccessTokenExpiryMinutes**: Token lifetime (default: 15 minutes)

### Rate Limiting Configuration

Configured in `Program.cs` with per-IP partitioning:

| Policy | Endpoint | Limit | Window | Algorithm |
|--------|----------|-------|--------|-----------|
| `auth` | Login/Register | 5 requests | 1 minute | Sliding Window |
| `refresh` | Refresh Token | 10 requests | 1 minute | Fixed Window |
| `api` | General API | 30 requests | 1 minute | Fixed Window |
| Global | All endpoints | 100 requests | 1 minute | Fixed Window |

### CORS Configuration

Update `Program.cs` to match your frontend URL:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder.WithOrigins("http://localhost:3000") // Your frontend URL
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});
```

---

## ?? API Endpoints

### Authentication

| Method | Endpoint | Description | Rate Limit | Auth Required |
|--------|----------|-------------|------------|---------------|
| `POST` | `/api/Auth/login` | User login | 5/min | ? |
| `POST` | `/api/Auth/register` | User registration | 5/min | ? |
| `POST` | `/api/Auth/refresh-token` | Refresh access token | 10/min | ? |

**Request Body (Login):**
```json
{
  "Email": "user@example.com",
  "Password": "SecurePassword123!"
}
```

**Response:**
```json
{
  "AccessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "UserId": "5cbada5b-3fd1-4241-b919-900be90765e3"
}
```

**Note**: Refresh token is automatically set in an HTTP-only cookie.

### Cart Management

| Method | Endpoint | Description | Rate Limit | Auth Required |
|--------|----------|-------------|------------|---------------|
| `POST` | `/api/Cart/update` | Update cart items | 30/min | ? |
| `GET` | `/api/Cart/getItems` | Get user's cart | 30/min | ? |

**Request Body (Update Cart):**
```json
{
  "ItemsToAdd": [
    {
      "ProductId": "abc123",
      "Quantity": 2,
      "Price": 29.99
    }
  ],
  "ItemsToUpdate": [
    {
      "CartItemId": 1,
      "Quantity": 3
    }
  ],
  "ItemsToDelete": [2, 3]
}
```

**Note**: `UserId` is automatically extracted from JWT token claims.

### Checkout

| Method | Endpoint | Description | Rate Limit | Auth Required |
|--------|----------|-------------|------------|---------------|
| `POST` | `/api/Checkout/generate-invoice` | Generate & email invoice | 30/min | ? |

**Request Body:**
```json
{
  "OrderDetails": {
    "Name": "John Doe",
    "Email": "john@example.com",
    "Address": "123 Main St, City, State 12345",
    "Phone": "+1234567890"
  }
}
```

**Response:**
```json
{
  "message": "Order placed and Invoice sent successfully over Email."
}
```

### Orders & Invoices

| Method | Endpoint | Description | Rate Limit | Auth Required |
|--------|----------|-------------|------------|---------------|
| `GET` | `/api/OrderedItems/get-invoice` | Get user's invoices | 30/min | ? |

**Response:**
```json
[
  {
    "Id": 1,
    "UserId": "5cbada5b-3fd1-4241-b919-900be90765e3",
    "InvoiceDate": "2025-12-21T10:00:00Z",
    "NumberOfItems": 3,
    "TotalAmount": 89.97,
    "InvoiceLink": "https://ecommerceinvoice.blob.core.windows.net/customerinvoice/2025/12/21/Invoice_5cbada5b_20251221100000.pdf"
  }
]
```

### Email

| Method | Endpoint | Description | Rate Limit | Auth Required |
|--------|----------|-------------|------------|---------------|
| `POST` | `/api/Email/send` | Send contact email | 30/min | ? |

**Request Body:**
```json
{
  "Name": "Jane Doe",
  "Email": "jane@example.com",
  "Message": "Hello, I have a question..."
}
```

---

## ??? Database Schema

### Users Table
```sql
CREATE TABLE Users (
    UserId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);
```

### CartItems Table
```sql
CREATE TABLE CartItems (
    CartItemId INT PRIMARY KEY IDENTITY(1,1),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ProductId NVARCHAR(100) NOT NULL,
    Quantity INT NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
```

### RefreshTokens Table
```sql
CREATE TABLE RefreshTokens (
    TokenId INT PRIMARY KEY IDENTITY(1,1),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Token NVARCHAR(MAX) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    UserAgent NVARCHAR(500),
    FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
```

### UserInvoices Table
```sql
CREATE TABLE UserInvoices (
    Id INT PRIMARY KEY IDENTITY(1,1),
    UserId UNIQUEIDENTIFIER NOT NULL,
    InvoiceDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    NumberOfItems INT NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    InvoiceLink NVARCHAR(MAX) NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
```

---

## ?? Security Features

### Authentication & Authorization Flow

1. **Registration/Login**
   - User provides email and password
   - Backend hashes password using ASP.NET Core Identity
   - Generates JWT access token (15 min) and refresh token (7 days)
   - Refresh token stored in HTTP-only cookie

2. **Making Authenticated Requests**
   - Frontend sends access token in `Authorization: Bearer {token}` header
   - Backend JWT middleware validates:
     - Signature (using secret key)
     - Expiration time
     - Issuer and Audience claims
   - Extracts user ID from `ClaimTypes.NameIdentifier` claim

3. **Token Refresh**
   - When access token expires, frontend calls `/api/Auth/refresh-token`
   - Backend validates refresh token from cookie
   - Generates new access token and refresh token
   - Old refresh token is replaced (token rotation)

### JWT Token Structure

```
Header:
{
  "alg": "HS256",
  "typ": "JWT"
}

Payload:
{
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "user@example.com",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "5cbada5b-3fd1-4241-b919-900be90765e3",
  "exp": 1766256428,
  "iss": "ecommerce-backend-api",
  "aud": "ecommerce-frontend-app"
}

Signature:
HMACSHA256(
  base64UrlEncode(header) + "." + base64UrlEncode(payload),
  secret
)
```

### Security Best Practices Implemented

- ? **Password Hashing**: Using ASP.NET Core Identity's `PasswordHasher`
- ? **JWT Signature Verification**: Every token is cryptographically verified
- ? **Token Expiration**: Short-lived access tokens (15 minutes)
- ? **Refresh Token Rotation**: New refresh token on each refresh
- ? **HTTP-Only Cookies**: Prevents XSS attacks on refresh tokens
- ? **Secure Cookies**: Only transmitted over HTTPS
- ? **SameSite=None**: Allows cross-origin requests with credentials
- ? **CORS Restrictions**: Configurable allowed origins
- ? **Rate Limiting**: Per-IP address to prevent brute force
- ? **Issuer/Audience Validation**: Prevents token misuse across applications
- ? **User ID Extraction from JWT**: Controllers get user ID from token, not request params
- ? **Input Validation**: Model validation via `[ApiController]` attribute
- ? **SQL Injection Prevention**: EF Core parameterized queries

### Rate Limiting Details

**Per-IP Partitioning**: Each IP address gets independent rate limit counters

```csharp
// Authentication endpoints (Sliding Window)
options.AddPolicy("auth", context =>
{
    var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return RateLimitPartition.GetSlidingWindowLimiter(
        partitionKey: ipAddress,
        factory: partition => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6
        });
});
```

**Benefits**:
- Prevents brute force attacks on login/register
- Stops DDoS attempts
- Ensures fair resource allocation per user
- Only blocks abusive IPs, not all users

### Production Security Checklist

- [ ] Move all secrets to Azure Key Vault or environment variables
- [ ] Enable HTTPS redirection (`app.UseHttpsRedirection()`)
- [ ] Configure proper CORS origins (replace `"http://localhost:3000"`)
- [ ] Set up centralized logging (Azure Application Insights, Serilog)
- [ ] Implement distributed rate limiting with Redis
- [ ] Add health check endpoints
- [ ] Enable API versioning
- [ ] Add request/response logging middleware
- [ ] Implement global exception handling
- [ ] Set up SQL Server backups
- [ ] Enable Azure Blob Storage access policies
- [ ] Configure firewall rules for SQL Server
- [ ] Use managed identities for Azure services
- [ ] Implement audit logging for sensitive operations
- [ ] Add monitoring and alerting

---

## ?? Development

### Running with Hot Reload

```bash
cd ECommerceBackend.API
dotnet watch run
```

### Creating and Applying Migrations

**Add a new migration:**
```bash
dotnet ef migrations add MigrationName --project ECommerceBackend.Infrastructure --startup-project ECommerceBackend.API
```

**Update database:**
```bash
dotnet ef database update --project ECommerceBackend.Infrastructure --startup-project ECommerceBackend.API
```

**Remove last migration (if not applied):**
```bash
dotnet ef migrations remove --project ECommerceBackend.Infrastructure --startup-project ECommerceBackend.API
```

### Testing with Bruno/Postman

**1. Register a new user:**
```
POST https://localhost:7244/api/Auth/register
Content-Type: application/json

{
  "Name": "Test User",
  "Email": "test@example.com",
  "Password": "Test@123"
}
```

**2. Copy the `AccessToken` from response**

**3. Make authenticated requests:**
```
GET https://localhost:7244/api/Cart/getItems
Authorization: Bearer {AccessToken}
```

**4. Refresh token (automatic via cookie):**
```
POST https://localhost:7244/api/Auth/refresh-token
```

### Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| **405 Method Not Allowed** | Check URL case sensitivity. Use `/api/Auth/login` (capital `A`) |
| **401 Unauthorized** | Access token expired (15-min lifetime). Call `/api/Auth/refresh-token` |
| **429 Too Many Requests** | Rate limit exceeded. Wait 1 minute for counter reset |
| **CORS Error** | Update `WithOrigins()` in `Program.cs` to match your frontend URL |
| **Database Connection Failed** | Check SQL Server is running and connection string is correct |
| **Email Send Failed** | Verify Gmail App Password and 2-Step Verification is enabled |
| **Azure Blob Error** | Verify Azure Storage connection string and container exists |

### Debugging Tips

**Enable detailed errors in development:**
```csharp
// In Program.cs
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
```

**Check JWT token claims:**
```csharp
// In any controller action
var claims = User.Claims.Select(c => new { c.Type, c.Value });
return Ok(claims);
```

**View rate limit headers:**
```
RateLimit-Limit: 5
RateLimit-Remaining: 3
RateLimit-Reset: 45
Retry-After: 45
```

---

## ?? Dependencies

### Key NuGet Packages

```xml
<!-- API Layer -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.RateLimiting" Version="8.0.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />

<!-- Application Layer -->
<PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.0.1" />
<PackageReference Include="QuestPDF" Version="2024.10.0" />
<PackageReference Include="Azure.Storage.Blobs" Version="12.19.1" />

<!-- Infrastructure Layer -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.0" />

<!-- Domain Layer -->
<!-- No external dependencies - pure domain models -->
```

---

## ?? Deployment

### Deploy to Azure App Service

**1. Publish the project:**
```bash
dotnet publish -c Release -o ./publish
```

**2. Create Azure Resources:**
- Azure App Service (Windows or Linux)
- Azure SQL Database
- Azure Blob Storage Account

**3. Configure App Service:**

Add these Application Settings (replaces `appsettings.json`):

```
ConnectionStrings__ECommerceBackendDBConnection=Server=tcp:your-server.database.windows.net,1433;...
Jwt__Issuer=https://api.yourdomain.com
Jwt__Audience=https://yourdomain.com
Jwt__Secret=<your-production-secret>
EmailSettings__SenderEmail=your-email@gmail.com
EmailSettings__AppPassword=<app-password>
AzureBlobStorage__ConnectionString=<connection-string>
```

**4. Deploy using Azure CLI:**
```bash
az webapp up --name your-app-name --resource-group your-rg --runtime "DOTNETCORE|8.0"
```

**5. Run migrations on Azure:**
```bash
dotnet ef database update --project ECommerceBackend.Infrastructure --startup-project ECommerceBackend.API --connection "your-azure-sql-connection-string"
```

### Environment-Specific Configuration

**Create `appsettings.Production.json`:**
```json
{
  "Jwt": {
    "Issuer": "https://api.yourdomain.com",
    "Audience": "https://yourdomain.com"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## ?? Contributing

Contributions are welcome! Please follow these steps:

1. **Fork the repository**
2. **Create a feature branch**
   ```bash
   git checkout -b feature/AmazingFeature
   ```
3. **Commit your changes**
   ```bash
   git commit -m 'Add some AmazingFeature'
   ```
4. **Push to the branch**
   ```bash
   git push origin feature/AmazingFeature
   ```
5. **Open a Pull Request**

### Coding Standards

- Follow [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Write unit tests for new features
- Ensure all tests pass before submitting PR

---

## ?? License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

```
MIT License

Copyright (c) 2025 Animesh Ganai

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## ?? Author

**Animesh Ganai**

- GitHub: [@animeshganai1999](https://github.com/animeshganai1999)
- Email: animesh1234ganai@gmail.com
- LinkedIn: [Animesh Ganai](https://www.linkedin.com/in/animesh-ganai)

---

## ?? Acknowledgments

- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core)
- [QuestPDF Documentation](https://www.questpdf.com/)
- [JWT.io](https://jwt.io/) - JWT Debugger
- [AutoMapper](https://automapper.org/)
- [Swagger/OpenAPI](https://swagger.io/)

---

## ?? Changelog

### Version 1.0.0 (2025-12-21)

**Added:**
- ? JWT authentication with access and refresh tokens
- ? User registration and login endpoints
- ? Shopping cart management (add, update, delete items)
- ? PDF invoice generation using QuestPDF
- ? Email notifications via Gmail SMTP
- ? Invoice storage in Azure Blob Storage
- ? Rate limiting per IP address (sliding/fixed window)
- ? CORS configuration for frontend integration
- ? Swagger/OpenAPI documentation
- ? Entity Framework Core with SQL Server
- ? Repository pattern for data access
- ? AutoMapper for DTO mappings
- ? User ID extraction from JWT claims for security

**Security:**
- ? Password hashing with ASP.NET Core Identity
- ? HTTP-only cookies for refresh tokens
- ? Issuer and Audience validation in JWT
- ? Per-IP rate limiting to prevent abuse
- ? HTTPS enforcement

---

## ?? Project Statistics

- **Total Projects**: 4 (API, Application, Domain, Infrastructure)
- **Architecture**: Clean Architecture / Onion Architecture
- **Lines of Code**: ~3,000+
- **Controllers**: 5
- **Services**: 5
- **Repositories**: 5
- **Entities**: 4
- **DTOs**: 6
- **API Endpoints**: 8

---

## ?? Future Enhancements

- [ ] Add unit and integration tests
- [ ] Implement forgot password functionality
- [ ] Add email verification for new users
- [ ] Implement two-factor authentication (2FA)
- [ ] Add product catalog management
- [ ] Implement payment gateway integration (Stripe/PayPal)
- [ ] Add order tracking and status updates
- [ ] Implement admin dashboard endpoints
- [ ] Add GraphQL support
- [ ] Implement WebSockets for real-time notifications
- [ ] Add caching with Redis
- [ ] Implement search functionality with Elasticsearch
- [ ] Add API versioning
- [ ] Implement soft deletes for entities
- [ ] Add comprehensive logging with Serilog
- [ ] Implement health checks
- [ ] Add performance monitoring
- [ ] Create Docker containerization
- [ ] Add Kubernetes deployment manifests
- [ ] Implement CI/CD pipeline

---

## ?? Support

If you encounter any issues or have questions:

1. **Check the documentation** - Most common issues are covered above
2. **Search existing issues** - Someone might have faced the same problem
3. **Open a new issue** - Provide detailed information about the problem
4. **Contact the maintainer** - Email: animesh1234ganai@gmail.com

---

## ? Star History

If you find this project helpful, please consider giving it a star on GitHub! It helps others discover the project.

---

**Built with ?? using .NET 8 by Animesh Ganai**
