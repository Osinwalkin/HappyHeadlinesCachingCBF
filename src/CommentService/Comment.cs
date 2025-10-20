public class Comment
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}