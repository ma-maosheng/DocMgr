using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    public sealed class InteractiveItemRelocationSession : IInteractiveItemRelocationSession
    {
        private InteractiveItemRelocationSource? _source;

        public InteractiveItemRelocationSource? Source => _source;

        public event Action? SourceChanged;

        public void SetSource(InteractiveItemRelocationSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            _source = source;
            SourceChanged?.Invoke();
        }

        public void ClearSource()
        {
            if (_source == null)
            {
                return;
            }

            _source = null;
            SourceChanged?.Invoke();
        }
    }
}
