using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using VKTalker.Models;

namespace VKTalker.Controls
{
    public class ScrollingListBox:ListBox
    {
        private ScrollModel _scrollModel;

        public ScrollModel ScrollModel
        {
            get => _scrollModel;
            set
            {
                if (value is not null && value.Count > 0)
                {
                    ScrollIntoView(value.Count-1);
                }
                SetAndRaise(ScrollModelProperty,ref _scrollModel, value);
                
            }
        }
        public static readonly DirectProperty<ScrollingListBox, ScrollModel>
            ScrollModelProperty =
                AvaloniaProperty.RegisterDirect<ScrollingListBox, ScrollModel>(
                    nameof(ScrollModel),
                    editor => editor.ScrollModel,
                    (editor, s) => editor.ScrollModel = s,
                    null, BindingMode.TwoWay);
    }
}