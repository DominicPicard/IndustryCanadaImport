using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
  /// Interaction logic for CharConverterWindow.xaml
  /// </summary>
  public partial class CharConverterWindow : Window, INotifyPropertyChanged
  {
    #region Properties
    private ObservableCollection<string> mAvailableSymbols = new ObservableCollection<string>();
    public ObservableCollection<string> AvailableSymbols
    {
      get { return mAvailableSymbols; }
      set
      {
        mAvailableSymbols = value;
        onPropertyChanged("AvailableSymbols");
      }
    }
    private string mSelectedSymbol;
    public string SelectedSymbol
    {
      get { return mSelectedSymbol; }
      set
      {
        mSelectedSymbol = value;
        onPropertyChanged("SelectedSymbol");
      }
    }
    #endregion

    #region INotifyPropertyChanged Members

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void onPropertyChanged(string iPropertyName)
    {
      if (this.PropertyChanged != null)
      {
        this.PropertyChanged(this, new PropertyChangedEventArgs(iPropertyName));
      }
    }

    #endregion


    public string ConvertedSymbol { get; set; }
    private int mSymbolNumber;
    public CharConverterWindow(string iString, int iSymbolNumber)
    {
      InitializeComponent();
      DataContext = this;

      //default value
      ConvertedSymbol = new string((char) (iSymbolNumber), 1);
      mSymbolNumber = iSymbolNumber;

      //add A to Z
      for (int i = 65; i <= 90 ; i++)
      {
        AvailableSymbols.Add(new string((char)(i),1));
      }

      //add other symbols
      for (int i = 33; i <= 64; i++)
      {
        AvailableSymbols.Add(new string((char)(i), 1));
      }

      //add space
      AvailableSymbols.Add("space");

      SelectedSymbol = AvailableSymbols[0];

      foreach (char c in iString)
      {
        if (c==iSymbolNumber)
        {
          DisplayString.Inlines.Add(new Bold(new Run(new string(c,1)))
          {
            Foreground = Brushes.Crimson
          });
        }
        else
        {
          DisplayString.Inlines.Add(new Run(new string(c, 1)));
        }
      }
      
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
      ConvertedSymbol = SelectedSymbol=="space"?" ":SelectedSymbol;
      CharConverter.Instance.addConversion(mSymbolNumber,ConvertedSymbol);
      Close();
    }
  }
}
