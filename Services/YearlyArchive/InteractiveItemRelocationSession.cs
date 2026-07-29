using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    public sealed class InteractiveItemRelocationSession : IInteractiveItemRelocationSession
    {
        private IReadOnlyList<InteractiveItemRelocationSource> _sources = [];

        public IReadOnlyList<InteractiveItemRelocationSource> Sources => _sources;

        public InteractiveItemRelocationSource? Source => _sources.Count > 0 ? _sources[0] : null;

        public event Action? SourceChanged;

        public void SetSource(InteractiveItemRelocationSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            SetSources([source]);
        }

        public void SetSources(IReadOnlyList<InteractiveItemRelocationSource> sources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            if (sources.Count == 0)
            {
                ClearSource();
                return;
            }

            foreach (var item in sources)
            {
                ArgumentNullException.ThrowIfNull(item);
            }

            _sources = sources.ToList();
            SourceChanged?.Invoke();
        }

        public void ClearSource()
        {
            if (_sources.Count == 0)
            {
                return;
            }

            _sources = [];
            SourceChanged?.Invoke();
        }
    }
}
