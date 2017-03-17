using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace IndustryCanadaImport
{
  [PropertyChanged.ImplementPropertyChanged] //this will implement INotifyPropertyChanged for all properties
  class Settings
  {
    #region Properties
    public string ExtracterPath { get; set; }
    public string DSN { get; set; }
    public string TNS { get; set; }
    public string OracleUsername { get; set; }
    public string OraclePassword { get; set; }
    public ObservableCollection<AutoTime> AutoTimeElements { get; set; }
    public AutoTime AutoTimeSelected { get; set; }
    public bool SkipIndustryCanadaDownloader { get; set; }
    public bool AutoRunIsOn { get; set; }
    public string DbfFolder { get; set; }
    public string LastImport { get; set; }
    #endregion

    private readonly string cSettingsFile = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) +
                                            @"\IC_Import\IC_import.ini";
    private static Settings mInstance = new Settings();
    public static Settings Instance { get {return mInstance;} }

    private Settings()
    {
      AutoTimeElements = new ObservableCollection<AutoTime>(AutoTime.getAutoTimes());

      fetchSavedSettings();

      //get DBF folder
      //Find folder where Industry Canada downloader put the .dbf
      RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\BCApps\\Misc");
      DbfFolder = key.GetValue("BDBSDir00").ToString();
    }

    public void saveSettings()
    {
      System.IO.File.WriteAllLines(cSettingsFile, new []
      {
        ExtracterPath,
        DSN,
        TNS,
        OracleUsername,
        OraclePassword,
        AutoTimeSelected.hour.ToString(),
        AutoTimeSelected.min.ToString(),
        AutoRunIsOn.ToString()
      });
      MessageBox.Show("Settings saved !","Info",MessageBoxButton.OK,MessageBoxImage.Information);
    }

    private void fetchSavedSettings()
    {
      if (System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(cSettingsFile))==false)
      {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cSettingsFile));
      }
      if (System.IO.File.Exists(cSettingsFile) == false)
      {
        System.IO.File.Create(cSettingsFile);
      }
      else
      {
        try
        {
          string[] wSettings = System.IO.File.ReadAllLines(cSettingsFile);
          ExtracterPath = wSettings[0];
          DSN = wSettings[1];
          TNS = wSettings[2];
          OracleUsername = wSettings[3];
          OraclePassword = wSettings[4];
          AutoTimeSelected =
            AutoTimeElements.ToList().Find(x => x.hour == int.Parse(wSettings[5]) && x.min == int.Parse(wSettings[6]));
          AutoRunIsOn = bool.Parse(wSettings[7]);
        }
        catch (Exception ex)
        {
          MessageBox.Show("Failed to load settings" + Environment.NewLine + ex.Message + Environment.NewLine + "The settings file will be erased", "Error !", MessageBoxButton.OK, MessageBoxImage.Error);
          System.IO.File.Delete(cSettingsFile);
        }
      }
    }
  }
}
