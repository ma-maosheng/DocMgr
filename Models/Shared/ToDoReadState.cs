using System;

namespace DocMgr.Models.Shared
{
    public class ToDoReadState
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string ToDoId { get; set; } = string.Empty;

        public DateTime ReadAt { get; set; }
    }
}