# Migration Plan: Local Configuration Secrets to Azure Key Vault

## Executive Summary

**Migration Type**: Sensitive Configuration Management  
**Source Technology**: Local Configuration (appsettings.json with hardcoded secrets)  
**Target Technology**: Azure Key Vault Secrets  
**Framework**: .NET 8.0 (SDK-style project)  
**Scope**: Migrate JWT signing secret and other sensitive configuration from appsettings.json to Azure Key Vault

## Current State Analysis

### Identified Secrets in Configuration

Based on codebase analysis, the following sensitive values are currently stored in `appsettings.json`:

1. **JWT Signing Secret** (Primary Migration Target)
   - Location: `appsettings.json` ? `Jwt:Secret`
   - Current Value: `"ECommerce_business_animesh_ganai_secret_key@2025!!"`
   - Usage: JWT token signing in `AuthService.cs`
   - Security Risk: **HIGH** - Critical secret for authentication

2. **Email Settings** (Secondary Migration Target)
   - `EmailSettings:AppPassword` - Gmail app password
   - Security Risk: **MEDIUM** - Access to email sending

3. **Azure Blob Storage Connection String** (Already should be in Key Vault)
   - `AzureBlobStorage:ConnectionString`
   - Security Risk: **MEDIUM** - Storage access

### Current JWT Implementation

**File**: `ECommerceBackend.Application/Services/AuthService.cs`
```csharp
public AuthService(IUserRepository userRepository, ITokenRepository tokenRepository, IConfiguration configuration)
{
    _userRepository = userRepository;
    _tokenRepository = tokenRepository;
    _issuer = configuration["Jwt:Issuer"]!;
    _audience = configuration["Jwt:Audience"]!;
    _secret = configuration["Jwt:Secret"]!;  // ? Currently from appsettings.json
    _expiryMinutes = int.Parse(configuration["Jwt:AccessTokenExpiryMinutes"] ?? "15");
}

private string GenerateAccessToken(User user)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));  // ? Uses the secret
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    // ... token generation
}
```

**File**: `ECommerceBackend.API/Program.cs`
```csharp
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secret = jwtSettings["Secret"];  // ? Currently from appsettings.json
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),  // ? Uses the secret
            // ... other validation parameters
        };
    });
```

### Existing Azure Key Vault Integration

The project **already has Azure Key Vault configured** in `Program.cs`:

```csharp
var keyVaultName = builder.Configuration["KeyVaultName"];
if (!string.IsNullOrEmpty(keyVaultName))
{
    var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential(),
        new AzureKeyVaultConfigurationOptions
        {
            // Optional: Configure reload interval if secrets need to be refreshed
            // ReloadInterval = TimeSpan.FromMinutes(5)
        });
}
```

**This means the infrastructure is already in place!** We just need to:
1. Move secrets from `appsettings.json` to Azure Key Vault
2. Add the `KeyVaultName` configuration
3. Ensure proper Azure RBAC permissions

## Migration Strategy

### Option 1: Use Existing Key Vault Configuration Integration (RECOMMENDED)

**Pros**:
- ? Already implemented in the codebase
- ? Seamless integration with .NET configuration system
- ? No code changes required in services
- ? Automatic secret refresh support
- ? Works with `DefaultAzureCredential`

**Implementation**:
1. Store secrets in Azure Key Vault with proper naming convention
2. Configure `KeyVaultName` in appsettings.json
3. Secrets automatically loaded into `IConfiguration`

### Option 2: Direct Azure Key Vault SDK Access

**Pros**:
- More control over secret retrieval
- Explicit secret management

**Cons**:
- Requires code changes in multiple services
- More complex error handling
- Need to manage `SecretClient` lifetime

**Decision**: We will use **Option 1** since the infrastructure is already in place.

## Required NuGet Packages

### Current Packages (Already Installed ?)

```xml
<PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.3.2" />
<PackageReference Include="Azure.Identity" Version="1.14.0" />
<PackageReference Include="Azure.Security.KeyVault.Secrets" Version="4.8.0" />
```

**Status**: All required packages are already installed. No package installation needed.

## Azure Key Vault Secret Naming Convention

Azure Key Vault secret names have restrictions:
- Only alphanumeric characters and hyphens allowed
- No colons or special characters

### Mapping Strategy

