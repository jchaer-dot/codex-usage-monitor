using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexUsageMonitor;

public partial class MainWindow
{
    private readonly LocalUsage _localUsage = new();
    private bool _readingLocal;
    private DateTime _lastLocalRefresh = DateTime.MinValue;
    private async Task RefreshLocalUsageAsync()
    {
        if (_readingLocal || DateTime.UtcNow - _lastLocalRefresh < TimeSpan.FromMinutes(5)) return;
        _readingLocal = true;
        try
        {
            await Task.Run(() => _localUsage.Refresh());
            _lastLocalRefresh = DateTime.UtcNow;
            RenderLocalRanking();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            ModelListPanel.Children.Clear();
            ModelListPanel.Children.Add(CreateInfoText("Historial local no disponible"));
        }
        finally { _readingLocal = false; }
    }
    private void RenderLocalRanking()
    {
        ModelListPanel.Children.Clear();
        if (ActualWidth >= 440 && ActualHeight >= 560)
        {
            ModelListPanel.Children.Add(new TextBlock {Text="HISTORIAL LOCAL · TOKENS · " + _chartRange + "  ↗",FontSize=11,Foreground=Brush("#AEAAF7"),Margin=new Thickness(0,0,0,8)});
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i=0;i<2;i++)
            {
                var panel = new StackPanel {Margin=new Thickness(0,0,12,0)};
                panel.Children.Add(new TextBlock {Text=i==0?"Modelos":"Proyectos",FontSize=12,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,5)});
                foreach(var entry in _localUsage.Rank(_chartRange,i==0).Take(4))
                {
                    var row = new Grid {Margin=new Thickness(0,0,0,4)};
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
                    row.Children.Add(new TextBlock {Text=System.IO.Path.GetFileName(entry.Name),ToolTip=entry.Name,FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis});
                    var value = new TextBlock {Text=FormatNumber(entry.Tokens),FontSize=11,Foreground=Brush("#CBC8FF"),Margin=new Thickness(8,0,0,0)};
                    Grid.SetColumn(value,1);row.Children.Add(value);panel.Children.Add(row);
                }
                Grid.SetColumn(panel,i);grid.Children.Add(panel);
            }
            ModelListPanel.Children.Add(grid);
            return;
        }
        var model = _localUsage.Rank(_chartRange, true).FirstOrDefault();
        var project = _localUsage.Rank(_chartRange, false).FirstOrDefault();
        ModelListPanel.Children.Add(new TextBlock { Text = "TOP LOCAL · TOKENS  ↗", FontSize=9, Foreground=Brush("#AEAAF7") });
        foreach (var item in new[] { model, project })
        {
            string name = item == null ? "Sin registros en este período" : System.IO.Path.GetFileName(item.Name);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            row.Children.Add(new TextBlock { Text=name, FontSize=10, TextTrimming=TextTrimming.CharacterEllipsis, ToolTip=item?.Name });
            var count = new TextBlock { Text=item == null ? "—" : FormatNumber(item.Tokens), FontSize=10, Margin=new Thickness(5,0,0,0), Foreground=Brush("#CBC8FF") };
            Grid.SetColumn(count,1); row.Children.Add(count); ModelListPanel.Children.Add(row);
        }
    }
    private void OpenLocalDetails(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var root = new Grid { Margin=new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        var note = new TextBlock {
            Text=$"Historial local · {_chartRange} · {_localUsage.Files} archivos · {_localUsage.Unreadable} inaccesibles\nTokens registrados por turno; incluyen caché. No son créditos ni porcentaje del cupo.\nCobertura parcial: solo registros disponibles en este equipo; modelos no identificados aparecen aparte.",
            Foreground=Brush("#B5BBC5"), FontSize=12, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,15)
        };
        root.Children.Add(note);
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition());
        for (int i=0;i<2;i++)
        {
            var panel = new StackPanel { Margin=new Thickness(0,0,12,0) };
            panel.Children.Add(new TextBlock {Text=i==0?"Modelos":"Proyectos (carpeta de trabajo)",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
            foreach(var item in _localUsage.Rank(_chartRange, i==0))
            {
                panel.Children.Add(new TextBlock {Text=$"{System.IO.Path.GetFileName(item.Name)} · {FormatNumber(item.Tokens)} tokens",
                    ToolTip=item.Name,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8),FontSize=12});
            }
            var scroll = new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            Grid.SetColumn(scroll,i); columns.Children.Add(scroll);
        }
        Grid.SetRow(columns,1); root.Children.Add(columns);
        new Window {Title="Consumo local por modelo y proyecto",Width=650,Height=450,MinWidth=450,MinHeight=300,
            Background=Brush("#181A22"),Foreground=Brush("#EBEDF6"),Content=root,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner}.Show();
        e.Handled=true;
    }
}
