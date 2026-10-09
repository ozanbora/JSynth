using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JuliaSynth.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;

namespace JuliaSynth.Views;

/// <summary>
/// A frame on the canvas: a titled box behind the nodes in it, and below the wires. Its title bar drags it with them,
/// and its corner grip resizes it.
/// </summary>
public partial class FrameNode : Controls.Node
{
    private Model.FrameNode? _frame;

    // Where a resize started: the pointer on the canvas, and the frame's size then.
    private (Point Point, int Columns, int Rows)? _resizeStart;

    public FrameNode()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_frame is not null)
        {
            _frame.PropertyChanged -= FrameChanged;
        }

        _frame = DataContext as Model.FrameNode;
        if (_frame is not null)
        {
            _frame.PropertyChanged += FrameChanged;
        }

        Resize();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Behind the wires, which are behind the nodes.
        if (Parent is ContentPresenter container)
        {
            container.ZIndex = -2;
        }
    }

    private void FrameChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Model.FrameNode.Columns) or nameof(Model.FrameNode.Rows))
        {
            Resize();
        }
    }

    private void Resize()
    {
        if (_frame is not null)
        {
            (Width, Height) = (_frame.Columns * Constants.WIDTH, _frame.Rows * Constants.HEIGHT);
        }
    }

    // The nodes in the frame made one, named after it.
    private void Group_Click(object? sender, RoutedEventArgs e)
    {
        Group();
    }

    // The title double-clicked, but on its buttons: the same. (The frame has the pointer while it is pressed: what was
    // double-clicked is what is under it.)
    private void Frame_DoubleTapped(object? sender, TappedEventArgs e)
    {
        Point point = e.GetPosition(TitleBar);
        bool onTitle = point.X >= 0 && point.Y >= 0 && point.X < TitleBar.Bounds.Width && point.Y < TitleBar.Bounds.Height;
        if (onTitle && (TitleBar.InputHitTest(point) as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is null)
        {
            Group();
            e.Handled = true;
        }
    }

    private void Group()
    {
        if (_frame is not null)
        {
            MainViewModel.SelectOnly(_frame);
            _ = MainViewModel.GroupSelection();
        }
    }

    // A note written: the frame drawn again around its nodes, with room for it.
    private void Note_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_frame is not null && MainViewModel.Nodes.Where(n => n is not Model.FrameNode && _frame.Holds(n)).ToList() is { Count: > 0 } held)
        {
            _frame.Fit(held);
        }
    }

    private void Grip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_frame is null || Parent is not Visual canvas || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _resizeStart = (e.GetPosition(canvas), _frame.Columns, _frame.Rows);
        e.Pointer.Capture(Grip);
        e.Handled = true;
    }

    private void Grip_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizeStart is not (Point start, int columns, int rows) || _frame is null || Parent is not Visual canvas)
        {
            return;
        }

        Vector moved = e.GetPosition(canvas) - start;
        _frame.Columns = Math.Min(columns + (int)Math.Round(moved.X / Constants.WIDTH), Constants.COLUMNS - _frame.Column);
        _frame.Rows = Math.Min(rows + (int)Math.Round(moved.Y / Constants.HEIGHT), Constants.ROWS - _frame.Row);
    }

    private void Grip_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizeStart is not null)
        {
            _resizeStart = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }
}
