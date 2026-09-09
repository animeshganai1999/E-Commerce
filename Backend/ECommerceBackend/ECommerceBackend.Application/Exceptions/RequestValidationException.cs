namespace ECommerceBackend.Application.Exceptions
{
    public sealed class RequestValidationException : Exception
    {
        public RequestValidationException(string field, string error)
            : this(new Dictionary<string, string[]> { [field] = [error] })
        {
        }

        public RequestValidationException(IDictionary<string, string[]> errors)
            : base("One or more request validation errors occurred.")
        {
            Errors = new Dictionary<string, string[]>(errors);
        }

        public IReadOnlyDictionary<string, string[]> Errors { get; }
    }
}
