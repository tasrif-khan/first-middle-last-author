using PropertyChanged;
using System.ComponentModel;

namespace Research_Author_Publication_Data
{
    [AddINotifyPropertyChangedInterface]
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = (s, e) => { };
    }
}
