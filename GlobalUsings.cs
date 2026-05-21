// Resolve WPF vs WinForms type ambiguities caused by UseWindowsForms implicit usings.
global using Application    = System.Windows.Application;
global using Color          = System.Windows.Media.Color;
global using ColorConverter = System.Windows.Media.ColorConverter;
global using Cursors        = System.Windows.Input.Cursors;
global using Point          = System.Windows.Point;
global using Pen            = System.Windows.Media.Pen;
global using Brush          = System.Windows.Media.Brush;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using DragEventArgs  = System.Windows.DragEventArgs;
global using DragDropEffects = System.Windows.DragDropEffects;
global using Brushes             = System.Windows.Media.Brushes;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using TextBox             = System.Windows.Controls.TextBox;
global using Button              = System.Windows.Controls.Button;
