# Azure Key Vault Integration Guide

This document provides comprehensive instructions for configuring and using Azure Key Vault to securely manage secrets in the E-Commerce Backend API.

## ?? Table of Contents

- [Overview](#overview)
- [Why Azure Key Vault?](#why-azure-key-vault)
- [Prerequisites](#prerequisites)
- [Setup Instructions](#setup-instructions)
- [Local Development](#local-development)
- [Azure Deployment](#azure-deployment)
- [Secret Management](#secret-management)
- [Troubleshooting](#troubleshooting)
- [Migration from appsettings.json](#migration-from-appsettingsjson)

---

## Overview

The E-Commerce Backend API has been migrated from storing secrets in `appsettings.json` to using **Azure Key Vault** for secure secret management. This provides:

? **Enhanced Security**: Secrets encrypted at rest and in transit  
? **Centralized Management**: Single source of truth for all environments  
? **Access Control**: Fine-grained RBAC permissions  
? **Audit Logging**: Track secret access with Azure Monitor  
? **Secret Rotation**: Update secrets without code deployment  
? **No Secrets in Source Control**: Never commit sensitive values

---

## Why Azure Key Vault?

### Before Migration (appsettings.json)

```json
{
  "Jwt": {
    "Secret": "YOUR_SECRET_KEY_HERE" // ? Visible in plain text
  },
  "ConnectionStrings": {
    "Database": "Server=...;Password=secret123;" // ? Committed to Git
  }
}
```

**Problems**:
- ? Secrets stored in plain text
- ? Accidentally committed to version control
- ? No audit trail
- ? Difficult to rotate secrets
- ? Same secrets across environments

### After Migration (Azure Key Vault)

```json
{
  "KeyVaultName": "my-ecommerce-keyvault", // ? Only Key Vault reference
  "Jwt": {
    "Issuer": "yourdomain.com", // Non-sensitive values remain
    "Audience": "yourdomain.com"
    // Secret automatically loaded from Key Vault
  }
}
```

**Benefits**:
- ? Secrets stored encrypted in Azure Key Vault
- ? No sensitive values in source code
- ? Full audit trail in Azure Monitor
- ? Easy secret rotation
- ? Different secrets per environment

---

## Prerequisites

### Required Tools

1. **Azure CLI** (for local development and deployment)
   ```bash
   # Install Azure CLI
   # Windows: https://aka.ms/installazurecli
   # macOS: brew install azure-cli
   # Linux: curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

   # Verify installation
   az --version
   ```

2. **Azure Subscription**
   - Active Azure subscription
   - Contributor or Owner role (to create resources)

3. **.NET 8 SDK** (already required for the project)

### Azure Resources Needed

1. **Azure Key Vault** - To store secrets
2. **Azure Managed Identity** (for Azure deployment)
3. **RBAC Permissions** - "Key Vault Secrets User" role

---

## Setup Instructions

### Step 1: Create Azure Key Vault

```bash
# Login to Azure
az login

# Set your subscription (if you have multiple)
az account set --subscription "Your Subscription Name"

# Create a resource group (if you don't have one)
az group create \
  --name ecommerce-rg \
  --location eastus

# Create a Key Vault
az keyvault create \
  --name my-ecommerce-keyvault \
  --resource-group ecommerce-rg \
  --location eastus \
  --enable-rbac-authorization true

# Note: Key Vault names must be globally unique
# If the name is taken, try: ecommerce-kv-<yourname>-<random>
```

**Important**: Save your Key Vault name for later use.

### Step 2: Grant Yourself Access

```bash
# Get your user object ID
USER_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

# Get the Key Vault resource ID
KV_ID=$(az keyvault show --name my-ecommerce-keyvault --query id -o tsv)

# Grant yourself "Key Vault Secrets User" role
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $USER_OBJECT_ID \
  --scope $KV_ID

# Verify access
az keyvault secret list --vault-name my-ecommerce-keyvault
```

### Step 3: Upload Secrets to Key Vault

#### Option A: Use the Provided PowerShell Script (Recommended)

```powershell
# Navigate to the scripts directory
cd .appmod/scripts

# Edit the script to replace placeholder values with actual secrets
# (See the script file for detailed instructions)

# Run the script
.\upload-secrets-to-keyvault.ps1 -KeyVaultName "my-ecommerce-keyvault"

# Dry run mode (to test without uploading)
.\upload-secrets-to-keyvault.ps1 -KeyVaultName "my-ecommerce-keyvault" -DryRun
```

#### Option B: Upload Secrets Manually via Azure CLI

```bash
# JWT Secret
az keyvault secret set \
  --vault-name my-ecommerce-keyvault \
  --name "Jwt--Secret" \
  --value "YOUR_SECRET_KEY_AT_LEAST_32_CHARACTERS"

# JWT Issuer
az keyvault secret set \
  --vault-name my-ecommerce-keyvault \
  --name "Jwt--Issuer" \
  --value "yourdomain.com"

# JWT Audience
az keyvault secret set \
  --vault-name my-ecommerce-keyvault \
  --name "Jwt--Audience" \
  --value "yourdomain.com"

# Email App Password
az keyvault secret set \
  --vault-name my-ecommerce-keyvault \
  --name "EmailSettings--AppPassword" \
  --value "your-gmail-app-password"

# SQL Server Connection String
az keyvault secret set \
  --vault-name my-ecommerce-keyvault \
  --name "ConnectionStrings--ECommerceBackendDBConnection" \
  --value "Server=yourserver.database.windows.net;Database=ECommerceDb;Authentication=Active Directory Default;"

# Redis Connection String
az keyvault secret set \
  --vault-name my-ecommerce-keyvault \
  --name "ConnectionStrings--Redis" \
  --value "yourredis.redis.cache.windows.net:6380,password=xxx,ssl=True"

# Azure Blob Storage Connection String
az keyvault secret set \
  --vault-name my-ecommerce-keyvault \
  --name "AzureBlobStorage--ConnectionString" \
  --value "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=..."
```

**Note**: Secret names use `--` (double dash) instead of `:` (colon) because Azure Key Vault only allows alphanumeric characters and hyphens. The configuration provider automatically maps `--` to `:`.

### Step 4: Configure Application

Update `appsettings.Development.json` (for local development):

```json
{
  "KeyVaultName": "my-ecommerce-keyvault"
}
```

For production, set this via environment variable:
```bash
KeyVaultName=my-ecommerce-keyvault
```

---

## Local Development

### Authentication Options

The application uses `DefaultAzureCredential`, which tries multiple authentication methods in order:

1. **Environment Variables** (AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_CLIENT_SECRET)
2. **Managed Identity** (not applicable locally)
3. **Azure CLI** (recommended for local development)
4. **Visual Studio** (if signed into Azure)
5. **VS Code Azure Account** (if signed into Azure)

### Recommended: Azure CLI Authentication

```bash
# Login to Azure
az login

# Verify your account
az account show

# Run your application
cd ECommerceBackend.API
dotnet run
```

The application will automatically authenticate using your Azure CLI credentials.

### Alternative: User Secrets (Local Override)

If you prefer not to use Key Vault locally, you can use .NET User Secrets:

```bash
# Initialize user secrets
dotnet user-secrets init --project ECommerceBackend.API

# Set local secrets (these override Key Vault)
dotnet user-secrets set "Jwt:Secret" "your-local-dev-secret" --project ECommerceBackend.API
dotnet user-secrets set "ConnectionStrings:ECommerceBackendDBConnection" "your-local-connection" --project ECommerceBackend.API
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project ECommerceBackend.API
```

**Configuration Precedence** (highest to lowest):
1. User Secrets
2. Environment Variables
3. Azure Key Vault
4. appsettings.{Environment}.json
5. appsettings.json

### Verify Configuration Loading

Add this temporary code to `Program.cs` after `var app = builder.Build();`:

```csharp
// Temporary: Verify secrets are loaded
Console.WriteLine($"JWT Secret loaded: {!string.IsNullOrEmpty(builder.Configuration["Jwt:Secret"])}");
Console.WriteLine($"DB Connection loaded: {!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("ECommerceBackendDBConnection"))}");
```

---

## Azure Deployment

### Option 1: Azure App Service

#### Step 1: Enable System-Assigned Managed Identity

```bash
# Enable Managed Identity for App Service
az webapp identity assign \
  --name your-app-name \
  --resource-group ecommerce-rg

# Get the Managed Identity Principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name your-app-name \
  --resource-group ecommerce-rg \
  --query principalId -o tsv)

echo "Managed Identity Principal ID: $PRINCIPAL_ID"
```

#### Step 2: Grant Key Vault Access

```bash
# Get Key Vault resource ID
KV_ID=$(az keyvault show --name my-ecommerce-keyvault --query id -o tsv)

# Grant "Key Vault Secrets User" role to Managed Identity
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $PRINCIPAL_ID \
  --scope $KV_ID
```

#### Step 3: Configure Application Settings

```bash
# Set Key Vault name in App Service configuration
az webapp config appsettings set \
  --name your-app-name \
  --resource-group ecommerce-rg \
  --settings KeyVaultName=my-ecommerce-keyvault

# Restart the app to apply changes
az webapp restart \
  --name your-app-name \
  --resource-group ecommerce-rg
```

### Option 2: Azure Container Apps

```bash
# Enable System-Assigned Managed Identity
az containerapp identity assign \
  --name your-container-app \
  --resource-group ecommerce-rg

# Get Principal ID
PRINCIPAL_ID=$(az containerapp identity show \
  --name your-container-app \
  --resource-group ecommerce-rg \
  --query principalId -o tsv)

# Grant Key Vault access
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $PRINCIPAL_ID \
  --scope $(az keyvault show --name my-ecommerce-keyvault --query id -o tsv)

# Set environment variable
az containerapp update \
  --name your-container-app \
  --resource-group ecommerce-rg \
  --set-env-vars KeyVaultName=my-ecommerce-keyvault
```

### Option 3: Azure Kubernetes Service (AKS)

```bash
# Enable workload identity
az aks update \
  --name your-aks-cluster \
  --resource-group ecommerce-rg \
  --enable-oidc-issuer \
  --enable-workload-identity

# Create managed identity
az identity create \
  --name ecommerce-identity \
  --resource-group ecommerce-rg

# Grant Key Vault access
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $(az identity show --name ecommerce-identity --resource-group ecommerce-rg --query principalId -o tsv) \
  --scope $(az keyvault show --name my-ecommerce-keyvault --query id -o tsv)

# Configure workload identity federation
# (See AKS workload identity documentation for full setup)
```

---

## Secret Management

### Secret Name Mapping

Azure Key Vault secret names use `--` (double dash) instead of `:` (colon):

| Configuration Key | Key Vault Secret Name |
|-------------------|----------------------|
| `Jwt:Secret` | `Jwt--Secret` |
| `Jwt:Issuer` | `Jwt--Issuer` |
| `Jwt:Audience` | `Jwt--Audience` |
| `EmailSettings:AppPassword` | `EmailSettings--AppPassword` |
| `ConnectionStrings:ECommerceBackendDBConnection` | `ConnectionStrings--ECommerceBackendDBConnection` |
| `ConnectionStrings:Redis` | `ConnectionStrings--Redis` |
| `AzureBlobStorage:ConnectionString` | `AzureBlobStorage--ConnectionString` |

### Viewing Secrets

```bash
# List all secrets
az keyvault secret list --vault-name my-ecommerce-keyvault --query "[].name" -o table

# Show a specific secret
az keyvault secret show --vault-name my-ecommerce-keyvault --name "Jwt--Secret"

# Get secret value
az keyvault secret show --vault-name my-ecommerce-keyvault --name "Jwt--Secret" --query value -o tsv
```

### Updating Secrets

```bash
# Update a secret
az keyvault secret set \
  --vault-name my-ecommerce-keyvault \
  --name "Jwt--Secret" \
  --value "NEW_SECRET_VALUE"

# The application will automatically use the new value on next restart
# For immediate reload, configure ReloadInterval in Program.cs:
# ReloadInterval = TimeSpan.FromMinutes(5)
```

### Deleting Secrets

```bash
# Delete a secret (soft delete - can be recovered)
az keyvault secret delete --vault-name my-ecommerce-keyvault --name "Jwt--Secret"

# Permanently delete (purge)
az keyvault secret purge --vault-name my-ecommerce-keyvault --name "Jwt--Secret"
```

### Secret Versioning

Azure Key Vault maintains version history:

```bash
# List all versions of a secret
az keyvault secret list-versions --vault-name my-ecommerce-keyvault --name "Jwt--Secret"

# Get a specific version
az keyvault secret show \
  --vault-name my-ecommerce-keyvault \
  --name "Jwt--Secret" \
  --version "abc123..."
```

---

## Troubleshooting

### Issue: "Forbidden" or "Access Denied"

**Cause**: Missing RBAC permissions

**Solution**:
```bash
# Grant yourself access
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $(az ad signed-in-user show --query id -o tsv) \
  --scope $(az keyvault show --name my-ecommerce-keyvault --query id -o tsv)
```

### Issue: "Key Vault Not Found"

**Cause**: Incorrect Key Vault name or subscription

**Solution**:
```bash
# Verify Key Vault exists
az keyvault show --name my-ecommerce-keyvault

# List all Key Vaults in subscription
az keyvault list --query "[].name" -o table

# Check current subscription
az account show --query name
```

### Issue: Secrets Not Loading

**Cause**: DefaultAzureCredential can't authenticate

**Solution**:

1. **Check Azure CLI authentication**:
   ```bash
   az login
   az account show
   ```

2. **Enable detailed logging** in `appsettings.Development.json`:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Azure.Identity": "Information",
         "Azure.Security.KeyVault": "Information"
       }
     }
   }
   ```

3. **Check application logs** for authentication details:
   ```
   Azure.Identity: DefaultAzureCredential authentication attempt succeeded with...
   ```

### Issue: Application Slow to Start

**Cause**: Key Vault network latency or authentication delays

**Solution**:

1. **Use caching** (secrets are cached by default)
2. **Reduce reload interval** or disable it:
   ```csharp
   builder.Configuration.AddAzureKeyVault(
       keyVaultUri,
       new DefaultAzureCredential(),
       new AzureKeyVaultConfigurationOptions
       {
           ReloadInterval = null  // Disable auto-reload
       });
   ```

### Issue: "Secret not found" in Key Vault

**Cause**: Secret name mismatch (colon vs double-dash)

**Solution**: Verify secret name uses `--` instead of `:`:
```bash
# ? Wrong
az keyvault secret show --vault-name my-ecommerce-keyvault --name "Jwt:Secret"

# ? Correct
az keyvault secret show --vault-name my-ecommerce-keyvault --name "Jwt--Secret"
```

---

## Migration from appsettings.json

### Migration Checklist

- [X] Azure Key Vault created
- [X] RBAC permissions configured
- [X] Secrets uploaded to Key Vault
- [ ] `KeyVaultName` configured in appsettings
- [ ] Sensitive values removed from appsettings.json
- [ ] Local development tested (Azure CLI authentication)
- [ ] Azure deployment tested (Managed Identity)
- [ ] Team members granted access to Key Vault
- [ ] Documentation updated

### What Was Migrated

The following secrets were moved from `appsettings.json` to Azure Key Vault:

1. **JWT Secret** (`Jwt:Secret`)
2. **JWT Issuer** (`Jwt:Issuer`) - Optional, but recommended
3. **JWT Audience** (`Jwt:Audience`) - Optional, but recommended
4. **Email App Password** (`EmailSettings:AppPassword`)
5. **SQL Connection String** (`ConnectionStrings:ECommerceBackendDBConnection`)
6. **Redis Connection String** (`ConnectionStrings:Redis`)
7. **Azure Blob Storage Connection String** (`AzureBlobStorage:ConnectionString`)

### What Remains in appsettings.json

Non-sensitive configuration:
- `KeyVaultName` (Key Vault reference)
- `EmailSettings:SenderEmail` (not sensitive)
- `EmailSettings:ReceiverEmail` (not sensitive)
- `AzureBlobStorage:ContainerName` (not sensitive)
- `Logging` settings
- `AllowedHosts`
- `Cors:AllowedOrigins`
- `Jwt:AccessTokenExpiryMinutes`

### Rollback Procedure

If you need to revert to using `appsettings.json`:

1. Set `KeyVaultName` to empty string in appsettings.json:
   ```json
   {
     "KeyVaultName": ""
   }
   ```

2. Restore secrets from `appsettings.json.backup`:
   ```bash
   # The backup file was created during migration
   cp ECommerceBackend.API/appsettings.json.backup ECommerceBackend.API/appsettings.json
   ```

3. Restart the application

---

## Best Practices

### Security

? **DO**: Use Managed Identity in Azure environments  
? **DO**: Grant minimal required permissions (Key Vault Secrets User)  
? **DO**: Use separate Key Vaults for different environments (dev/staging/prod)  
? **DO**: Enable Key Vault audit logging  
? **DO**: Rotate secrets regularly  

? **DON'T**: Share Key Vault access keys  
? **DON'T**: Store Key Vault URLs in public repositories  
? **DON'T**: Use the same secrets across environments  
? **DON'T**: Grant "Key Vault Administrator" unless necessary  

### Development

? **DO**: Use Azure CLI authentication for local development  
? **DO**: Use User Secrets for local overrides  
? **DO**: Document required secrets in README  
? **DO**: Provide setup scripts for new team members  

? **DON'T**: Commit actual secret values  
? **DON'T**: Share secrets via email/chat  
? **DON'T**: Hardcode Key Vault names in code  

### Operations

? **DO**: Monitor Key Vault access logs  
? **DO**: Set up alerts for suspicious access  
? **DO**: Implement secret rotation procedures  
? **DO**: Document secret update procedures  
? **DO**: Test backup/restore procedures  

---

## Additional Resources

- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [DefaultAzureCredential Documentation](https://docs.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
- [Managed Identity Documentation](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/)
- [Azure RBAC Documentation](https://docs.microsoft.com/en-us/azure/role-based-access-control/)

---

## Support

If you encounter issues:

1. Check the [Troubleshooting](#troubleshooting) section
2. Review application logs (enable detailed logging for Azure.Identity)
3. Verify RBAC permissions in Azure Portal
4. Contact the development team

---

**Last Updated**: 2025-01-21  
**Migration Version**: 1.0.0
