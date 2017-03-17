using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace IndustryCanadaImport
{
  /// <summary>
  /// Interaction logic for ReportWindow.xaml
  /// </summary>
  public partial class ReportWindow : Window
  {
    private static ReportWindow mInstance = new ReportWindow();
    public static ReportWindow Instance { get { return mInstance; } }
    private ReportWindow()
    {
      InitializeComponent();
      ReportString.TextAlignment = TextAlignment.Left;
    }

    public void addReport(List<ConsoleMessage> wList)
    {
      ReportString.Inlines.Clear();
      foreach (ConsoleMessage wMessage in wList)
      {
        Inline wInline = new Run(wMessage.ToString() + Environment.NewLine);
        if (wMessage.MessageType == ConsoleMessage.MessageTypeEnum.eInfo)
        {
          wInline.Foreground = Brushes.DimGray;
        }
        else if (wMessage.MessageType == ConsoleMessage.MessageTypeEnum.eWarning)
        {
          wInline.Foreground = Brushes.DarkOrange;
        }
        else if (wMessage.MessageType == ConsoleMessage.MessageTypeEnum.eError)
        {
          wInline.Foreground = Brushes.Red;
        }
        ReportString.Inlines.Add(wInline);
      }
    }

    private void ReportWindow_OnClosing(object sender, CancelEventArgs e)
    {
      e.Cancel = true;
      Hide();
    }
  }
}
