using Photobooth.Core;

namespace Photobooth.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private string _title = "Photobooth";
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
