﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Reflection;
//using System.Diagnostics;
using System.Threading.Tasks;
//using Microsoft.AspNet.SignalR;
//using Microsoft.Owin.Hosting;

using SmartCOM3Lib;

using TRL.Logging;
using TRL.Common;
using TRL.Common.Data;
using TRL.Common.Extensions.Data;
using TRL.Common.Models;
using TRL.Common.Handlers;
using TRL.Common.Collections;
using TRL.Common.TimeHelpers;
using TRL.Connect.Smartcom.Commands;
using TRL.Connect.Smartcom.Data;
using TRL.Transaction;
//using TRL.Connect.Smartcom;
//using TRL.Connect.Smartcom.Handlers;
//using TRL.Handlers.Spreads;
//using TRL.Handlers.StopLoss;
//using TRL.Handlers.TakeProfit;
//using TRx.TestBack;
using TRx.Helpers;
using TRx.Handlers;
using TRL.Common.Statistics;

//<add key="Symbol" value="Si-9.15_FT" />

namespace TRx.TestBack
{
    class CrossMa1
    {
        private static MarketDataProvider marketDataProvider = 
            new MarketDataProvider();

        private static RawTradingDataProvider rawTradingDataProvider = 
            new RawTradingDataProvider(DefaultLogger.Instance);

        private static SymbolsDataProvider symbolsDataProvider = 
            new SymbolsDataProvider();

        private static IOrderManager orderManager = 
            new TRL.Emulation.BacktestOrderManager(TradingData.Instance, DefaultLogger.Instance);

        //private static SmartComAdapter adapter =
        //    new SmartComAdapter();

        private static TraderBase traderBase =
            //new TraderBase(new SmartComOrderManager());
            new TraderBase(orderManager);
       
        private static StrategyHeader strategyHeader = 
            new StrategyHeader(1,
                "ReversOnBar strategyHeader",
                AppSettings.GetStringValue("Portfolio"),
                AppSettings.GetStringValue("Symbol"),
                AppSettings.GetValue<double>("Amount"));
        
        private static BarSettings barSettings = new BarSettings(
            strategyHeader,
            strategyHeader.Symbol,
            AppSettings.GetValue<int>("Interval"),
            AppSettings.GetValue<int>("Period"));

        //private static ProfitPointsSettings ppSettings =
        //    new ProfitPointsSettings(strategyHeader, AppSettings.GetValue<double>("ProfitPoints"), false);

        //private static TakeProfitOrderSettings poSettings =
        //    new TakeProfitOrderSettings(strategyHeader, 86400);

        //private static StopPointsSettings spSettings =
        //    new StopPointsSettings(strategyHeader, AppSettings.GetValue<double>("StopPoints"), false);

        //private static StopLossOrderSettings soSettings =
        //    new StopLossOrderSettings(strategyHeader, 86400);

        private static string[] assemblies = { "Interop.SmartCOM3Lib.dll", "TRL.Common.dll", "TRL.Connect.Smartcom.dll" };

