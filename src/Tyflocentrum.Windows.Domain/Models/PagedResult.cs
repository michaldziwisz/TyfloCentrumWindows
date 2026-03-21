namespace Tyflocentrum.Windows.Domain.Models;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, bool HasMoreItems);
