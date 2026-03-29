using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace OnFlight.App.Views;

public class DialogButton
{
    public string Text { get; set; } = string.Empty;
    public string? ResultId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsDestructive { get; set; }
}

public partial class ConfirmDialog : Window
{
    private string? _resultId;

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public static async Task<string?> ShowAsync(
        Window owner,
        string title,
        string message,
        params DialogButton[] buttons)
    {
        var dialog = new ConfirmDialog();
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.BuildButtons(buttons);
        return await dialog.ShowDialog<string?>(owner);
    }

    private void BuildButtons(DialogButton[] buttons)
    {
        foreach (var def in buttons)
        {
            var btn = new Button
            {
                Content = def.Text,
                Padding = new Thickness(16, 6),
                Cursor = new Cursor(StandardCursorType.Hand),
                FontSize = 13,
                Tag = def.ResultId,
            };

            if (def.IsDestructive)
            {
                btn.Classes.Add("Fab");
                btn.Background = new SolidColorBrush(Color.Parse("#FF3B30"));
                btn.Foreground = Brushes.White;
            }
            else if (def.IsPrimary)
            {
                btn.Classes.Add("Fab");
            }
            else
            {
                btn.Classes.Add("PlainIconBtn");
                btn.CornerRadius = new CornerRadius(8);
            }

            btn.Click += OnButtonClick;
            ButtonsPanel.Items.Add(btn);
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            _resultId = btn.Tag as string;
            Close(_resultId);
        }
    }
}
