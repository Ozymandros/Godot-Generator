using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.ApplicationLifetimes;
using Godot_Generator_Avalonia.ViewModels;

namespace Godot_Generator_Avalonia.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => InitializeNativeMenu();
            // Try a first-time init in case DataContext already set
            InitializeNativeMenu();
        }

        private void InitializeNativeMenu()
        {
            try
            {
                if (this.DataContext is not MainWindowViewModel vm)
                    return;

                var menu = new NativeMenu();

                // File
                var file = new NativeMenuItem("File") { Menu = new NativeMenu() };
                file.Menu.Items.Add(new NativeMenuItem("New Project") { Command = vm.NewProjectCommand });
                file.Menu.Items.Add(new NativeMenuItem("Open Project...") { Command = vm.OpenProjectCommand });
                file.Menu.Items.Add(new NativeMenuItemSeparator());
                file.Menu.Items.Add(new NativeMenuItem("Exit") { Command = vm.ExitApplicationCommand });

                // View
                var view = new NativeMenuItem("View") { Menu = new NativeMenu() };
                view.Menu.Items.Add(new NativeMenuItem("Reload") { Command = vm.ReloadConfigurationCommand });
                view.Menu.Items.Add(new NativeMenuItem("Force Reload") { Command = vm.ForceReloadCommand });
                view.Menu.Items.Add(new NativeMenuItem("Toggle Developer Tools") { Command = vm.ToggleDevToolsCommand });
                view.Menu.Items.Add(new NativeMenuItemSeparator());
                view.Menu.Items.Add(new NativeMenuItem("Reset Zoom") { Command = vm.ResetZoomCommand });
                view.Menu.Items.Add(new NativeMenuItem("Zoom In") { Command = vm.ZoomInCommand });
                view.Menu.Items.Add(new NativeMenuItem("Zoom Out") { Command = vm.ZoomOutCommand });
                view.Menu.Items.Add(new NativeMenuItemSeparator());
                view.Menu.Items.Add(new NativeMenuItem("Toggle Fullscreen") { Command = vm.ToggleFullscreenCommand });

                // Edit
                var edit = new NativeMenuItem("Edit") { Menu = new NativeMenu() };
                edit.Menu.Items.Add(new NativeMenuItem("Undo") { Command = vm.UndoCommand });
                edit.Menu.Items.Add(new NativeMenuItem("Redo") { Command = vm.RedoCommand });
                edit.Menu.Items.Add(new NativeMenuItemSeparator());
                edit.Menu.Items.Add(new NativeMenuItem("Cut") { Command = vm.CutCommand });
                edit.Menu.Items.Add(new NativeMenuItem("Copy") { Command = vm.CopyCommand });
                edit.Menu.Items.Add(new NativeMenuItem("Paste") { Command = vm.PasteCommand });
                edit.Menu.Items.Add(new NativeMenuItem("Delete") { Command = vm.DeleteCommand });
                edit.Menu.Items.Add(new NativeMenuItemSeparator());
                edit.Menu.Items.Add(new NativeMenuItem("Select All") { Command = vm.SelectAllCommand });

                // Tools
                var tools = new NativeMenuItem("Tools") { Menu = new NativeMenu() };
                tools.Menu.Items.Add(new NativeMenuItem("Developer Tools") { Command = vm.ToggleDevToolsCommand });
                tools.Menu.Items.Add(new NativeMenuItemSeparator());
                tools.Menu.Items.Add(new NativeMenuItem("Reload") { Command = vm.ReloadConfigurationCommand });
                tools.Menu.Items.Add(new NativeMenuItem("Force Reload") { Command = vm.ForceReloadCommand });

                // Window
                var window = new NativeMenuItem("Window") { Menu = new NativeMenu() };
                window.Menu.Items.Add(new NativeMenuItem("Minimize") { Command = vm.MinimizeCommand });
                window.Menu.Items.Add(new NativeMenuItem("Close") { Command = vm.CloseWindowCommand });

                // Help
                var help = new NativeMenuItem("Help") { Menu = new NativeMenu() };
                help.Menu.Items.Add(new NativeMenuItem("User Guide") { Command = vm.OpenUserGuideCommand });
                help.Menu.Items.Add(new NativeMenuItemSeparator());
                help.Menu.Items.Add(new NativeMenuItem("About Godot Generator") { Command = vm.AboutCommand });

                menu.Items.Add(file);
                menu.Items.Add(view);
                menu.Items.Add(edit);
                menu.Items.Add(tools);
                menu.Items.Add(window);
                menu.Items.Add(help);

                try
                {
                    var t = this.GetType();
                    var prop = t.GetProperty("NativeMenu");
                    if (prop is not null && prop.CanWrite)
                    {
                        prop.SetValue(this, menu);
                    }
                    else if (this.PlatformImpl is not null)
                    {
                        var setMethod = this.PlatformImpl.GetType().GetMethod("SetNativeMenu");
                        if (setMethod is not null)
                        {
                            setMethod.Invoke(this.PlatformImpl, new object[] { menu });
                        }
                    }
                }
                catch
                {
                    // best effort
                }
            }
            catch
            {
                // best effort: some platforms may not support NativeMenu
            }
        }
    }
}