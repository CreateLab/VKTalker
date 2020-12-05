using ReactiveUI;

namespace VKTalker.Models
{
    public class DialogModel : BaseModel
    {
        private string _name;
        private string _message;
        private string _image;
        public string SecondName { get; set; }

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name , value);
        }

        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message , value);
        }

        public string Image
        {
            get => _image;
            set => this.RaiseAndSetIfChanged(ref _image , value);
        }

        public long ChatId { get; set; }
    }
}