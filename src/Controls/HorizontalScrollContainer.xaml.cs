// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace BlendHub.Controls;

public sealed partial class HorizontalScrollContainer : UserControl
{
    private bool _canScrollLeft = false;
    private bool _canScrollRight = false;
    private bool _isHovered = false;

    public HorizontalScrollContainer()
    {
        this.InitializeComponent();
        this.PointerEntered += OnPointerEntered;
        this.PointerExited += OnPointerExited;
    }

    public object Source
    {
        get => (object)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(object), typeof(HorizontalScrollContainer), new PropertyMetadata(null));

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isHovered = true;
        UpdateArrowVisibility();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isHovered = false;
        UpdateArrowVisibility();
    }

    private void Scroller_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
    {
        _canScrollLeft = e.FinalView.HorizontalOffset > 1;
        _canScrollRight = e.FinalView.HorizontalOffset < scroller.ScrollableWidth - 1;
        UpdateArrowVisibility();
    }

    private void ScrollBackBtn_Click(object sender, RoutedEventArgs e)
    {
        scroller.ChangeView(scroller.HorizontalOffset - scroller.ViewportWidth, null, null);
        ScrollForwardBtn.Focus(FocusState.Programmatic);
    }

    private void ScrollForwardBtn_Click(object sender, RoutedEventArgs e)
    {
        scroller.ChangeView(scroller.HorizontalOffset + scroller.ViewportWidth, null, null);
        ScrollBackBtn.Focus(FocusState.Programmatic);
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScrollState();
    }

    private void UpdateScrollState()
    {
        _canScrollLeft = scroller.HorizontalOffset > 1;
        _canScrollRight = scroller.ScrollableWidth > 0 && scroller.HorizontalOffset < scroller.ScrollableWidth - 1;
        UpdateArrowVisibility();
    }

    private void UpdateArrowVisibility()
    {
        // Only show arrows when hovered AND there's content to scroll in that direction
        ScrollBackBtn.Visibility = (_isHovered && _canScrollLeft) ? Visibility.Visible : Visibility.Collapsed;
        ScrollForwardBtn.Visibility = (_isHovered && _canScrollRight) ? Visibility.Visible : Visibility.Collapsed;
    }
}