| Configuration Key | Key Vault Secret Name | Value Source |
|-------------------|----------------------|--------------|
| `Jwt:Secret` | `Jwt--Secret` | Current appsettings.json |
| `Jwt:Issuer` | `Jwt--Issuer` | Current appsettings.json |
| `Jwt:Audience` | `Jwt--Audience` | Current appsettings.json |
| `EmailSettings:AppPassword` | `EmailSettings--AppPassword` | Current appsettings.json |
| `ConnectionStrings:ECommerceBackendDBConnection` | `ConnectionStrings--ECommerceBackendDBConnection` | Current appsettings.json |
| `ConnectionStrings:Redis` | `ConnectionStrings--Redis` | Current appsettings.json |
| `AzureBlobStorage:ConnectionString` | `AzureBlobStorage--ConnectionString` | Current appsettings.json |

**Note**: The `AzureKeyVaultConfigurationOptions` automatically maps `--` to `:` when loading into `IConfiguration`.

## Migration Tasks

### Phase 1: Preparation and Setup

1. **Create Key Vault Secret Storage Script**
   - Create PowerShell script to upload secrets to Azure Key Vault
   - Use Azure CLI to authenticate and store secrets

2. **Update Configuration Files**
   - Add `KeyVaultName` to appsettings.json
   - Remove sensitive values from appsettings.json (or comment them)
   - Add developer guidance comments

3. **Verify Existing Key Vault Integration**
   - Ensure the Key Vault configuration code in Program.cs is working
   - No code changes needed (already implemented)

### Phase 2: Secret Migration

4. **Create Azure Key Vault Secrets**
   - Upload JWT secret to Key Vault
   - Upload email password to Key Vault
   - Upload connection strings to Key Vault

5. **Configure Azure RBAC Permissions**
   - Ensure application Managed Identity has "Key Vault Secrets User" role
   - For local development, ensure developer accounts have access

### Phase 3: Testing and Validation

6. **Update appsettings.json**
   - Remove hardcoded secrets
   - Add KeyVaultName configuration
   - Keep non-sensitive settings

7. **Test Local Development**
   - Verify DefaultAzureCredential works locally
   - Test JWT token generation and validation
   - Test email sending functionality

8. **Build Verification**
   - Ensure project compiles successfully
   - Verify no hardcoded secrets remain

### Phase 4: Documentation

9. **Create Developer Setup Guide**
   - Document how to configure local Azure credentials
   - Document how to add secrets to Key Vault
   - Update README.md with Key Vault setup instructions

## Code Changes Required

### File 1: `ECommerceBackend.API/appsettings.json`

**Before**:
```json
{
  "Jwt": {
    "Issuer": "yourdomain.com",
    "Audience": "yourdomain.com",
    "Secret": "ECommerce_business_animesh_ganai_secret_key@2025!!",
    "AccessTokenExpiryMinutes": 15
  },
  "EmailSettings": {
    "SenderEmail": "aniecom.contact@gmail.com",
    "AppPassword": "kaga zzwr cucq atqw",
    "ReceiverEmail": "animesh1234ganai@gmail.com"
  },
  "AzureBlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;...",
    "ContainerName": "customerinvoice"
  },
  "ConnectionStrings": {
    "ECommerceBackendDBConnection": "Data Source=animesh\\sqlexpress01;...",
    "Redis": "localhost:6379"
  }
}
```

**After**:
```json
{
  "KeyVaultName": "",  // ? Add this for Azure Key Vault integration

  "Jwt": {
    "Issuer": "yourdomain.com",
    "Audience": "yourdomain.com",
    // Secret moved to Key Vault: Jwt--Secret
    "AccessTokenExpiryMinutes": 15
  },
  "EmailSettings": {
    "SenderEmail": "aniecom.contact@gmail.com",
    // AppPassword moved to Key Vault: EmailSettings--AppPassword
    "ReceiverEmail": "animesh1234ganai@gmail.com"
  },
  "AzureBlobStorage": {
    // ConnectionString moved to Key Vault: AzureBlobStorage--ConnectionString
    "ContainerName": "customerinvoice"
  },
  "ConnectionStrings": {
    // Moved to Key Vault: ConnectionStrings--ECommerceBackendDBConnection
    // Moved to Key Vault: ConnectionStrings--Redis
  }
}
```

### File 2: `ECommerceBackend.API/appsettings.Development.json`

