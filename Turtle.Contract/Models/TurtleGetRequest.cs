namespace Turtle.Contract.Models
{
    public sealed class TurtleGetRequest
    {
        public bool IsGetRoots { get; set; }
        public bool IsGetSelectors { get; set; }
        public bool IsGetBookmarks { get; set; } = true;
        public Guid[] GetChildrenIds { get; set; } = [];
        public Guid[] GetParentsIds { get; set; } = [];
        public long LastId { get; set; } = -1;
    }
}
