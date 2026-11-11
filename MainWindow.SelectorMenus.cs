using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace EriMixer;

public partial class MainWindow
{
    private Popup? selectorPopup;

    private Popup CreateSelectorPopup(
        System.Windows.Controls.Button target,
        PlacementMode placement,
        double minWidth,
        out StackPanel options)
    {
        CloseSelectorPopup();

        var popup = new Popup
        {
            PlacementTarget = target,
            Placement = placement,
            StaysOpen = false,
            AllowsTransparency = true,
            Focusable = true
        };

        var surface = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            MinWidth = minWidth,
            Focusable = true,
            SnapsToDevicePixels = true
        };
        surface.SetResourceReference(Border.BackgroundProperty, "ThemeMenuBackground");
        surface.SetResourceReference(Border.BorderBrushProperty, "ThemeSelectorBorder");
        surface.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                CloseSelectorPopup();
                e.Handled = true;
            }
        };
        surface.PreviewMouseRightButtonDown += (_, e) =>
        {
            CloseSelectorPopup();
            e.Handled = true;
        };

        options = new StackPanel();
        surface.Child = options;
        popup.Child = surface;
        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(selectorPopup, popup))
                selectorPopup = null;
        };
        selectorPopup = popup;
        popup.IsOpen = true;
        return popup;
    }

    private System.Windows.Controls.Button CreateSelectorOption(
        string label,
        bool isSelected,
        Action<System.Windows.Controls.Button> onClick)
    {
        var layout = new Grid { Margin = new Thickness(9, 4, 10, 4) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());

        var checkBox = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        checkBox.SetResourceReference(Border.BackgroundProperty,
            isSelected ? "ThemeAccent" : "ThemeMenuBackground");
        checkBox.SetResourceReference(Border.BorderBrushProperty,
            isSelected ? "ThemeAccent" : "ThemeSelectorBorder");
        var checkMark = new TextBlock
        {
            Text = "✓",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed
        };
        checkMark.SetResourceReference(TextBlock.ForegroundProperty, "ThemeHeaderText");
        checkBox.Child = checkMark;
        Grid.SetColumn(checkBox, 0);
        layout.Children.Add(checkBox);

        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = label
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "ThemeText");
        Grid.SetColumn(text, 1);
        layout.Children.Add(text);

        var button = new System.Windows.Controls.Button
        {
            Content = layout,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            Focusable = true,
            ToolTip = label,
            Tag = (checkBox, checkMark)
        };
        button.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ThemeMenuBackground");
        button.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "ThemeText");
        button.Template = CreateSelectorOptionTemplate();
        button.Click += (_, _) => onClick(button);
        button.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                CloseSelectorPopup();
                e.Handled = true;
            }
        };
        button.PreviewMouseRightButtonDown += (_, e) =>
        {
            CloseSelectorPopup();
            e.Handled = true;
        };
        SetSelectorOptionState(button, isSelected);
        return button;
    }

    private static ControlTemplate CreateSelectorOptionTemplate()
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Surface";
        border.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding(nameof(System.Windows.Controls.Control.Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty,
            new System.Windows.Data.Binding(nameof(System.Windows.Controls.Control.BorderBrush)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderThicknessProperty,
            new System.Windows.Data.Binding(nameof(System.Windows.Controls.Control.BorderThickness)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(ContentPresenter.ContentProperty,
            new System.Windows.Data.Binding(nameof(System.Windows.Controls.ContentControl.Content)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Stretch);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;

        var hover = new Trigger
        {
            Property = System.Windows.Controls.Control.IsMouseOverProperty,
            Value = true
        };
        hover.Setters.Add(new Setter(Border.BackgroundProperty,
            new DynamicResourceExtension("ThemeSelectorHover"), "Surface"));
        template.Triggers.Add(hover);

        var focused = new Trigger
        {
            Property = System.Windows.Controls.Control.IsKeyboardFocusedProperty,
            Value = true
        };
        focused.Setters.Add(new Setter(Border.BorderBrushProperty,
            new DynamicResourceExtension("ThemeAccentLight"), "Surface"));
        focused.Setters.Add(new Setter(Border.BorderThicknessProperty,
            new Thickness(1), "Surface"));
        template.Triggers.Add(focused);

        var pressed = new Trigger
        {
            Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,
            Value = true
        };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.78, "Surface"));
        template.Triggers.Add(pressed);
        return template;
    }

    private static void SetSelectorOptionState(System.Windows.Controls.Button option, bool isSelected)
    {
        if (option.Tag is not ValueTuple<Border, TextBlock> parts)
            return;

        parts.Item1.SetResourceReference(Border.BackgroundProperty,
            isSelected ? "ThemeAccent" : "ThemeMenuBackground");
        parts.Item1.SetResourceReference(Border.BorderBrushProperty,
            isSelected ? "ThemeAccent" : "ThemeSelectorBorder");
        parts.Item2.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CloseSelectorPopup()
    {
        if (selectorPopup is { } popup)
        {
            selectorPopup = null;
            popup.IsOpen = false;
        }
    }
}