        static void Main(string[] args)
        {
            TradeConsole.ConsoleSetSize();
            Export.LogAssemblyInfo(assemblies);

            //AddStrategySettings();
            {
                TradingData.Instance.Get<ICollection<StrategyHeader>>().Add(strategyHeader);
                TradingData.Instance.Get<ICollection<BarSettings>>().Add(barSettings);

                //TradingData.Instance.Get<ICollection<ProfitPointsSettings>>().Add(ppSettings);
                //TradingData.Instance.Get<ICollection<TakeProfitOrderSettings>>().Add(poSettings);
                //TradingData.Instance.Get<ICollection<StopPointsSettings>>().Add(spSettings);
                //TradingData.Instance.Get<ICollection<StopLossOrderSettings>>().Add(soSettings);

                //SMASettings smaSettings = new SMASettings(strategyHeader, 7, 14);
                int mafast = AppSettings.GetValue<int>("MaFast");
                int maslow = AppSettings.GetValue<int>("MaSlow");
                SMASettings smaSettings = new SMASettings(strategyHeader, mafast, maslow);
                TradingData.Instance.Get<ICollection<SMASettings>>().Add(smaSettings);
            }

            SmartComHandlers.Instance.Add<_IStClient_DisconnectedEventHandler>(IsDisconnected);
            SmartComHandlers.Instance.Add<_IStClient_ConnectedEventHandler>(IsConnected);

            MakeRangeBarsOnTick updateBarsHandler =
                new MakeRangeBarsOnTick(barSettings,
                    new TimeTracker(),
                    TradingData.Instance,
                    DefaultLogger.Instance);

            SendItemOnBar barItemSender =
                new SendItemOnBar(barSettings,
                                  TradingData.Instance);
            //barItemSender.AddItemHandler(TradeConsole.ConsoleWriteLineBar);

            IndicatorOnBar2Ma indicatorsOnBar =
                new IndicatorOnBar2Ma(strategyHeader,
                    TradingData.Instance,
                    SignalQueue.Instance,
                    DefaultLogger.Instance);
            indicatorsOnBar.AddMa1Handler(TradeConsole.ConsoleWriteLineValueDouble);
            indicatorsOnBar.AddMa2Handler(TradeConsole.ConsoleWriteLineValueDouble);
            indicatorsOnBar.AddCrossUpHandler(TradeConsole.ConsoleWriteLineValueBool);
            indicatorsOnBar.AddCrossDnHandler(TradeConsole.ConsoleWriteLineValueBool);

            ReversOnBar reversHandler =
                new ReversOnBar(strategyHeader,
                    TradingData.Instance,
                    SignalQueue.Instance,
                    DefaultLogger.Instance)
                {
                    IndicatorsOnBar = indicatorsOnBar
                };

            SendItemOnTrade tradeItemSender =
                new SendItemOnTrade(TradingData.Instance, DefaultLogger.Instance);
            //tradeItemSender.AddItemHandler(TradeConsole.ConsoleWriteLineTrade);

            //SendItemOnOrder senderItemOrder =
            //    new SendItemOnOrder(TradingData.Instance.Get<ObservableQueue<Order>>());
            //senderItemOrder.AddedItemHandler(TradeHubStarter.sendOrder);

            //AddStrategySubscriptions();
            {
                DefaultSubscriber.Instance.Portfolios.Add(strategyHeader.Portfolio);
                DefaultSubscriber.Instance.BidsAndAsks.Add(strategyHeader.Symbol);
                DefaultSubscriber.Instance.Ticks.Add(strategyHeader.Symbol);
            }

            //список доступных команд
            TradeConsole.ConsoleWriteCommands();


            TradeHubStarter tradeHubStarter = new TradeHubStarter();
            if (AppSettings.GetValue<bool>("SignalHub"))
            {
                //отправляем через signalR
                barItemSender.AddItemHandler(TradeHubStarter.sendBar);
                tradeItemSender.AddItemHandler(TradeHubStarter.sendTrade);
                indicatorsOnBar.AddMa1Handler(TradeHubStarter.sendValueDouble1);
                indicatorsOnBar.AddMa2Handler(TradeHubStarter.sendValueDouble2);
                indicatorsOnBar.AddCrossUpHandler(TradeHubStarter.sendValueBool);
                indicatorsOnBar.AddCrossDnHandler(TradeHubStarter.sendValueBool);

                //reversHandler.AddMa1Handler(TradeHubStarter.sendIndicator1);
                //reversHandler.AddMa2Handler(TradeHubStarter.sendIndicator2);


                Task.Run(() => tradeHubStarter.StartServer());
                Console.WriteLine(String.Format("Starting server..."));
            }
            else
            {
                Console.WriteLine(String.Format("SignalHub is off"));
            }

            if (AppSettings.GetValue<bool>("ConsoleWaitStart"))
            {
                TradeConsole.WaitStart();
            }

            //adapter.Start();
            TradeConsole.ImportTicksTransaction(args);

            TradeConsole.GetBuySellTrades(strategyHeader);
            TradeConsole.GetDeals(strategyHeader);

            while (true)
            {
                try
                {
                    string command = Console.ReadLine();
                    if (command == "x")
                    {
                        //adapter.Stop();

                        Export.ExportData<Order>(AppSettings.GetValue<bool>("ExportOrdersOnExit"));
                        Export.ExportData<Trade>(AppSettings.GetValue<bool>("ExportTradesOnExit"));
                        Export.ExportData<Bar>(AppSettings.GetValue<bool>("ExportBarsOnExit"));
                        var dealList = TradeConsole.GetDeals(strategyHeader);
                        Export.ExportData<Deal>(dealList.Deals);

                        break;
                    }
                    if (command == "s")
                    {
                        Console.Clear();
                        TradeConsole.GetBuySellTrades(strategyHeader);
                        var dealList = TradeConsole.GetDeals(strategyHeader);
                        Export.ExportData<Deal>(dealList.Deals);
                    }
                    if (command == "p")
                    {
                        Console.Clear();

                        Console.WriteLine(String.Format("Реализованный профит и лосс составляет {0} пунктов",
                            TradingData.Instance.GetProfitAndLossPoints(strategyHeader)));
                    }
                    if (command == "t")
                    {
                        Console.Clear();

                        foreach (Trade item in TradingData.Instance.Get<IEnumerable<Trade>>())
                            Console.WriteLine(item.ToString());
                    }
                    if (command == "b")
                    {
                        Console.Clear();
                        foreach (Bar item in TradingData.Instance.Get<IEnumerable<Bar>>().OrderBy(i => i.DateTime))
                        //foreach (Bar item in TradingData.Instance.Get<IEnumerable<Bar>>())
                        {
                            TradeConsole.ConsoleWriteLineBar(item);
                            //TradeHubStarter.sendBarString(item);
                            TradeHubStarter.sendBar(item);
                        }
                    }
                    if (command == "c")
                    {
                        //Console.Clear();
                        Console.WriteLine("clearChart");
                        TradeHubStarter.clearChart();
                    }
                    if (command == "h")
                    {
                        Console.Clear();
                        TradeConsole.ConsoleWriteCommands();
                    }

                    if (command == "d")
                    {
                        TradeHubStarter.ConsoleWriteTime = !TradeHubStarter.ConsoleWriteTime;
                        Console.WriteLine(String.Format("ConsoleWriteTime {0}",
                                                        TradeHubStarter.ConsoleWriteTime));
                    }
                }
                catch (System.Runtime.InteropServices.COMException e)
                {
                    DefaultLogger.Instance.Log(e.Message);

                    //adapter.Restart();
                }
            }

            if (tradeHubStarter.SignalR != null)
            {
                tradeHubStarter.SignalR.Dispose();
            }
        }

