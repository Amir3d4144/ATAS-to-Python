using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Timers;
using Utils.Common;
using Utils.Common.Logging;
using OFT.Attributes;
using ATAS.Indicators.Technical.Properties;


namespace ATAS.Indicators.Technichal
{
    public class ATAS_To_Python_v1 : Indicator
    {

        #region Fields

        private List<DateTime> end_time_list = new List<DateTime>();
        private ArrayList allPrice = new ArrayList();
        private Dictionary<decimal, ((string, object), (string, object))> footprint_dic = new
                            Dictionary<decimal, ((string, object), (string, object))>();

        private DataTable table = new DataTable();
        private DataTable bid_table = new DataTable();
        private DataTable ask_table = new DataTable();

        private DateTime start_time = DateTime.MinValue;
        private DateTime end_time = DateTime.MinValue;
        private System.Timers.Timer _timer;
        private DateTime current_time;

        private int time_frame = 15;
        private int i = 1;
        private readonly string filePath = "D:\\Writing C# Code For Atas\\ATAS To Python\\footprint.csv";

        [Display(ResourceType = typeof(Resources), GroupName = "TimeFrame", Name = "Period")]
        public int MyTimeFrame
        {
            get { return time_frame; }
            set
            {
                time_frame = value;

                end_time_list.Clear();
                allPrice.Clear();
                footprint_dic.Clear();
                bid_table.Clear();
                ask_table.Clear();
                table.Clear();

                RecalculateValues();
            }
        }

        #endregion

        public ATAS_To_Python_v1()
        {
            // Initialize and start the timer
            _timer = new System.Timers.Timer();
            _timer.Elapsed += TimerElapsed;
            _timer.Interval = 1;
            _timer.Start();

            //-----------------------------------------------
            table.Columns.Add("Time", typeof(DateTime));
            table.Columns.Add("Price", typeof(decimal));
            table.Columns.Add("Volume", typeof(decimal));
            table.Columns.Add("Direction", typeof(TradeDirection));
            //--------------------------------------------------
            bid_table.Columns.Add("Time", typeof(DateTime));
            bid_table.Columns.Add("Price", typeof(decimal));
            bid_table.Columns.Add("Volume", typeof(decimal));
            bid_table.Columns.Add("Direction", typeof(TradeDirection));
            //--------------------------------------------------
            ask_table.Columns.Add("Time", typeof(DateTime));
            ask_table.Columns.Add("Price", typeof(decimal));
            ask_table.Columns.Add("Volume", typeof(decimal));
            ask_table.Columns.Add("Direction", typeof(TradeDirection));
            //--------------------------------------------------
        }

        protected override void OnCalculate(int bar, decimal value)
        {

        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            current_time = DateTime.Now.ToUniversalTime();
            var _minute = current_time.Minute;
            var _second = current_time.Second;

            //----------------------------------------------------------------            

            if (_minute % time_frame == 0 & _second == 0)
            {
                if (end_time_list.Count == 0)
                {
                    end_time_list.Add(current_time);
                    end_time_list.Add(DateTime.MinValue);
                }
                else
                {
                    end_time_list.Add(current_time);
                }

                var start = end_time_list[0];
                end_time = start.AddMinutes(time_frame);// End Time of a Candle

                // Calculate the Start Time of a Candle 
                if (current_time <= start & end_time_list[i] != end_time_list[0])
                {
                    start_time = start;
                    i++;

                    this.LogInfo("Start Time: {0: HH:mm:ss} --  End Time: {1: HH:mm:ss} -- Time Frame: M{2}", start_time, end_time, time_frame);
                }
                else
                    i++;

                if (current_time >= end_time)
                {

                    end_time_list.Clear();
                    i = 1;

                    //-------------------------------------------------------------

                    allPrice.Clear();

                    foreach (DataRow row in table.Rows)
                    {
                        allPrice.Add(row.ItemArray[1]);
                    }
                    HashSet<decimal> uniqueSet = new HashSet<decimal>(allPrice.Cast<decimal>());
                    decimal[] uniqueAllPrice = uniqueSet.ToArray();
                    //-------------------------------------------------------------

                    footprint_dic.Clear();

                    foreach (var _price_ in uniqueAllPrice)
                    {
                        bid_table.Clear();
                        ask_table.Clear();

                        DataRow[] _bid = table.Select($"Price = {_price_} AND Direction = 2");
                        var count_bid = _bid.Count();
                        if (count_bid != 0)
                        {
                            for (var j = 0; j < count_bid; j++)
                            {
                                bid_table.Rows.Add(_bid[j].ItemArray);
                            }
                        }
                        DataRow[] _ask = table.Select($"Price = {_price_} AND Direction = 1");
                        var count_ask = _ask.Count();
                        if (count_ask != 0)
                        {
                            for (var k = 0; k < count_ask; k++)
                            {
                                ask_table.Rows.Add(_ask[k].ItemArray);
                            }
                        }

                        footprint_dic.Add(_price_, (("Bid", bid_table.Compute("SUM(Volume)", string.Empty)),
                                              ("Ask", ask_table.Compute("SUM(Volume)", string.Empty))));

                    }

                    using (StreamWriter writer = new StreamWriter(filePath))
                    {

                        writer.WriteLine("Price,Bid,Ask,Time");

                        foreach (KeyValuePair<decimal, ((string, object), (string, object))> item in footprint_dic)
                        {
                            decimal key = item.Key;
                            object bid = item.Value.Item1.Item2;
                            object ask = item.Value.Item2.Item2;

                            writer.WriteLine("{0},{1},{2},{3: HH:mm:ss}", key, bid, ask, current_time.AddMinutes(-time_frame));
                        }
                    }
                    this.LogInfo("FootPrint was Made at: {0: HH:mm:ss}", current_time.AddMinutes(-time_frame)); ;
                    footprint_dic.Clear();
                    table.Clear();
                }
            }
        }

        protected override void OnNewTrade(MarketDataArg trade)
        {
            var _time = trade.Time;
            var _price = trade.Price;
            var _volume = trade.Volume;
            var _direction = trade.Direction;
            //-----------------------------------------------------------------

            if (current_time < end_time)
            {
                table.Rows.Add(_time, _price, _volume, _direction);
            }
        }

        protected override void OnDispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}


