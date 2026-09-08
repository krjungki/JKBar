// Describes the small part of a feed entry that JKBar displays and opens.
namespace JKBar.Core.News;

public sealed record NewsItem(string Title, Uri Link, DateTimeOffset? PublishedAt);