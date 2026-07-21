namespace ECommerceBackend.Application.Models
{
    // Lightweight paged result wrapper for list endpoints (bounds large result sets).
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
