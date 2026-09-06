namespace ECommerceBackend.Application.Exceptions
{
    public sealed class OrderStateConflictException : Exception
    {
        public OrderStateConflictException(string message) : base(message)
        {
        }
    }
}
