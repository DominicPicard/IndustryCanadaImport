using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Data.OracleClient;
using System.Diagnostics;
using System.IO;
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
  /// Interaction logic for RunWindow.xaml
  /// </summary>
  public partial class RunWindow : Window
  {
    private BackgroundWorker mBackgroundWorker = new BackgroundWorker();
    private RunWindowViewModel mRunWindowViewModel = new RunWindowViewModel();

    private string mControlFilePath;

    private OleDbConnection mDbfConnection = new OleDbConnection();
    private OracleConnection mOracleConnection = new OracleConnection();

    private int mNumberOfRecordsDbf;
    private readonly string cControlFileDirectory = "ControlFiles";

    public RunWindow()
    {
      InitializeComponent();
      DataContext = mRunWindowViewModel;
      
      mBackgroundWorker.WorkerReportsProgress = true;
      //mBackgroundWorker.WorkerSupportsCancellation = true;
      mBackgroundWorker.DoWork += executeUpdate;
      mBackgroundWorker.ProgressChanged += reportUpdateProgress;
      mBackgroundWorker.RunWorkerCompleted += importCompleted;
    }
    
    public void RunImport()
    {
      if (mBackgroundWorker.IsBusy == false)
      {
        mRunWindowViewModel.reset();

        mBackgroundWorker.RunWorkerAsync();
      }
    }

    public bool isBusy()
    {
      return mBackgroundWorker.IsBusy;
    }

    private void importCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
      mDbfConnection.Close();

      //Write Report To File
      List<string> wList = new List<string>();
      mRunWindowViewModel.ConsoleMessages.ToList().ForEach(x => wList.Add(x.ToString()));
      if (System.IO.Directory.Exists("Reports")==false)
      {
        System.IO.Directory.CreateDirectory("Reports");
      }
      System.IO.File.WriteAllLines(@".\Reports\" + DateTime.Now.ToString("yyyy-mm-dd hh.mm.ss") + ".txt",wList);
      Settings.Instance.LastImport = DateTime.Now.ToString();

      ReportWindow.Instance.addReport(mRunWindowViewModel.ConsoleMessages.ToList());
    }

    private void reportUpdateProgress(object sender, ProgressChangedEventArgs e)
    {
      if (e.UserState is ConsoleMessage)
      {
        mRunWindowViewModel.ConsoleMessages.Add(e.UserState as ConsoleMessage);
      }
    }

    private void executeUpdate(object sender, DoWorkEventArgs e)
    {
      //TODO download from http://spectrum.ic.gc.ca/engineering/engdoc/baserad.zip

      //Execute ExtractDownloader.exe
      if (Settings.Instance.SkipIndustryCanadaDownloader == false)
      {
        mBackgroundWorker.ReportProgress(0,
          new ConsoleMessage("Starting ExtractDownloader.exe...", ConsoleMessage.MessageTypeEnum.eInfo));
        mRunWindowViewModel.IcExtractInProgress = true;

        using (Process p = new Process())
        { 
          p.StartInfo.FileName = Settings.Instance.ExtracterPath;
          p.StartInfo.Arguments = "--autostart";
          p.Start();
          p.WaitForExit();
        }
        mRunWindowViewModel.IcExtractInProgress = false;
        mBackgroundWorker.ReportProgress(0,
          new ConsoleMessage("ExtractDownloader.exe has finished", ConsoleMessage.MessageTypeEnum.eInfo));
      }

      //Create Control File Directory
      if (System.IO.Directory.Exists(cControlFileDirectory) ==false)
      {
        System.IO.Directory.CreateDirectory(cControlFileDirectory);
      }

      //Upload tables from DBF folder to Oracle Server
      mRunWindowViewModel.TotalProgressBarMax = 15;
      UploadTable("FMSTATIO", "FMSTATIO_BUFF");
      UploadTable("AMSTATIO", "AMSTATIO_BUFF");
      UploadTable("TVSTATIO", "TVSTATIO_BUFF");
      UploadTable("CITY", "CITY_BUFF");
      UploadTable("AUGMENT", "AUGMENT_BUFF");
      UploadTable("PARAMS", "PARAMS_BUFF");
      UploadTable("STATIONS", "STATIONS_BUFF");
      UploadTable("LIMCODE", "LIMCODE");
      UploadTable("APATSTAT", "APATSTAT_BUFF");
      UploadTable("APATDAT", "APATDAT_BUFF");
      UploadTable("APATDESC", "APATDESC_BUFF");
      UploadTable("CONTOURS", "CONTOURS_BUFF");
      UploadTable("COMMENTS", "COMMENTS_BUFF");
      UploadTable("PROVINCE", "PROVINCE");
      //       RefreshLimitations("FMLimits.txt", "FMLimits");
      //       RefreshLimitations("TVLimits.txt", "TVLimits");
      OracleExecute("PURGE RECYCLEBIN");
      mRunWindowViewModel.StatusText = "Upload is completed !";
    }

    private void RefreshLimitations(string iLimitationFile, string iOracleTable)
    {
      try
      {
        mRunWindowViewModel.TableProgressBarValue = 0;
        List<string> wList = System.IO.File.ReadAllLines(Settings.Instance.DbfFolder + @"\" + iLimitationFile).ToList();
        mRunWindowViewModel.TableProgressBarMax = wList.Count;
        foreach (string iLine in wList)
        {
          string wCallSign = iLine.Substring(0, iLine.IndexOf(" "));
          string wText = iLine.Substring(iLine.IndexOf(" "));

          //Add limitation to table
          //TODO

          mRunWindowViewModel.TableProgressBarValue++;
        }
      }
      catch (Exception exception)
      {
        mBackgroundWorker.ReportProgress(0,
         new ConsoleMessage("Error when parsing  " + iLimitationFile, ConsoleMessage.MessageTypeEnum.eError));
        mBackgroundWorker.ReportProgress(0,
         new ConsoleMessage(exception.Message, ConsoleMessage.MessageTypeEnum.eError));
      }
    }

    private void UploadTable(string iDbfName, string iOracleName)
    {
      mRunWindowViewModel.StatusText = "Writing " + iDbfName + " Control File ...";
      mControlFilePath = @".\" + cControlFileDirectory + @"\" + iDbfName + ".ctl";
      //
      //Write Control File from the DBF table, the control file is used to 
      //upload the table to Oracle using SqlPlus
      bool wWritingControlFileSucceeded = WriteControlFile(iDbfName, iOracleName);
      
      if (wWritingControlFileSucceeded)
      {
        #region UploadToOracle

        try
        {
          if (iDbfName == "PROVINCE" || iDbfName == "LIMCODE" || iDbfName == "COMMENTS")
          {
            if (OracleExecute("TRUNCATE TABLE " + iOracleName) == false)
            {
              //Recreate these tables
              if (iDbfName == "LIMCODE")
                OracleExecute(
                  "CREATE TABLE LIMCODE (STAT_TYPE VARCHAR2(1 CHAR), CNTRY_CODE VARCHAR2(2 CHAR),LANGUAGE VARCHAR2(1 CHAR), OLD_CODE VARCHAR2(7 CHAR), LMT_CODE NUMBER)");
              else if (iDbfName == "PROVINCE")
              {
                OracleExecute("CREATE TABLE PROVINCE (PROVINCE VARCHAR2(2 CHAR)," +
                              "COUNTRY  VARCHAR2(2 CHAR)," +
                              "LOW_LAT  NUMBER," +
                              "HIGH_LAT NUMBER," +
                              "LOW_LONG  NUMBER," +
                              "HIGH_LONG NUMBER," +
                              "CREAT_DT DATE," +
                              "MOD_DT DATE," +
                              "ENGDESC VARCHAR2(25 CHAR)," +
                              "FRNDESC VARCHAR2(25 CHAR))");
              }
              else if (iDbfName == "COMMENTS")
              {
                OracleExecute("CREATE TABLE COMMENTS_BUFF (CALLS_BANR  VARCHAR2(14 CHAR)," +
                              "NAME VARCHAR2(40 CHAR))");
              }
            }
          }
          else
          {
            //Erase table content and recreate it if the table does not exist
            if (OracleExecute("TRUNCATE TABLE " + iOracleName) == false)
            {
              #region Recreate table
              setupDbfConnection();
              //
              //Defines columns that would be uploaded
              List<string> wColumnNames = new List<string>();
              using (OleDbCommand wCommand = new OleDbCommand("SELECT * FROM " + iDbfName, mDbfConnection))
              {
                using (OleDbDataReader wReader = wCommand.ExecuteReader())
                {
                  //Depending on the table, we limit the number of columns
                  int wColumnCount;
                  if (iDbfName == "COMPANY")
                  {
                    wColumnCount = 6;
                  }
                  else if (iDbfName == "COMMENTS")
                  {
                    wColumnCount = 2;
                  }
                  else
                  {
                    wColumnCount = wReader.FieldCount;
                  }

                  //'NUMBER' is not a valid column name in Oracle, so we need to replace it
                  for (int i = 0; i < wColumnCount; i++)
                  {
                    if (wReader.GetName(i).ToUpper() == "NUMBER")
                    {
                      wColumnNames.Add("SEG_NUMBER");
                    }
                    else
                    {
                      wColumnNames.Add(wReader.GetName(i).ToUpper());
                    }
                  }
                }
              }
              OracleExecute("CREATE TABLE " + iOracleName + " AS SELECT " + string.Join(",", wColumnNames) + " FROM " + iDbfName);
              #endregion
            }
          }

          //
          //Proceed with upload to Oracle using SqlLoader
          mBackgroundWorker.ReportProgress(0, new ConsoleMessage("Uploading " + iOracleName + " to Oracle (run SQL Loader) ...", ConsoleMessage.MessageTypeEnum.eInfo));
          mRunWindowViewModel.StatusText = "Uploading " + iDbfName + " to Oracle (run SQL Loader) ...";
          using (Process p = new Process())
          {
            p.StartInfo.FileName = "sqlldr.exe";
            p.StartInfo.Arguments = Settings.Instance.OracleUsername + "/" + Settings.Instance.OraclePassword + "@" +
                                    Settings.Instance.TNS + " " + System.IO.Path.GetFileName(mControlFilePath);
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.WorkingDirectory = cControlFileDirectory;
            p.Start();
            p.WaitForExit();
          }
          
          //
          //Find the number of records in the table on Oracle
          using (OracleCommand wCommand = new OracleCommand("SELECT COUNT(*) FROM " + iOracleName, mOracleConnection))
          {
            using (OracleDataReader wReader = wCommand.ExecuteReader())
            {
              wReader.Read();
              int wOracleNumberOfRecords = int.Parse(wReader.GetValue(0).ToString());
              mBackgroundWorker.ReportProgress(0, new ConsoleMessage("Finished uploading " + iOracleName + " to Oracle (" + wOracleNumberOfRecords + " records)", ConsoleMessage.MessageTypeEnum.eInfo));
              if (wOracleNumberOfRecords != mNumberOfRecordsDbf)
              {
                mBackgroundWorker.ReportProgress(0, new ConsoleMessage("Error in upload of " +iOracleName + ", total records number mismatch : Dbf=" + mNumberOfRecordsDbf + " Oracle=" + wOracleNumberOfRecords, ConsoleMessage.MessageTypeEnum.eError));
              }
            }
          }

        }
        catch (Exception exception)
        {
          mBackgroundWorker.ReportProgress(0,
            new ConsoleMessage("Failed to upload table " + iDbfName + " to Oracle",
              ConsoleMessage.MessageTypeEnum.eError));
          mBackgroundWorker.ReportProgress(0,
            new ConsoleMessage(exception.Message, ConsoleMessage.MessageTypeEnum.eError));
        }

        #endregion

        mRunWindowViewModel.TotalProgressBarValue++;
      }
      else
      {
        mBackgroundWorker.ReportProgress(0,
          new ConsoleMessage("Failed to write Control File for " + iDbfName, ConsoleMessage.MessageTypeEnum.eError));
      }
    }

    private bool OracleExecute(string iCommand)
    {
      try
      {
        mBackgroundWorker.ReportProgress(0,
         new ConsoleMessage("Executing '" + iCommand + "' on Oracle",ConsoleMessage.MessageTypeEnum.eInfo));

        setupOracleConnection();

        using (OracleCommand wCommand = new OracleCommand(iCommand, mOracleConnection))
        {
          wCommand.ExecuteNonQuery();
        }
        return true;
      }
      catch (Exception exception)
      {
        mBackgroundWorker.ReportProgress(0,
          new ConsoleMessage("Error in execution of command '" + iCommand + "' on Oracle",
            ConsoleMessage.MessageTypeEnum.eError));
        mBackgroundWorker.ReportProgress(0,
          new ConsoleMessage(exception.Message, ConsoleMessage.MessageTypeEnum.eError));
        return false;
      }
    }

    private bool WriteControlFile(string iDbfName, string iOracleName)
    {
      try
      {
        mRunWindowViewModel.TableProgressBarValue = 0;

        setupDbfConnection();
        
        //Find total number of item in table and set table progress bar maximum
        using (OleDbCommand wCommand = new OleDbCommand("SELECT COUNT(*) FROM " + iDbfName, mDbfConnection))
        {
          using (OleDbDataReader wReader = wCommand.ExecuteReader())
          {
            wReader.Read();
            mRunWindowViewModel.TableProgressBarMax = double.Parse(wReader.GetValue(0).ToString());
            mNumberOfRecordsDbf = (int)(mRunWindowViewModel.TableProgressBarMax);
          }
        }

        //Show information on Console
        mBackgroundWorker.ReportProgress(0,
          new ConsoleMessage("Writing Control File for " + iDbfName + " (" + mNumberOfRecordsDbf + " records) ...", ConsoleMessage.MessageTypeEnum.eInfo));

        //loop through table records
        using (OleDbCommand wCommand = new OleDbCommand("SELECT * FROM " + iDbfName, mDbfConnection))
        {
          using (OleDbDataReader wReader = wCommand.ExecuteReader())
          {
            int wColumnCount = wReader.FieldCount;

            //special case for COMPANY and COMMENTS table
            if (iDbfName == "COMPANY")
            {
              wColumnCount = 6;
            }
            else if (iDbfName == "COMMENTS")
            {
              wColumnCount = 2;
            }

            //convert column types to be compatible with Oracle
            List<string> wColumnNames = new List<string>();
            for (int i = 0; i < wColumnCount; i++)
            {
              switch (wReader.GetFieldType(i).Name.ToUpper())
              {
                case "DATETIME":
                  wColumnNames.Add(wReader.GetName(i).ToUpper() + " \"to_date(:" + wReader.GetName(i) + ",'YYYY/MM/DD')\"");
                  break;
                  
                default:
                  if (wReader.GetName(i).ToUpper() == "NUMBER")
                  {
                    wColumnNames.Add("SEG_NUMBER");
                  }
                  else
                  {
                    wColumnNames.Add(wReader.GetName(i).ToUpper());
                  }
                  break;
              }
            }

            //
            //Write Control File
            using (TextWriter wTextWriter = new StreamWriter(mControlFilePath, false))
            {
              //Write Header
              wTextWriter.WriteLine("load data");
              wTextWriter.WriteLine("infile *");
              wTextWriter.WriteLine("into table " + iOracleName);
              wTextWriter.WriteLine("fields terminated by \"|\" optionally enclosed by '\"'");
              wTextWriter.WriteLine("TRAILING NULLCOLS");
              wTextWriter.WriteLine("(" + string.Join(",", wColumnNames) + ")");
              wTextWriter.WriteLine("begindata");

              //Loop through records and write them to Control File
              #region WriteControlFileContent
              int wRecordCounter = 1;
              while (wReader.Read())
              {
                try
                {
                  wRecordCounter++;
                  List<string> wFieldValues = new List<string>();
                  for (int i = 0; i < wColumnCount; i++)
                  {
                    //Some column types require a special conversion
                    switch (wReader.GetFieldType(i).Name)
                    {
                      case "DateTime":
                        string wDateValue = wReader.GetValue(i).ToString();
                        if (wDateValue != "")
                        {
                          DateTime wDateTime = DateTime.Parse(wDateValue);
                          wFieldValues.Add(string.Join("/",
                            new string[]
                              {wDateTime.Year.ToString(), wDateTime.Month.ToString("00"), wDateTime.Day.ToString("00")}));
                        }
                        else
                        {
                          //add an empty value
                          wFieldValues.Add("");
                        }
                        break;

                      case "Decimal":
                        //TODO deal with culture
                        wFieldValues.Add(wReader.GetValue(i).ToString().Replace('.',','));
                        break;

                      case "String":
                        #region StringType
                        //Make the string uppercase and remove unwanted accents or symbols
                        string wString = wReader.GetValue(i).ToString().ToUpper().Replace(Environment.NewLine, "");

                        //loop through string char and remove symbols
                        string wFilteredString = "";
                        List<string> wUnknownChars = new List<string>();
                        foreach (char c in wString)
                        {
                          if (c > 0x005F)
                          {
                            string wConvertedChar = "";
                            if (CharConverter.Instance.getConvertedChar(c, out wConvertedChar) == false)
                            {
                              //Symbol not found in char lookup
                              if (Settings.Instance.AutoRunIsOn)
                              {
                                //log error, the symbol is unknown
                                wUnknownChars.Add(c.ToString());
                              }
                              else
                              {
                                //ask user on UI thread
                                Dispatcher.Invoke(new Action(() =>
                                {
                                  CharConverterWindow wCharConverterWindow = new CharConverterWindow(wString, c);
                                  wCharConverterWindow.Owner = this;
                                  wCharConverterWindow.ShowDialog();
                                  wFilteredString += wCharConverterWindow.ConvertedSymbol;
                                }));
                              }
                            }
                            else
                            {
                              wFilteredString += wConvertedChar;
                            }
                          }
                          else
                          {
                            wFilteredString += c;
                          }
                        }

                        if (wUnknownChars.Count != 0)
                        {
                          //log an error
                          mBackgroundWorker.ReportProgress(0,
                            new ConsoleMessage(
                              "Unknown character(s) found at record #" + wRecordCounter + " in string '" + wString +
                              "' : " + string.Join(",", wUnknownChars), ConsoleMessage.MessageTypeEnum.eError));
                        }
                        wFieldValues.Add(wFilteredString);
                        #endregion
                        break;

                      default:
                        wFieldValues.Add(wReader.GetValue(i).ToString());
                        break;
                    }
                  }

                  wTextWriter.WriteLine(string.Join("|", wFieldValues));
                  mRunWindowViewModel.TableProgressBarValue = wRecordCounter;
                }
                catch (Exception exception)
                {
                  mBackgroundWorker.ReportProgress(0, new ConsoleMessage("Error when writing record #" + wRecordCounter, ConsoleMessage.MessageTypeEnum.eError));
                  mBackgroundWorker.ReportProgress(0, new ConsoleMessage(exception.Message, ConsoleMessage.MessageTypeEnum.eError));
                }
              }
              #endregion
            }
          }
        }
        mBackgroundWorker.ReportProgress(0, new ConsoleMessage("Finished writing Control File for " + iDbfName, ConsoleMessage.MessageTypeEnum.eInfo));
        return true;
      }
      catch (Exception ex)
      {
        mBackgroundWorker.ReportProgress(0, new ConsoleMessage("Error when writing Control File for " + iDbfName, ConsoleMessage.MessageTypeEnum.eError));
        mBackgroundWorker.ReportProgress(0, new ConsoleMessage(ex.Message, ConsoleMessage.MessageTypeEnum.eError));
        return false;
      }
    }

    private void setupDbfConnection()
    {
      if (mDbfConnection.ConnectionString == "" || mDbfConnection.State==ConnectionState.Closed)
      {
        mDbfConnection.ConnectionString = @"Provider=vfpoledb.1;Data Source=" + Settings.Instance.DbfFolder +
                                        ";Collating Sequence=general";
        mDbfConnection.Open();
      }
    }

    private void setupOracleConnection()
    {
      if (mOracleConnection.ConnectionString == "")
      {
        mOracleConnection.ConnectionString = "Data Source=" + Settings.Instance.TNS + ";User Id=" +
                                             Settings.Instance.OracleUsername + ";Password=" + Settings.Instance.OraclePassword + ";Integrated Security=no;";
        mOracleConnection.Open();
      }
    }
    
    private void RunWindow_OnClosing(object sender, CancelEventArgs e)
    {
      e.Cancel = true;
      Hide();
    }

    private void CopyConsoleToClipboard(object sender, RoutedEventArgs e)
    {
      List<string> wList = new List<string>();
      mRunWindowViewModel.ConsoleMessages.ToList().ForEach(x => wList.Add(x.ToString()));
      System.Windows.Forms.Clipboard.SetText(string.Join(Environment.NewLine,wList));
    }
  }

  [PropertyChanged.ImplementPropertyChanged]
  public class RunWindowViewModel
  {
    #region Properties
    public bool IcExtractInProgress { get; set; }
    public string StatusText { get; set; }
    public ObservableCollection<ConsoleMessage> ConsoleMessages { get; set; }
    public double TableProgressBarMax { get; set; }
    public double TableProgressBarValue { get; set; }
    public double TotalProgressBarMax { get; set; }
    public double TotalProgressBarValue { get; set; }
    #endregion
    
    public RunWindowViewModel()
    {
      ConsoleMessages = new ObservableCollection<ConsoleMessage>();
    }

    public void reset()
    {
      ConsoleMessages.Clear();
      TotalProgressBarValue = 0;
      TableProgressBarValue = 0;
      StatusText = "";
    }
  }

  [PropertyChanged.ImplementPropertyChanged]
  public class ConsoleMessage
  {
    public enum MessageTypeEnum
    {
      eInfo,
      eWarning,
      eError
    };

    public MessageTypeEnum MessageType { get; set; }
    public string Message { get; set; }
    public DateTime TimeStamp { get; set; }

    public ConsoleMessage(string iMsg, MessageTypeEnum iMessageType)
    {
       TimeStamp = DateTime.Now;
       Message = iMsg;
       MessageType = iMessageType;
    }

    public override string ToString()
    {
      string wString = "[" + TimeStamp + "]" + "  ";
      switch (MessageType)
      {
          case MessageTypeEnum.eInfo:
          wString += "< Info >    " + Message;
          break;

          case MessageTypeEnum.eWarning:
          wString += "< Warning > " + Message;
          break;

          case MessageTypeEnum.eError:
          wString += "< Error >   " + Message;
          break;

        default:
          wString += "";
          break;

      }
      return wString;
    }
  }

  

}
