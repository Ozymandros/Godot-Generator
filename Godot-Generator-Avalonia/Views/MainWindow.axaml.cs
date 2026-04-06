using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Godot_Generator_Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private Avalonia.Input.IInputElement? _focusedBeforeMenu;

        public MainWindow()
        {
            InitializeComponent();
            // Track focus changes from any child control so we can cache
            // the last-focused element when the menu opens.
            this.AddHandler(InputElement.GotFocusEvent, new EventHandler<RoutedEventArgs>(OnGotFocus), handledEventsToo: true);
            // Also capture pointer presses so we can cache focused element
            // for clicks that open the menu via mouse.
            this.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>(OnPointerPressed), handledEventsToo: true);
        }

        private void Menu_Opened(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Try to capture the focused element before the menu steals focus.
                var vmDebug = this.DataContext as ViewModels.MainWindowViewModel;
                var current = this.FocusManager?.GetFocusedElement();

                // If the FocusManager reports a Menu or MenuItem (menu has already
                // taken focus), fall back to the last-focused element the VM
                // recorded via GotFocus/Pointer events.
                if (current is Avalonia.Controls.Menu || current is Avalonia.Controls.MenuItem)
                {
                    _focusedBeforeMenu = vmDebug?.GetCachedFocusedElement();
                }
                else
                {
                    _focusedBeforeMenu = current;
                }

                // Debug: show what we cached.
                if (vmDebug != null)
                {
                    vmDebug.StatusMessage = _focusedBeforeMenu != null ?
                        $"Cached focus: {_focusedBeforeMenu.GetType().Name}" :
                        "No focused control to cache";
                }
            }
            catch
            {
                // best effort
            }
        }

        // Menu click handlers that use the cached focused element
        private void Undo_Click(object? sender, RoutedEventArgs e)
        {
            ExecuteOnCachedTextBox(tb => tb.Undo(), "Undo executed", "Undo: no cached TextBox");
        }

        private void Redo_Click(object? sender, RoutedEventArgs e)
        {
            ExecuteOnCachedTextBox(tb => tb.Redo(), "Redo executed", "Redo: no cached TextBox");
        }

        private void Cut_Click(object? sender, RoutedEventArgs e)
        {
            ExecuteOnCachedTextBox(tb => tb.Cut(), "Cut executed", "Cut: no cached TextBox");
        }

        private void Copy_Click(object? sender, RoutedEventArgs e)
        {
            ExecuteOnCachedTextBox(tb => tb.Copy(), "Copy executed", "Copy: no cached TextBox");
        }

        private void Paste_Click(object? sender, RoutedEventArgs e)
        {
            ExecuteOnCachedTextBox(tb => tb.Paste(), "Paste executed", "Paste: no cached TextBox");
        }

        private void SelectAll_Click(object? sender, RoutedEventArgs e)
        {
            ExecuteOnCachedTextBox(tb => tb.SelectAll(), "Select All executed", "Select All: no cached TextBox");
        }

        private void ExecuteOnCachedTextBox(Action<Avalonia.Controls.TextBox> action, string successMessage, string failureMessage)
        {
            var vm = this.DataContext as ViewModels.MainWindowViewModel;
            if (_focusedBeforeMenu is Avalonia.Controls.TextBox tb)
            {
                // Attempt to restore focus to the cached control using the
                // window's FocusManager, then run the action on the UI thread
                // after focus has a chance to settle. This avoids the race where
                // the menu steals focus right before the command runs.
                // Some IFocusManager implementations don't expose a SetFocusedElement
                // helper; call Control.Focus() as the reliable fallback.
                try { (tb as Avalonia.Controls.Control)?.Focus(); } catch { }

                // Give the focus a little more time to settle, then execute.
                // Run on the UI thread: wait a short time so focus settles, then
                // execute the action on the dispatcher (no ConfigureAwait here).
                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(60);
                        // This continuation runs on the UI thread because InvokeAsync
                        // captures the dispatcher context for the async lambda.
                        action(tb);
                        if (vm != null) vm.StatusMessage = successMessage + " (focus restored)";
                        return;
                    }
                    catch
                    {
                        // fallback: try operating on the bound ViewModel for the panel
                        try
                        {
                            if (vm != null)
                            {
                                var (panel, text, selStart, selLen, caret) = vm.GetCachedSelectionInfo();
                                if (panel != null)
                                {
                                    // Perform operation directly on the panel's Prompt property as a fallback.
                                    // Only implement Cut/Copy/Paste/SelectAll here as simple cases.
                                    if (successMessage.StartsWith("Cut"))
                                    {
                                        var newText = text.Remove(selStart, selLen);
                                        panel.Prompt = newText;
                                        vm.StatusMessage = "Cut executed (viewmodel fallback)";
                                    }
                                    else if (successMessage.StartsWith("Copy"))
                                    {
                                        // Copy: place selection onto clipboard
                                        try
                                        {
                                            var top = Avalonia.Controls.TopLevel.GetTopLevel(this);
                                            if (top?.Clipboard is { } clipboard)
                                            {
#pragma warning disable CS0618
                                                clipboard.SetTextAsync(text.Substring(selStart, selLen)).GetAwaiter().GetResult();
#pragma warning restore CS0618
                                                vm.StatusMessage = "Copy executed (viewmodel fallback)";
                                            }
                                            else
                                            {
                                                vm.StatusMessage = "Copy failed (viewmodel fallback)";
                                            }
                                        }
                                        catch
                                        {
                                            vm.StatusMessage = "Copy failed (viewmodel fallback)";
                                        }
                                    }
                                    else if (successMessage.StartsWith("Paste"))
                                    {
                                        try
                                        {
                                            var top = Avalonia.Controls.TopLevel.GetTopLevel(this);
                                            if (top?.Clipboard is { } clipboard)
                                            {
#pragma warning disable CS0618
                                                var clip = clipboard.GetTextAsync().GetAwaiter().GetResult() ?? "";
#pragma warning restore CS0618
                                                var newText = text.Insert(caret, clip);
                                                panel.Prompt = newText;
                                                vm.StatusMessage = "Paste executed (viewmodel fallback)";
                                            }
                                            else
                                            {
                                                vm.StatusMessage = "Paste failed (viewmodel fallback)";
                                            }
                                        }
                                        catch
                                        {
                                            vm.StatusMessage = "Paste failed (viewmodel fallback)";
                                        }
                                    }
                                    else if (successMessage.StartsWith("Select All"))
                                    {
                                        // Selecting all in VM is a no-op visually, but we can
                                        // set selection info in the VM if needed.
                                        vm.StatusMessage = "Select All executed (viewmodel fallback)";
                                    }
                                }
                                else
                                {
                                    vm.StatusMessage = successMessage + " (error executing)";
                                }
                            }
                        }
                        catch
                        {
                            if (vm != null) vm.StatusMessage = successMessage + " (error executing)";
                        }
                    }
                }, DispatcherPriority.Background);
            }
            else
            {
                if (vm != null) vm.StatusMessage = failureMessage;
            }
        }

        private void OnGotFocus(object? sender, RoutedEventArgs e)
        {
            try
            {
                var vm = this.DataContext as ViewModels.MainWindowViewModel;
                if (vm != null && e.Source is IInputElement ie)
                {
                    // Only cache real input controls (TextBox etc.). Ignore
                    // focus events coming from the Menu/MenuItem so we don't
                    // overwrite the last useful focus with the menu.
                    if (ie is Avalonia.Controls.TextBox)
                    {
                        vm.UpdateLastFocused(ie);
                    }
                }
            }
            catch
            {
                // best-effort; don't crash the UI
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            try
            {
                // Record the currently focused element before focus changes.
                // We avoid visual-tree traversal; simply capture the active
                // focused element at pointer press time. This ensures menu
                // clicks will preserve the last-focused control for commands.
                var vm = this.DataContext as ViewModels.MainWindowViewModel;
                if (vm != null)
                {
                    var current = this.FocusManager?.GetFocusedElement();
                    if (current is Avalonia.Input.IInputElement ie)
                    {
                        // Only cache when the current focused element is a TextBox
                        // (or another input element we care about).
                        if (ie.GetType().Name == "TextBox" || ie is Avalonia.Controls.TextBox)
                            vm.UpdateLastFocused(ie);
                    }
                }
            }
            catch
            {
                // best-effort
            }
        }
    }
}
