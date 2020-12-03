using System;
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
            this.WhenAnyValue(x => x.ViewModel.MessageModels.Count)
                .Subscribe(args => ScrollToTop(args));
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
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    list.ScrollIntoView(count);
                });
        }
    }
}