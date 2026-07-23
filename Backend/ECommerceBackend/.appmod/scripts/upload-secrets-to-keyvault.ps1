# ============================================================================
# Azure Key Vault Secret Upload Script
# ============================================================================
# This script uploads sensitive configuration secrets from appsettings.json
# to Azure Key Vault for secure storage and management.
#
# PREREQUISITES:
# 1. Azure CLI installed (https://aka.ms/installazurecli)
# 2. Logged into Azure CLI: az login
# 3. Azure Key Vault created
# 4. Appropriate permissions on the Key Vault (Key Vault Administrator or Contributor)
#
# USAGE:
#   .\upload-secrets-to-keyvault.ps1 -KeyVaultName "your-keyvault-name"
#
# IMPORTANT NOTES:
# - Secret names in Key Vault use '--' instead of ':' (e.g., Jwt--Secret)
# - The Azure Key Vault configuration provider automatically maps '--' to ':'
# - Values shown here are examples - replace with your actual values
# ============================================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$KeyVaultName,

    [Parameter(Mandatory=$false)]
    [switch]$DryRun = $false
)

# Color output functions
function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }
function Write-Error { Write-Host $args -ForegroundColor Red }
function Write-Info { Write-Host $args -ForegroundColor Cyan }

Write-Info "============================================================================"
Write-Info "Azure Key Vault Secret Upload Script"
Write-Info "============================================================================"
Write-Info "Key Vault Name: $KeyVaultName"
Write-Info "Dry Run Mode: $DryRun"
Write-Info ""

# Check if Azure CLI is installed
Write-Info "Checking Azure CLI installation..."
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Success "? Azure CLI installed (Version: $($azVersion.'azure-cli'))"
} catch {
    Write-Error "? Azure CLI is not installed or not in PATH"
    Write-Error "  Please install from: https://aka.ms/installazurecli"
    exit 1
}

# Check if logged into Azure CLI
Write-Info "Checking Azure CLI authentication..."
try {
    $account = az account show --output json 2>$null | ConvertFrom-Json
    Write-Success "? Logged into Azure (Account: $($account.name))"
    Write-Info "  Subscription: $($account.name) ($($account.id))"
} catch {
    Write-Error "? Not logged into Azure CLI"
    Write-Error "  Please run: az login"
    exit 1
}

# Verify Key Vault exists
Write-Info "Verifying Key Vault exists..."
try {
    $kvExists = az keyvault show --name $KeyVaultName --output json 2>$null | ConvertFrom-Json
    Write-Success "? Key Vault found: $($kvExists.properties.vaultUri)"
} catch {
    Write-Error "? Key Vault '$KeyVaultName' not found or no access"
    Write-Error "  Please ensure the Key Vault exists and you have appropriate permissions"
    exit 1
}

# ============================================================================
# SECRET DEFINITIONS
# ============================================================================
# Define secrets to upload
# SECURITY WARNING: Replace these placeholder values with your actual secrets
# DO NOT commit actual secrets to version control!
# ============================================================================

$secrets = @{
    "Jwt--Secret" = @{
        Value = "YOUR_JWT_SECRET_HERE_AT_LEAST_32_CHARACTERS"
        Description = "JWT signing secret key for token generation and validation"
        Example = "ECommerce_business_animesh_ganai_secret_key@2025!!"
    }
    "Jwt--Issuer" = @{
        Value = "yourdomain.com"
        Description = "JWT token issuer identifier"
        Example = "yourdomain.com"
    }
    "Jwt--Audience" = @{
        Value = "yourdomain.com"
        Description = "JWT token audience identifier"
        Example = "yourdomain.com"
    }
    "EmailSettings--AppPassword" = @{
        Value = "YOUR_GMAIL_APP_PASSWORD"
        Description = "Gmail app password for sending emails"
        Example = "xxxx xxxx xxxx xxxx"
    }
    "EmailSettings--SenderEmail" = @{
        Value = "your-email@gmail.com"
        Description = "Gmail sender email address"
        Example = "aniecom.contact@gmail.com"
    }
    "ConnectionStrings--ECommerceBackendDBConnection" = @{
        Value = "YOUR_SQL_SERVER_CONNECTION_STRING"
        Description = "SQL Server database connection string"
        Example = "Server=yourserver.database.windows.net;Database=ECommerceDb;Authentication=Active Directory Default;"
    }
    "AzureBlobStorage--ConnectionString" = @{
        Value = "YOUR_AZURE_BLOB_STORAGE_CONNECTION_STRING"
        Description = "Azure Blob Storage connection string for invoice storage"
        Example = "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=..."
    }
}

