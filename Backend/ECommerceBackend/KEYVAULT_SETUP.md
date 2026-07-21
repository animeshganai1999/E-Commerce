# Azure Key Vault Configuration Guide

## Overview

This application has been migrated to use Azure Key Vault for secure secret management. All sensitive credentials and connection strings that were previously stored in `appsettings.json` must now be stored in Azure Key Vault.

## Prerequisites

1. **Azure Key Vault**: Create an Azure Key Vault resource in your Azure subscription
2. **Authentication**: 
   - For local development: Use `az login` with Azure CLI
   - For Azure-hosted applications: Use Managed Identity (recommended)

## Configuration Steps

### 1. Create Azure Key Vault

```bash
# Login to Azure
az login

# Create a resource group (if needed)
az group create --name YourResourceGroup --location eastus

# Create Key Vault
az keyvault create --name YourKeyVaultName --resource-group YourResourceGroup --location eastus
```

### 2. Grant Access Permissions

#### For Local Development
```bash
# Grant yourself access to manage secrets
az keyvault set-policy --name YourKeyVaultName --upn your-email@domain.com --secret-permissions get list set delete
```

#### For Azure App Service (Managed Identity)
```bash
# Enable managed identity on your App Service
az webapp identity assign --name YourAppServiceName --resource-group YourResourceGroup

# Get the principal ID (it will be displayed in the output)

# Grant the App Service access to Key Vault
az keyvault set-policy --name YourKeyVaultName --object-id <principal-id> --secret-permissions get list
```

### 3. Add Secrets to Key Vault

Use the Azure CLI to add each secret. Note that Azure Key Vault uses `--` (double dash) as the section separator instead of `:` used in appsettings.json.

```bash
# Email Settings
az keyvault secret set --vault-name YourKeyVaultName --name "EmailSettings--SenderEmail" --value "your-sender-email@gmail.com"
az keyvault secret set --vault-name YourKeyVaultName --name "EmailSettings--AppPassword" --value "your-gmail-app-password"
az keyvault secret set --vault-name YourKeyVaultName --name "EmailSettings--ReceiverEmail" --value "your-receiver-email@gmail.com"

# Azure Blob Storage
az keyvault secret set --vault-name YourKeyVaultName --name "AzureBlobStorage--ConnectionString" --value "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"

# JWT Secret (CRITICAL - must match the secret used to sign existing tokens)
az keyvault secret set --vault-name YourKeyVaultName --name "Jwt--Secret" --value "your-jwt-signing-secret-key"

# SQL Server Connection String
az keyvault secret set --vault-name YourKeyVaultName --name "ConnectionStrings--ECommerceBackendDBConnection" --value "Data Source=...;Initial Catalog=...;Integrated Security=True;Trust Server Certificate=True"

# Redis Connection (if different from localhost:6379)
az keyvault secret set --vault-name YourKeyVaultName --name "ConnectionStrings--Redis" --value "your-redis-connection-string"
```

### 4. Update appsettings.json

Update the `KeyVaultName` value in `appsettings.json`:

```json
{
  "KeyVaultName": "YourKeyVaultName",
  ...
}
```

## Secret Naming Convention

| Configuration Key (appsettings.json) | Key Vault Secret Name |
|--------------------------------------|----------------------|
| `EmailSettings:SenderEmail` | `EmailSettings--SenderEmail` |
| `EmailSettings:AppPassword` | `EmailSettings--AppPassword` |
| `EmailSettings:ReceiverEmail` | `EmailSettings--ReceiverEmail` |
| `AzureBlobStorage:ConnectionString` | `AzureBlobStorage--ConnectionString` |
| `Jwt:Secret` | `Jwt--Secret` |
| `ConnectionStrings:ECommerceBackendDBConnection` | `ConnectionStrings--ECommerceBackendDBConnection` |
| `ConnectionStrings:Redis` | `ConnectionStrings--Redis` |

**Note**: Azure Key Vault Configuration Provider automatically converts `--` in secret names to `:` when loading into the configuration system.

## Required Secrets

### Critical Secrets (Must be configured)

1. **Jwt--Secret**: JWT signing key for authentication. If this doesn't match the key used to sign existing tokens, all authentication will fail.
2. **ConnectionStrings--ECommerceBackendDBConnection**: Database connection string. Application cannot start without this.

### Important Secrets (Application features will fail without these)

3. **EmailSettings--SenderEmail**: Gmail sender email address
4. **EmailSettings--AppPassword**: Gmail app password
5. **EmailSettings--ReceiverEmail**: Default receiver email for contact form
6. **AzureBlobStorage--ConnectionString**: Azure Storage account connection string for invoice storage
7. **ConnectionStrings--Redis**: Redis connection string (default: localhost:6379)

## Non-Sensitive Configuration

The following values remain in `appsettings.json` as they are not sensitive:

- `Jwt:Issuer`: JWT token issuer
- `Jwt:Audience`: JWT token audience
- `Jwt:AccessTokenExpiryMinutes`: Token expiration time
- `AzureBlobStorage:ContainerName`: Blob container name
- `Cors:AllowedOrigins`: CORS allowed origins
- `Logging`: Logging configuration
- `AllowedHosts`: Allowed hosts

## Verification

After configuring Key Vault, verify the application can start:

1. Ensure you're authenticated (run `az login` if needed)
2. Start the application
3. Check that the application can access secrets by verifying:
   - Database connectivity (health check endpoint: `/health`)
   - Authentication works (try to login)
   - Email service works (try contact form)

## Troubleshooting

### "KeyVaultName configuration is missing or empty"
- Ensure `KeyVaultName` is set in `appsettings.json`

### "Access denied" errors
- Verify you have granted proper permissions to your identity (user or managed identity)
- Check that the permission includes `get` and `list` for secrets

### "Secret 'name' not found"
- Verify the secret exists in Key Vault using: `az keyvault secret list --vault-name YourKeyVaultName`
- Check that the secret name uses `--` instead of `:`

### Authentication fails after migration
- Verify that `Jwt--Secret` in Key Vault matches the original secret
- Existing JWT tokens were signed with the old secret and won't work if the secret changes

## Security Best Practices

1. **Never commit secrets** to source control
2. **Rotate secrets regularly** using Key Vault's versioning feature
3. **Use Managed Identity** in production instead of service principals
4. **Limit access** - grant only necessary permissions (principle of least privilege)
5. **Enable audit logging** in Key Vault to track secret access
6. **Use separate Key Vaults** for different environments (dev, staging, production)

## Additional Resources

- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Azure Key Vault Configuration Provider](https://docs.microsoft.com/en-us/aspnet/core/security/key-vault-configuration)
- [Managed Identity Documentation](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/)
