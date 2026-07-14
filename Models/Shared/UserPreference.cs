using System;

namespace DocMgr.Models.Shared
{
    public class UserPreference
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public bool EnableToDoPopup { get; set; } = true;

        public bool EnableToDoBadge { get; set; } = true;

        public int ToDoRefreshSeconds { get; set; } = 15;

        public int ToDoTopN { get; set; } = 20;

        public bool MarkAllAsReadOnAcknowledge { get; set; } = true;

        public DateTime UpdatedAt { get; set; }
    }
}