Write-Info ""
Write-Warning "============================================================================"
Write-Warning "IMPORTANT: Update Secret Values Before Running!"
Write-Warning "============================================================================"
Write-Warning "The script contains placeholder values. You must update them with actual"
Write-Warning "secrets before running. Edit this script and replace:"
Write-Warning "  - YOUR_JWT_SECRET_HERE_AT_LEAST_32_CHARACTERS"
Write-Warning "  - YOUR_GMAIL_APP_PASSWORD"
Write-Warning "  - YOUR_SQL_SERVER_CONNECTION_STRING"
Write-Warning "  - YOUR_AZURE_BLOB_STORAGE_CONNECTION_STRING"
Write-Warning ""
Write-Warning "NOTE: Redis is NOT stored here - it uses passwordless Entra ID auth."
Write-Warning ""
Write-Warning "Alternatively, pass values as parameters or read from secure vault."
Write-Warning "============================================================================"
Write-Info ""

if ($DryRun) {
    Write-Info "DRY RUN MODE - No secrets will be uploaded"
    Write-Info ""
}

# ============================================================================
# UPLOAD SECRETS TO KEY VAULT
# ============================================================================

$successCount = 0
$failCount = 0
$skippedCount = 0

Write-Info "Uploading secrets to Key Vault..."
Write-Info ""

foreach ($secretName in $secrets.Keys) {
    $secretInfo = $secrets[$secretName]
    $secretValue = $secretInfo.Value

    Write-Info "------------------------------------------------"
    Write-Info "Secret: $secretName"
    Write-Info "Description: $($secretInfo.Description)"

    # Check if placeholder value
    if ($secretValue -like "YOUR_*" -or $secretValue -eq "") {
        Write-Warning "? SKIPPED - Placeholder value detected"
        Write-Warning "  Please update with actual value: $($secretInfo.Example)"
        $skippedCount++
        continue
    }

    if ($DryRun) {
        Write-Info "? Would upload: $secretName"
        Write-Info "  Value length: $($secretValue.Length) characters"
        $successCount++
    } else {
        try {
            # Upload secret to Key Vault
            $result = az keyvault secret set `
                --vault-name $KeyVaultName `
                --name $secretName `
                --value $secretValue `
                --output json 2>&1

            if ($LASTEXITCODE -eq 0) {
                Write-Success "? Successfully uploaded: $secretName"
                $successCount++
            } else {
                Write-Error "? Failed to upload: $secretName"
                Write-Error "  Error: $result"
                $failCount++
            }
        } catch {
            Write-Error "? Exception uploading: $secretName"
            Write-Error "  Error: $($_.Exception.Message)"
            $failCount++
        }
    }
}

# ============================================================================
# SUMMARY
# ============================================================================

Write-Info ""
Write-Info "============================================================================"
Write-Info "Upload Summary"
Write-Info "============================================================================"

if ($DryRun) {
    Write-Success "Dry run completed successfully"
    Write-Info "  Would upload: $successCount secrets"
} else {
    Write-Success "Successfully uploaded: $successCount secrets"
    Write-Error "Failed to upload: $failCount secrets"
}

Write-Warning "Skipped (placeholder values): $skippedCount secrets"
Write-Info ""

if ($skippedCount -gt 0) {
    Write-Warning "ACTION REQUIRED: Update placeholder values in this script before running"
    Write-Warning "Edit the `$secrets hashtable at the top of this script"
}

if ($failCount -gt 0) {
    Write-Error "Some secrets failed to upload. Check the error messages above."
    exit 1
}

# ============================================================================
# NEXT STEPS
# ============================================================================

if (-not $DryRun -and $successCount -gt 0) {
    Write-Info "============================================================================"
    Write-Success "Next Steps:"
    Write-Info "============================================================================"
    Write-Info "1. Update appsettings.json to add KeyVaultName configuration"
    Write-Info "2. Remove sensitive values from appsettings.json"
    Write-Info "3. Test application locally with Azure CLI authentication (az login)"
    Write-Info "4. Configure Managed Identity for Azure deployment"
    Write-Info "5. Grant 'Key Vault Secrets User' role to Managed Identity"
    Write-Info ""
    Write-Info "Local Development:"
    Write-Info "  - Ensure you're logged into Azure CLI: az login"
    Write-Info "  - DefaultAzureCredential will use your Azure CLI credentials"
    Write-Info ""
    Write-Info "Azure Deployment:"
    Write-Info "  - Enable Managed Identity on App Service/Container App"
    Write-Info "  - Grant RBAC permissions: Key Vault Secrets User"
    Write-Info "  - Set KeyVaultName environment variable"
    Write-Info ""
    Write-Success "Migration setup complete! ?"
}

Write-Info "============================================================================"
