using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Godot_Generator_Avalonia.ViewModels;

namespace Godot_Generator_Avalonia.Views
{
    /// <summary>
    /// Single modality generation panel.
    /// </summary>
    public partial class GenerationPanelView : UserControl
    {
        /// <summary>Initializes the generation panel view.</summary>
        public GenerationPanelView()
        {
            InitializeComponent();
        }

        public void ChildTextBox_GotFocus(object? sender, GotFocusEventArgs e)
            => HandleChildTextBoxFocused(sender);

        public void ChildTextBox_GotFocus(object? sender, RoutedEventArgs e)
            => HandleChildTextBoxFocused(sender);

        private void HandleChildTextBoxFocused(object? sender)
        {
            try
            {
                if (sender is IInputElement ie)
                {
                    var root = this.VisualRoot as Window;
                    if (root?.DataContext is MainWindowViewModel mwvm)
                    {
                        // If the focused control is a TextBox, capture selection and
                        // caret info so the VM can act on the bound text when the
                        // menu steals focus.
                        if (sender is Avalonia.Controls.TextBox tb)
                        {
                            // Try to locate the panel VM that owns this TextBox by
                            // walking the DataContext chain (UserControl -> panel VM).
                            GenerationPanelViewModel? panelVm = null;
                            if (this.DataContext is GenerationPanelViewModel gpvm)
                                panelVm = gpvm;

                            mwvm.UpdateLastFocusedWithSelection(ie, tb.Text, tb.SelectionStart, tb.SelectionEnd - tb.SelectionStart, tb.CaretIndex, panelVm);
                        }
                        else
                        {
                            mwvm.UpdateLastFocused(ie);
                        }
                    }
                }
            }
            catch
            {
                // best-effort
            }
        }

        public void ChildTextBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            // no-op; present so XAML can attach the event without errors.
        }
    }
}
