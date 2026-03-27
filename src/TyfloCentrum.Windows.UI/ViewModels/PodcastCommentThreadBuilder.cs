using TyfloCentrum.Windows.Domain.Models;

namespace TyfloCentrum.Windows.UI.ViewModels;

public static class PodcastCommentThreadBuilder
{
    public static CommentItemViewModel[] Build(IReadOnlyList<WordPressComment> comments)
    {
        if (comments.Count == 0)
        {
            return [];
        }

        var itemsById = comments.ToDictionary(comment => comment.Id, comment => new CommentItemViewModel(comment));
        var commentsById = comments.ToDictionary(comment => comment.Id);
        var sourceIndexByCommentId = comments
            .Select((comment, index) => new { comment.Id, Index = index })
            .ToDictionary(item => item.Id, item => item.Index);
        var childrenByParentId = comments
            .GroupBy(comment => comment.ParentId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var commentDisplayOrderComparer = Comparer<WordPressComment>.Create(CompareForDisplay);

        var ordered = new List<CommentItemViewModel>(comments.Count);
        var roots = comments
            .Where(comment => comment.ParentId == 0 || !commentsById.ContainsKey(comment.ParentId))
            .OrderBy(comment => comment, commentDisplayOrderComparer);

        foreach (var root in roots)
        {
            AppendComment(root, 0, null);
        }

        return ordered.ToArray();

        void AppendComment(WordPressComment comment, int depth, string? parentAuthorName)
        {
            var item = itemsById[comment.Id];
            item.ApplyThreadContext(depth, parentAuthorName);
            ordered.Add(item);

            if (!childrenByParentId.TryGetValue(comment.Id, out var children))
            {
                return;
            }

            foreach (var child in children.OrderBy(comment => comment, commentDisplayOrderComparer))
            {
                AppendComment(child, depth + 1, comment.AuthorName);
            }
        }

        int CompareForDisplay(WordPressComment left, WordPressComment right)
        {
            var leftTimestamp = left.PublishedAtUtc;
            var rightTimestamp = right.PublishedAtUtc;

            if (leftTimestamp.HasValue && rightTimestamp.HasValue)
            {
                var timestampComparison = leftTimestamp.Value.CompareTo(rightTimestamp.Value);
                if (timestampComparison != 0)
                {
                    return timestampComparison;
                }
            }

            if (leftTimestamp.HasValue != rightTimestamp.HasValue)
            {
                return leftTimestamp.HasValue ? -1 : 1;
            }

            // WordPress returns comments newest-first by default. When timestamps are equal
            // or unavailable, reverse the source order so the display remains oldest-first.
            var sourceOrderComparison = sourceIndexByCommentId[right.Id].CompareTo(sourceIndexByCommentId[left.Id]);
            if (sourceOrderComparison != 0)
            {
                return sourceOrderComparison;
            }

            return left.Id.CompareTo(right.Id);
        }
    }
}