**Add local development configuration**:
```json
{
  "KeyVaultName": "your-dev-keyvault-name",  // ? Add local Key Vault for development
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### File 3: Create `.appmod/scripts/upload-secrets-to-keyvault.ps1`

PowerShell script to upload secrets to Azure Key Vault.

### File 4: Update `README.md`

Add Azure Key Vault setup instructions.

## No Service Code Changes Required ?

**Important**: Because the project already uses the `AddAzureKeyVault` configuration integration, **no changes are needed** in:

- ? `AuthService.cs` - Will continue to use `configuration["Jwt:Secret"]`
- ? `Program.cs` (JWT configuration) - Will continue to use `jwtSettings["Secret"]`
- ? `EmailService.cs` - Will continue to use `configuration["EmailSettings:AppPassword"]`
- ? Any other services using `IConfiguration`

The Azure Key Vault integration transparently loads secrets into the `IConfiguration` system.

## Security Improvements

### Before Migration
- ? Secrets stored in plain text in appsettings.json
- ? Secrets committed to version control
- ? No secret rotation mechanism
- ? No audit trail for secret access

### After Migration
- ? Secrets stored encrypted in Azure Key Vault
- ? No secrets in source code or configuration files
- ? Automatic secret refresh support (configurable)
- ? Azure audit logs for secret access
- ? RBAC-based access control
- ? Support for Managed Identity in Azure
- ? Support for DefaultAzureCredential in local development

## Local Development Setup

### Prerequisites
1. Azure CLI installed and authenticated (`az login`)
2. Azure account with access to Key Vault
3. Key Vault created with appropriate secrets

### Developer Workflow

**Option 1: Use Azure Key Vault (Recommended for team consistency)**
```bash
# Authenticate with Azure
az login

# Set Key Vault name in appsettings.Development.json
# Secrets will be loaded automatically from Key Vault
dotnet run
```

**Option 2: Use User Secrets for Local Development**
```bash
# Initialize user secrets
dotnet user-secrets init --project ECommerceBackend.API

# Set local secrets (overrides Key Vault for local dev)
dotnet user-secrets set "Jwt:Secret" "your-local-secret" --project ECommerceBackend.API
dotnet user-secrets set "EmailSettings:AppPassword" "your-local-password" --project ECommerceBackend.API
```

## Azure Deployment Configuration

### Required Azure Resources
1. **Azure Key Vault** - To store secrets
2. **Managed Identity** - For the Azure App Service or Container App
3. **RBAC Role Assignment** - Grant "Key Vault Secrets User" to Managed Identity

### App Service Configuration

**Environment Variables**:
```
KeyVaultName=your-production-keyvault
AZURE_CLIENT_ID=<managed-identity-client-id>  // Optional for user-assigned MI
```

**System-Assigned Managed Identity**:
```bash
# Enable system-assigned managed identity
az webapp identity assign --name <app-name> --resource-group <resource-group>

# Grant access to Key Vault
az keyvault set-policy --name <keyvault-name> \
  --object-id <managed-identity-principal-id> \
  --secret-permissions get list
```

## Risk Assessment

### Migration Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Key Vault unavailable during startup | HIGH | Add fallback to user secrets in development |
| Incorrect RBAC permissions | MEDIUM | Test with multiple environments before production |
| Secret naming mismatch | LOW | Use automated scripts to upload with correct naming |
| Local development complexity | LOW | Provide clear documentation and setup scripts |

### Rollback Plan

If migration fails:
1. Set `KeyVaultName` to empty string in appsettings.json
2. Uncomment original secret values in appsettings.json
3. Application will work as before

## Success Criteria

- ? All secrets removed from appsettings.json
- ? JWT authentication works correctly using Key Vault secrets
- ? Email functionality works correctly using Key Vault secrets
- ? Database connections work correctly using Key Vault secrets
- ? Application compiles and builds successfully
- ? Local development experience documented
- ? No hardcoded secrets in codebase
- ? README.md updated with Key Vault setup instructions

## Timeline Estimate

| Phase | Estimated Time |
|-------|---------------|
| Phase 1: Preparation | 15 minutes |
| Phase 2: Secret Migration | 20 minutes |
| Phase 3: Testing | 15 minutes |
| Phase 4: Documentation | 15 minutes |
| **Total** | **~65 minutes** |

## Dependencies and Prerequisites

### Azure Requirements
- Azure subscription
- Azure Key Vault created
- Azure CLI installed (for local development)
- Visual Studio or VS Code with Azure account extension

### Development Requirements
- .NET 8 SDK
- Azure CLI authenticated (`az login`)
- Access to Key Vault (RBAC permissions)

## Post-Migration Checklist

- [ ] All secrets uploaded to Azure Key Vault
- [ ] appsettings.json cleaned of sensitive values
- [ ] KeyVaultName configured in appsettings.json
- [ ] Local development tested successfully
- [ ] JWT token generation and validation tested
- [ ] Email functionality tested
- [ ] Database connectivity tested
- [ ] Build verification passed
- [ ] README.md updated
- [ ] Developer setup guide created
- [ ] Team notified of changes

---

**Migration Prepared By**: GitHub Copilot  
**Date**: 2025-01-21  
**Target Framework**: .NET 8.0  
**Estimated Completion**: 65 minutes
