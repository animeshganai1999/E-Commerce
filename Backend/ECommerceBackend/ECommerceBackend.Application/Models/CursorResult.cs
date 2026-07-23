namespace ECommerceBackend.Application.Models
{
    // Cursor (keyset) pagination result for "Load more" style endpoints.
    // Unlike PagedResult (OFFSET-based), this scales to arbitrarily deep pages
    // because the database seeks directly to the cursor position via an index,
    // instead of counting through all skipped rows.
    public class CursorResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = new List<T>();

        // The cursor to pass as "afterId" on the next request.
        // Null when there are no more items (end of the list).
        public int? NextCursor { get; set; }

        // Convenience flag for the client's "Load more" button.
        public bool HasMore => NextCursor.HasValue;

        public int PageSize { get; set; }
    }
}
