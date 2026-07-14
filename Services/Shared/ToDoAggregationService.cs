using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Shared
{
    public class ToDoAggregationService : IToDoService
    {
        private readonly IEnumerable<IToDoProvider> _providers;

        public ToDoAggregationService(IEnumerable<IToDoProvider> providers)
        {
            _providers = providers;
        }

        /// <summary>
        /// 聚合各业务域待办。待办项是否展示由业务办结状态决定，「已读」标记不影响待办归属。
        /// </summary>
        public async Task<List<ToDoItem>> GetMyToDosAsync(User currentUser, int topN = 20)
        {
            if (currentUser == null)
            {
                return new List<ToDoItem>();
            }

            var all = new List<ToDoItem>();

            foreach (var provider in _providers)
            {
                var items = await provider.GetToDosAsync(currentUser);
                if (items != null && items.Count > 0)
                {
                    all.AddRange(items);
                }
            }

            var merged = all
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderByDescending(x => x.Priority == "高")
                .ThenByDescending(x => x.CreatedTime)
                .ToList();

            return merged
                .Take(topN)
                .ToList();
        }

        public async Task MarkAsReadAsync(User currentUser, string toDoId)
        {
            await Task.CompletedTask;
        }

        public async Task MarkAsReadBatchAsync(User currentUser, IEnumerable<string> toDoIds)
        {
            await Task.CompletedTask;
        }
    }
}