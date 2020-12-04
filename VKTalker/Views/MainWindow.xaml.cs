using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;
using VKTalker.ViewModels;

namespace VKTalker.Views
{
    public class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        private ListBox list;

        public MainWindow()
        {
            InitializeComponent();
            this.WhenAnyValue(x => x.ViewModel.MessageModels.Count).ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ScrollToTop);
            list = this.Get<ListBox>("MessageListBox");
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ScrollToTop(int count)
        {
            if (count > 0)
                list.ScrollIntoView(count);
        }
    }
}