using Azure.Security.KeyVault.Secrets;

namespace ECommerceBackend.Application.Interfaces
{
    /// <summary>
    /// Service interface for interacting with Azure Key Vault to manage secrets.
    /// </summary>
    public interface IKeyVaultService
    {
        /// <summary>
        /// Retrieves a secret value from Azure Key Vault by its name.
        /// </summary>
        /// <param name="secretName">The name of the secret to retrieve.</param>
        /// <returns>The secret value as a string.</returns>
        Task<string> GetSecretAsync(string secretName);

        /// <summary>
        /// Retrieves a secret from Azure Key Vault including its properties.
        /// </summary>
        /// <param name="secretName">The name of the secret to retrieve.</param>
        /// <returns>The KeyVaultSecret object containing the secret value and properties.</returns>
        Task<KeyVaultSecret> GetSecretWithPropertiesAsync(string secretName);

        /// <summary>
        /// Sets or updates a secret in Azure Key Vault.
        /// </summary>
        /// <param name="secretName">The name of the secret to set.</param>
        /// <param name="secretValue">The value of the secret.</param>
        /// <returns>The created or updated KeyVaultSecret.</returns>
        Task<KeyVaultSecret> SetSecretAsync(string secretName, string secretValue);

        /// <summary>
        /// Deletes a secret from Azure Key Vault.
        /// </summary>
        /// <param name="secretName">The name of the secret to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteSecretAsync(string secretName);

        /// <summary>
        /// Lists all secret names in Azure Key Vault.
        /// </summary>
        /// <returns>An enumerable collection of secret names.</returns>
        Task<IEnumerable<string>> ListSecretNamesAsync();
    }
}
