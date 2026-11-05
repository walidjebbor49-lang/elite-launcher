using System.Windows;
namespace ELITE.Launcher;
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
    private void ShowStage(string section) => MessageBox.Show($"La sección {section} se activará en la siguiente etapa de desarrollo.", "ELITE Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
    private void Versions_Click(object sender, RoutedEventArgs e) => ShowStage("Versiones");
    private void Optimization_Click(object sender, RoutedEventArgs e) => ShowStage("Optimización");
    private void Mods_Click(object sender, RoutedEventArgs e) => ShowStage("Mods");
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowStage("Ajustes");
}
