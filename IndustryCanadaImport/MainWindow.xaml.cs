using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Application = System.Windows.Application;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace IndustryCanadaImport
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    private DispatcherTimer mDispatcherTimer = new DispatcherTimer();
    private RunWindow mRunWindow = new RunWindow();

    public MainWindow()
    {
      InitializeComponent();
      DataContext = Settings.Instance;
      
      mDispatcherTimer.Interval = TimeSpan.FromSeconds(30.0);
      mDispatcherTimer.IsEnabled = true;
      mDispatcherTimer.Tick += DispatcherTimer_Tick;

      MinimizeToTray.Enable(this);
    }

    private void MainWindow_OnClosing(object sender, CancelEventArgs e)
    {
      Application.Current.Shutdown();
    }

    private void MainWindow_OnStateChanged(object sender, EventArgs e)
    {
     
    }

    private void DispatcherTimer_Tick(object sender, EventArgs e)
    {
      //This tick happens every 30 seconds 
      if (Settings.Instance.AutoRunIsOn && Settings.Instance.AutoTimeSelected!=null)
      {
        if (DateTime.Now.Hour==Settings.Instance.AutoTimeSelected.hour && DateTime.Now.Minute==Settings.Instance.AutoTimeSelected.min)
        {
          //run auto import
          mRunWindow.RunImport();
        }
      }
    }

    private void ShowReport_Click(object sender, RoutedEventArgs e)
    {
      ReportWindow.Instance.Show();
    }

    private void CharLookUp_Click(object sender, RoutedEventArgs e)
    {
      if (mRunWindow.isBusy()==false)
      {
        CharConverter.Instance.deleteCharLookUp();
      }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
      if (mRunWindow.isBusy() == false)
      {
        Settings.Instance.saveSettings();
      }
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
      mRunWindow.Show();
      mRunWindow.RunImport();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
      using (System.Windows.Forms.FileDialog wFileDialog = new System.Windows.Forms.OpenFileDialog())
      {
        wFileDialog.Filter = "exe files (*.exe)|*.exe";
        wFileDialog.Title = "Select Industry Canada Extract Downloader";
        if (wFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
          Settings.Instance.ExtracterPath = wFileDialog.FileName;
        }
      }
    }
  }





  public class AutoTime
  {
    public int hour { get; set; }
    public int min { get; set; }
    public override string ToString()
    {
      return hour.ToString("00") + " : " + min.ToString("00");
    }

    public static List<AutoTime> getAutoTimes()
    {
      List<AutoTime> wList = new List<AutoTime>();
      for (int i = 0; i < 24; i++)
      {
        wList.Add(new AutoTime() { hour = i, min = 0 });
        wList.Add(new AutoTime() { hour = i, min = 30 });
      }
      return wList;
    }
  }






  public class CharConverter
  {
    public class Conversion
    {
      public int SymbolNumber { get; set; }
      public string StringSymbol { get; set; }
    }

    private readonly string cCharLookUpPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) +
                                            @"\IC_Import\CharLookUp.dat";
    private static CharConverter mInstance=new CharConverter();
    private List<Conversion> mConversionList = new List<Conversion>();
    
    public static CharConverter Instance { get { return mInstance; } }

    private CharConverter()
    {
      //Retrieve char lookup
      if (System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(cCharLookUpPath)) == false)
      {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cCharLookUpPath));
      }
      if (System.IO.File.Exists(cCharLookUpPath) == false)
      {
        System.IO.File.Create(cCharLookUpPath);
      }
      else
      {
        try
        {
          foreach (string line in System.IO.File.ReadAllLines(cCharLookUpPath))
          {
            string[] wS = line.Split('\t');
            mConversionList.Add(new Conversion() { SymbolNumber = int.Parse(wS[0]), StringSymbol = wS[1] });
          }
        }
        catch (Exception ex)
        {
          MessageBox.Show("Failed to load char look up" + Environment.NewLine + ex.Message + Environment.NewLine + "The file will be erased", "Error !", MessageBoxButton.OK, MessageBoxImage.Error);
          System.IO.File.Delete(cCharLookUpPath);
        }
      }
    }
    
    public void addConversion(int iSymbolNumber, string iStringSymbol)
    {
      mConversionList.Add(new Conversion() {SymbolNumber = iSymbolNumber,StringSymbol = iStringSymbol});
      System.IO.File.AppendAllLines(cCharLookUpPath,new string[] { iSymbolNumber + "\t" + iStringSymbol});
    }
    public bool getConvertedChar(int iSymbolNumber, out string iConvertedSymbol)
    {
      Conversion wConversion = mConversionList.Find(x => x.SymbolNumber == iSymbolNumber);
      iConvertedSymbol = (wConversion == null) ? "?" : wConversion.StringSymbol;
      return (wConversion != null);
    }

    public void deleteCharLookUp()
    {
      if (MessageBox.Show("Delete Char Look Up File ?","Question ?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
      {
        System.IO.File.Delete(cCharLookUpPath);
        mConversionList.Clear();
      }
    }
  }
}
