﻿namespace REvernus.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows;

    using Prism.Commands;
    using Prism.Mvvm;

    using REvernus.Models;
    using REvernus.Utilites;

    public class MarginToolViewModel : BindableBase
    {
        private readonly List<ExportedOrderModel> _orders = new List<ExportedOrderModel>();

        private readonly FileSystemWatcher _watcher = new FileSystemWatcher
        {
            Path = Paths.EveMarketLogsFolderPath,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = true
        };

        public MarginToolViewModel()
        {
            _watcher.Created += WatcherOnChanged;
            _watcher.EnableRaisingEvents = true;

            BuyCopyCommand = new DelegateCommand(BuyPriceClipboardCopy);
            SellCopyCommand = new DelegateCommand(SellPriceClipboardCopy);
            ItemNameCopyCommand = new DelegateCommand(ItemNameClipboardCopy);

            // Define SampleDataTables
            using var tempTable = new DataTable();
            tempTable.Columns.AddRange(new List<DataColumn>
            {
                new DataColumn("Volume", typeof(string)),
                new DataColumn("Cost", typeof(string)),
                new DataColumn("Profit", typeof(string))
            }.ToArray());

            TensDataTable = tempTable.Clone();
            FivesDataTable = tempTable.Clone();

            // ReSharper disable once PossibleNullReferenceException
            JumpsOut = App.Settings.MarginToolSettings.JumpsOut;
        }

        public DelegateCommand BuyCopyCommand { get; set; }

        public DelegateCommand ItemNameCopyCommand { get; set; }

        public DelegateCommand SellCopyCommand { get; set; }

        public double GetBuyPrice(double buyPrice)
        {
            return buyPrice + buyPrice * BuyBroker;
        }

        public double GetMargin(double sellPrice, double buyPrice)
        {
            var realSell = GetSellPrice(sellPrice);
            var realBuy = GetBuyPrice(buyPrice);

            return (realSell - realBuy) / realSell;
        }

        public double GetMarkup(double sellPrice, double buyPrice)
        {
            var realSell = GetSellPrice(sellPrice);
            var realBuy = GetBuyPrice(buyPrice);

            return (realSell - realBuy) / realBuy;
        }

        public double GetProfit(double sellPrice, double buyPrice)
        {
            var costs = GetCosts(sellPrice, buyPrice);
            var revenue = sellPrice;
            return revenue - costs;
        }

        public double GetSellPrice(double sellPrice)
        {
            return sellPrice * (1.0 - SalesTax) - sellPrice * SellBroker;
        }

        private void BuyPriceClipboardCopy()
        {
            try
            {
                if (Application.Current.Dispatcher != null)
                    Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(_buyPrice.ToString("F")));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private double GetCosts(double sellPrice, double buyPrice)
        {
            return sellPrice * SalesTax + sellPrice * SellBroker + buyPrice * BuyBroker + buyPrice;
        }

        // Copy the item name to the clipboard
        private void ItemNameClipboardCopy()
        {
            try
            {
                if (Application.Current.Dispatcher != null)
                    Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(ItemName));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void SellPriceClipboardCopy()
        {
            try
            {
                if (Application.Current.Dispatcher != null)
                    Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(_sellPrice.ToString("F")));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void UpdateMarginInformation(double bestSell, double bestBuy)
        {
            Margin = GetMargin(bestSell, bestBuy).ToString("P");
            Markup = GetMarkup(bestSell, bestBuy).ToString("P");

            var profit = GetProfit(bestSell, bestBuy);
            Profit = profit.ToString("N");

            Revenue = bestSell.ToString("N");
            var costs = GetCosts(bestSell, bestBuy);
            Costs = costs.ToString("N");

            var tempTensDataTable = TensDataTable.Clone();
            var tempFivesDataTable = FivesDataTable.Clone();

            for (var i = 0; i < 7; i++)
            {
                var newTensRow = tempTensDataTable.NewRow();
                var newFivesRow = tempFivesDataTable.NewRow();

                var multiplier = Math.Pow(10, i);

                newTensRow["Volume"] = multiplier.ToString("N");
                newTensRow["Cost"] = (costs * multiplier / 1000000).ToString("N") + "M";
                newTensRow["Profit"] = (profit * multiplier / 1000000).ToString("N") + "M";

                newFivesRow["Volume"] = (multiplier * 5).ToString("N");
                newFivesRow["Cost"] = (costs * multiplier * 5 / 1000000).ToString("N") + "M";
                newFivesRow["Profit"] = (profit * multiplier * 5 / 1000000).ToString("N") + "M";

                tempTensDataTable.Rows.Add(newTensRow);
                tempFivesDataTable.Rows.Add(newFivesRow);
            }

            TensDataTable = tempTensDataTable;
            FivesDataTable = tempFivesDataTable;
        }

        private void WatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            // There appears to be some sort of timing error resulting in NaN in the window under Margin and Markup
            // various other fields do not get populated with the correct data.
            // Sleeping the thread seems to fix the problem

            Thread.Sleep(50);

            try
            {
                // ReSharper disable once UnusedVariable
                var currentChar = App.CharacterManager.SelectedCharacter;
                _orders.Clear();
                using (var file = File.Open(e.FullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    using var reader = new StreamReader(file);
                    try
                    {
                        reader.ReadLine(); // read first line and disregard
                        while (!reader.EndOfStream)
                        {
                            var values = reader.ReadLine()?.Split(',');
                            var order = new ExportedOrderModel();
                            if (values != null)
                            {
                                order.Price = double.Parse(values[0], CultureInfo.InvariantCulture);
                                order.VolumeRemaining = Convert.ToInt32(Math.Floor(Convert.ToDouble(values[1])),
                                    CultureInfo.InvariantCulture);
                                order.TypeId = int.Parse(values[2], CultureInfo.InvariantCulture);
                                order.Range = int.Parse(values[3], CultureInfo.InvariantCulture);
                                order.OrderId = long.Parse(values[4], CultureInfo.InvariantCulture);
                                order.VolumeEntered = int.Parse(values[5], CultureInfo.InvariantCulture);
                                order.MinVolume = int.Parse(values[6], CultureInfo.InvariantCulture);
                                order.IsBuyOrder = bool.Parse(values[7]);
                                order.DateIssued = DateTime.Parse(values[8], CultureInfo.InvariantCulture);
                                order.Duration = int.Parse(values[9], CultureInfo.InvariantCulture);
                                order.StationId = long.Parse(values[10], CultureInfo.InvariantCulture);
                                order.RegionId = int.Parse(values[11], CultureInfo.InvariantCulture);
                                order.SystemId = int.Parse(values[12], CultureInfo.InvariantCulture);
                                order.NumJumpsAway = int.Parse(values[13], CultureInfo.InvariantCulture);
                            }

                            _orders.Add(order);
                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                // Filter results
                var filteredOrders = _orders.Where(o => o.NumJumpsAway <= JumpsOut).ToList();
                var filteredSellOrders = filteredOrders.Where(o => !o.IsBuyOrder).ToList();
                var filteredBuyOrders = filteredOrders.Where(o => o.IsBuyOrder).ToList();

                // Calculate margin
                _sellPrice = 0.0;
                _buyPrice = 0.0;

                if (filteredSellOrders.ElementAtOrDefault(0) != null)
                    _sellPrice = filteredSellOrders[0].Price - 0.01;
                if (filteredBuyOrders.ElementAtOrDefault(0) != null)
                    _buyPrice = filteredBuyOrders[0].Price + 0.01;


                var temp = e.Name.Split('.');
                var tempList = temp[0].Split('-').ToList();
                tempList.RemoveAt(0);
                tempList.RemoveAt(tempList.Count - 1);
                ItemName = string.Join("-", tempList.ToArray());


                Buyout = filteredSellOrders.Sum(o => o.Price * o.VolumeRemaining).ToString("N");
                NumBuyOrders = filteredBuyOrders.Count.ToString();
                NumSellOrders = filteredSellOrders.Count.ToString();

                BuyOrderFulfillment =
                    $"{filteredBuyOrders.Sum(o => o.VolumeEntered - o.VolumeRemaining)}/{filteredBuyOrders.Sum(o => o.VolumeEntered)}";
                SellOrderFulfillment =
                    $"{filteredSellOrders.Sum(o => o.VolumeEntered - o.VolumeRemaining)}/{filteredSellOrders.Sum(o => o.VolumeEntered)}";

                SellCopyPrice = _sellPrice.ToString("N");
                BuyCopyPrice = _buyPrice.ToString("N");

                switch (SelectedEnum)
                {
                    case CopyEnum.Sell:
                        SellPriceClipboardCopy();
                        break;
                    case CopyEnum.Buy:
                        BuyPriceClipboardCopy();
                        break;
                    case CopyEnum.None:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(SelectedEnum), SelectedEnum, null);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        #region Margin Tool Bindings

        private DataTable _tensDataTable = new DataTable();

        public DataTable TensDataTable
        {
            get => _tensDataTable;
            set => SetProperty(ref _tensDataTable, value);
        }

        private DataTable _fivesDataTable = new DataTable();

        public DataTable FivesDataTable
        {
            get => _fivesDataTable;
            set => SetProperty(ref _fivesDataTable, value);
        }

        private string _itemName = "Export Item Market Data In-Game";

        public string ItemName
        {
            get => _itemName;
            set => SetProperty(ref _itemName, value);
        }

        private string _margin = "0.00%";

        public string Margin
        {
            get => _margin;
            set => SetProperty(ref _margin, value);
        }

        private string _markup = "0.00%";

        public string Markup
        {
            get => _markup;
            set => SetProperty(ref _markup, value);
        }

        private string _profit = "0.00";

        public string Profit
        {
            get => _profit;
            set => SetProperty(ref _profit, value);
        }

        private string _revenue = "0";

        public string Revenue
        {
            get => _revenue;
            set => SetProperty(ref _revenue, value);
        }

        private string _costs = "0";

        public string Costs
        {
            get => _costs;
            set => SetProperty(ref _costs, value);
        }

        private string _buyout = "0";

        public string Buyout
        {
            get => _buyout;
            set => SetProperty(ref _buyout, value);
        }

        private string _numBuyOrders = "0";

        public string NumBuyOrders
        {
            get => _numBuyOrders;
            set => SetProperty(ref _numBuyOrders, value);
        }

        private string _numSellOrders = "0";

        public string NumSellOrders
        {
            get => _numSellOrders;
            set => SetProperty(ref _numSellOrders, value);
        }


        private string _sellOrderFulfillment = "0/0";

        public string SellOrderFulfillment
        {
            get => _sellOrderFulfillment;
            set => SetProperty(ref _sellOrderFulfillment, value);
        }

        private string _buyOrderFulfillment = "0/0";

        public string BuyOrderFulfillment
        {
            get => _buyOrderFulfillment;
            set => SetProperty(ref _buyOrderFulfillment, value);
        }

        private uint _jumpsOut;

        public uint JumpsOut
        {
            get => App.Settings.MarginToolSettings.JumpsOut;
            set
            {
                SetProperty(ref _jumpsOut, value);
                App.Settings.MarginToolSettings.JumpsOut = value;
            }
        }

        private double _buyBroker = 0.05;

        public double BuyBroker
        {
            get => _buyBroker;
            set
            {
                SetProperty(ref _buyBroker, value);
                UpdateMarginInformation(_sellPrice, _buyPrice);
            }
        }

        private double _sellBroker = 0.05;

        public double SellBroker
        {
            get => _sellBroker;
            set
            {
                SetProperty(ref _sellBroker, value);
                UpdateMarginInformation(_sellPrice, _buyPrice);
            }
        }

        private double _salesTax = 0.05;

        public double SalesTax
        {
            get => _salesTax;
            set
            {
                SetProperty(ref _salesTax, value);
                UpdateMarginInformation(_sellPrice, _buyPrice);
            }
        }

        private double _buyPrice;
        private string _buyCopyPrice;

        public string BuyCopyPrice
        {
            get => _buyCopyPrice;
            set
            {
                try
                {
                    var parsedPrice = double.Parse(value);
                    _buyPrice = parsedPrice;
                }
                catch (Exception)
                {
                    SetProperty(ref _sellCopyPrice, "");
                    return;
                }

                UpdateMarginInformation(_sellPrice, _buyPrice);

                SetProperty(ref _buyCopyPrice, _buyPrice.ToString("N"));
            }
        }

        private double _sellPrice;
        private string _sellCopyPrice;

        public string SellCopyPrice
        {
            get => _sellCopyPrice;
            set
            {
                try
                {
                    var parsedPrice = double.Parse(value);
                    _sellPrice = parsedPrice;
                }
                catch (Exception)
                {
                    SetProperty(ref _sellCopyPrice, "");
                    return;
                }

                UpdateMarginInformation(_sellPrice, _buyPrice);

                SetProperty(ref _sellCopyPrice, _sellPrice.ToString("N"));
            }
        }

        private CopyEnum _selectedEnum = CopyEnum.None;

        public CopyEnum SelectedEnum
        {
            get => _selectedEnum;
            set
            {
                SetProperty(ref _selectedEnum, value);
                switch (value)
                {
                    case CopyEnum.Sell:
                        SellPriceClipboardCopy();
                        break;
                    case CopyEnum.Buy:
                        BuyPriceClipboardCopy();
                        break;
                    case CopyEnum.None:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }
            }
        }

        public enum CopyEnum
        {
            None,
            Sell,
            Buy
        }

        #endregion
    }
}