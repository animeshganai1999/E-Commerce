using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using ECommerceBackend.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ECommerceBackend.Application.Services
{
    /// <summary>
    /// Service implementation for interacting with Azure Key Vault to manage secrets.
    /// Uses DefaultAzureCredential for authentication which supports multiple authentication methods.
    /// </summary>
    public class KeyVaultService : IKeyVaultService
    {
        private readonly SecretClient _secretClient;

        /// <summary>
        /// Initializes a new instance of the KeyVaultService class.
        /// </summary>
        /// <param name="configuration">The configuration instance to retrieve Key Vault name.</param>
        public KeyVaultService(IConfiguration configuration)
        {
            var keyVaultName = configuration["KeyVaultName"];
            if (string.IsNullOrWhiteSpace(keyVaultName))
            {
                throw new ArgumentException("KeyVaultName configuration is missing or empty.");
            }

            var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
            _secretClient = new SecretClient(keyVaultUri, new DefaultAzureCredential());
        }

        /// <summary>
        /// Retrieves a secret value from Azure Key Vault by its name.
        /// </summary>
        /// <param name="secretName">The name of the secret to retrieve.</param>
        /// <returns>The secret value as a string.</returns>
        /// <exception cref="RequestFailedException">Thrown when the secret is not found or access is denied.</exception>
        public async Task<string> GetSecretAsync(string secretName)
        {
            try
            {
                KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);
                return secret.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new KeyNotFoundException($"Secret '{secretName}' not found in Key Vault.", ex);
            }
            catch (RequestFailedException ex) when (ex.Status == 403)
            {
                throw new UnauthorizedAccessException($"Access denied to secret '{secretName}' in Key Vault.", ex);
            }
        }

        /// <summary>
        /// Retrieves a secret from Azure Key Vault including its properties.
        /// </summary>
        /// <param name="secretName">The name of the secret to retrieve.</param>
        /// <returns>The KeyVaultSecret object containing the secret value and properties.</returns>
        /// <exception cref="RequestFailedException">Thrown when the secret is not found or access is denied.</exception>
        public async Task<KeyVaultSecret> GetSecretWithPropertiesAsync(string secretName)
        {
            try
            {
                return await _secretClient.GetSecretAsync(secretName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new KeyNotFoundException($"Secret '{secretName}' not found in Key Vault.", ex);
            }
            catch (RequestFailedException ex) when (ex.Status == 403)
            {
                throw new UnauthorizedAccessException($"Access denied to secret '{secretName}' in Key Vault.", ex);
            }
        }

        /// <summary>
        /// Sets or updates a secret in Azure Key Vault.
        /// </summary>
        /// <param name="secretName">The name of the secret to set.</param>
        /// <param name="secretValue">The value of the secret.</param>
        /// <returns>The created or updated KeyVaultSecret.</returns>
        /// <exception cref="RequestFailedException">Thrown when the operation fails.</exception>
        public async Task<KeyVaultSecret> SetSecretAsync(string secretName, string secretValue)
        {
            try
            {
                return await _secretClient.SetSecretAsync(secretName, secretValue);
            }
            catch (RequestFailedException ex) when (ex.Status == 403)
            {
                throw new UnauthorizedAccessException($"Access denied to set secret '{secretName}' in Key Vault.", ex);
            }
        }

        /// <summary>
        /// Deletes a secret from Azure Key Vault.
        /// </summary>
        /// <param name="secretName">The name of the secret to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="RequestFailedException">Thrown when the operation fails.</exception>
        public async Task DeleteSecretAsync(string secretName)
        {
            try
            {
                var operation = await _secretClient.StartDeleteSecretAsync(secretName);
                await operation.WaitForCompletionAsync();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Secret already deleted or doesn't exist
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 403)
            {
                throw new UnauthorizedAccessException($"Access denied to delete secret '{secretName}' in Key Vault.", ex);
            }
        }

        /// <summary>
        /// Lists all secret names in Azure Key Vault.
        /// </summary>
        /// <returns>An enumerable collection of secret names.</returns>
        /// <exception cref="RequestFailedException">Thrown when the operation fails.</exception>
        public async Task<IEnumerable<string>> ListSecretNamesAsync()
        {
            try
            {
                var secretNames = new List<string>();
                await foreach (var secretProperties in _secretClient.GetPropertiesOfSecretsAsync())
                {
                    secretNames.Add(secretProperties.Name);
                }
                return secretNames;
            }
            catch (RequestFailedException ex) when (ex.Status == 403)
            {
                throw new UnauthorizedAccessException("Access denied to list secrets in Key Vault.", ex);
            }
        }
    }
}