        private static void AddStrategySettings()
        {
            TradingData.Instance.Get<ICollection<StrategyHeader>>().Add(strategyHeader);
            TradingData.Instance.Get<ICollection<BarSettings>>().Add(barSettings);
            //TradingData.Instance.Get<ICollection<ProfitPointsSettings>>().Add(ppSettings);
            //TradingData.Instance.Get<ICollection<TakeProfitOrderSettings>>().Add(poSettings);
            //TradingData.Instance.Get<ICollection<StopPointsSettings>>().Add(spSettings);
            //TradingData.Instance.Get<ICollection<StopLossOrderSettings>>().Add(soSettings);
        }

        private static void AddStrategySubscriptions()
        {
            DefaultSubscriber.Instance.Portfolios.Add(strategyHeader.Portfolio);
            DefaultSubscriber.Instance.BidsAndAsks.Add(strategyHeader.Symbol);
            DefaultSubscriber.Instance.Ticks.Add(strategyHeader.Symbol);
        }
        public static void IsConnected()
        {
            DefaultLogger.Instance.Log("IsConnected.");
            Console.WriteLine("IsConnected.");
            //DefaultLogger.Instance.Log("Requesting history bars.");
            //Console.WriteLine(String.Format("Requesting history bars Symbol: {0} Interval: {1} Period: {2}", barSettings.Symbol, barSettings.Interval, barSettings.Period));
            //ITransaction getBars = new GetBarsCommand(barSettings.Symbol, barSettings.Interval, barSettings.Period);
            //getBars.Execute();
        }
        public static void IsDisconnected(string reason)
        {
            DefaultLogger.Instance.Log("IsDisconnected.");
            Console.WriteLine("IsDisconnected.");
            //DefaultLogger.Instance.Log("Cleaning Bar collection.");
            //Console.WriteLine("Cleaning Bar collection.");
            //TradingData.Instance.Get<ICollection<Bar>>().Clear();
        }
    }
}

////stopLoss
//StrategiesPlaceStopLossByPointsOnTradeHandlers stopLossOnTradeHandlers =
//    new StrategiesPlaceStopLossByPointsOnTradeHandlers(TradingData.Instance,
//        SignalQueue.Instance,
//        DefaultLogger.Instance,
//        AppSettings.GetValue<bool>("MeasureStopFromSignalPrice"));
////stopLoss
//StrategiesStopLossByPointsOnTickHandlers stopLossOnTickHandlers =
//    new StrategiesStopLossByPointsOnTickHandlers(TradingData.Instance,
//        SignalQueue.Instance,
//        DefaultLogger.Instance,
//        AppSettings.GetValue<bool>("MeasureStopFromSignalPrice"));
////takeProfit
//StrategiesPlaceTakeProfitByPointsOnTradeHandlers takeProfitOnTradeHandlers =
//    new StrategiesPlaceTakeProfitByPointsOnTradeHandlers(TradingData.Instance,
//        SignalQueue.Instance,
//        DefaultLogger.Instance,
//        AppSettings.GetValue<bool>("MeasureProfitFromSignalPrice"));
////takeProfit
//StrategiesTakeProfitByPointsOnTickHandlers takeProfitOnTickHandlers =
//    new StrategiesTakeProfitByPointsOnTickHandlers(TradingData.Instance,
//        SignalQueue.Instance,
//        DefaultLogger.Instance,
//        AppSettings.GetValue<bool>("MeasureProfitFromSignalPrice"));

//BreakOutOnTick openHandler =
//    new BreakOutOnTick(strategyHeader,
//        TradingData.Instance,
//        SignalQueue.Instance,
//        DefaultLogger.Instance);

//ReversOnTick openHandler =
//    new ReversOnTick(strategyHeader,
//        TradingData.Instance,
//        SignalQueue.Instance,
//        DefaultLogger.Instance);


//UpdateBarsOnTick updateBarsHandler =
//    new UpdateBarsOnTick(barSettings,
//        new TimeTracker(),
//        TradingData.Instance,
//        DefaultLogger.Instance);