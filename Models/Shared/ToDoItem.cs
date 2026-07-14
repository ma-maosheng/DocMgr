using System;

namespace DocMgr.Models.Shared
{
    public class ToDoItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string BizType { get; set; } = string.Empty;
        public int BizId { get; set; }
        public string BizNo { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public string Priority { get; set; } = "普通";
    }
}