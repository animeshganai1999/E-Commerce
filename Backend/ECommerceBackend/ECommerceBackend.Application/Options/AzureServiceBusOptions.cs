namespace ECommerceBackend.Application.Options
{
    // Azure Service Bus settings. Only the fully-qualified namespace is stored — auth is
    // passwordless via Microsoft Entra ID (DefaultAzureCredential), so there is NO connection
    // string secret (consistent with the Key Vault / Azure Managed Redis setup).
    public class AzureServiceBusOptions
    {
        public const string SectionName = "AzureServiceBus";

        // e.g. "ecommerce-sb-animesh.servicebus.windows.net"
        public string FullyQualifiedNamespace { get; set; } = string.Empty;

        // Queue that carries post-payment fulfillment work (invoice + email + persist).
        public string FulfillmentQueueName { get; set; } = "order-fulfillment";
    }
}
