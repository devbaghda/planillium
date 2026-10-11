using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Planillium.App.Controls;

/// Lays children out left to right in the order they were added, and starts a new line when the
/// next child would not fit. Every child keeps its natural (desired) size; nothing is stretched.
/// A child wider than the whole row gets a line to itself.
public sealed class WrapFlowPanel : Panel
{
    private double _horizontalSpacing;
    private double _verticalSpacing;

    public double HorizontalSpacing
    {
        get => _horizontalSpacing;
        set { _horizontalSpacing = value; InvalidateMeasure(); }
    }

    public double VerticalSpacing
    {
        get => _verticalSpacing;
        set { _verticalSpacing = value; InvalidateMeasure(); }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Flow(availableSize.Width, arrange: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Flow(finalSize.Width, arrange: true);
        return finalSize;
    }

    private Size Flow(double availableWidth, bool arrange)
    {
        double x = 0, y = 0, lineHeight = 0, widest = 0;
        bool lineHasItems = false;
        foreach (var child in Children)
        {
            var w = child.DesiredSize.Width;
            var h = child.DesiredSize.Height;
            if (lineHasItems && x + HorizontalSpacing + w > availableWidth)
            {
                y += lineHeight + VerticalSpacing;
                x = 0;
                lineHeight = 0;
                lineHasItems = false;
            }
            if (lineHasItems)
                x += HorizontalSpacing;
            if (arrange)
                child.Arrange(new Rect(x, y, w, h));
            x += w;
            lineHeight = Math.Max(lineHeight, h);
            widest = Math.Max(widest, x);
            lineHasItems = true;
        }
        return new Size(widest, y + lineHeight);
    }
}
