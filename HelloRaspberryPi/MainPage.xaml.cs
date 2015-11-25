using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Windows.Devices.Gpio;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace HelloRaspberryPi
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            InitWeather();
            InitGPIO();
            InitTimer();
        }

        #region Weather      

        // weather url from slovenian weather agency for Ljubljana
        private const string WeatherUrl = 
            @"http://www.meteo.si/uploads/probase/www/observ/surface/text/sl/observation_LJUBL-ANA_BEZIGRAD_latest.xml";

        public async void InitWeather()
        {
            WeatherCity.Visibility = Visibility.Visible;
            WeatherData.Visibility = Visibility.Visible;

            SetStatus($"Getting weather data...");
            try
            {
                // create http client with correct url, make async call
                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(new Uri(WeatherUrl));

                // make sure we got 200 back and get content
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();

                // turn response text in xml and parse it                
                var xmlDoc = XDocument.Parse(responseBody);

                // get data from xml document
                var city = xmlDoc.Descendants("metData").First().Descendants("domain_longTitle").First().Value;
                var state = xmlDoc.Descendants("metData").First().Descendants("nn_shortText").First().Value;
                var temp = xmlDoc.Descendants("metData").First().Descendants("t").First().Value;

                // display data
                WeatherCity.Text = city;
                WeatherData.Text = FixEncoding(string.Format("{0}, {1} °C", state, temp));

                SetStatus($"Received weather data for {city}");
            }
            catch (Exception ex)
            {
                // something went wrogn!
                SetStatus($"Error getting weather: {ex.Message}");
            }
        }

        #endregion

        #region Blinker
       
        private const int LedPin = 5;

        private GpioPin _ledPin;
        private GpioPinValue _ledPinValue;

        private DispatcherTimer _timer;

        private readonly SolidColorBrush _onBrush = new SolidColorBrush(Colors.Red);
        private readonly SolidColorBrush _offBrush = new SolidColorBrush(Colors.LightGray);

        private bool _eventFromButton;        

        private void InitGPIO()
        {
            LED.Visibility = Visibility.Visible;            

            // try to initialize GPIO controller on RPi or similar device
            var gpio = GpioController.GetDefault();

            // show an error if there is no GPIO controller
            if (gpio == null)
            {
                _ledPin = null;
                SetStatus("There is no GPIO controller on this device.\nAre you running this in Raspberry Pi?");

                TimerSwitch.IsEnabled = false;

                return;
            }

            SetStatus("GPIO pin initialized correctly.");

            // initialize LED pin, make it output and turn it off

            _ledPin = gpio.OpenPin(LedPin);
            _ledPin.SetDriveMode(GpioPinDriveMode.Output);

            _ledPinValue = GpioPinValue.High;
            _ledPin.Write(_ledPinValue);

            // initialize Button pin, make it input and not bounce, attach handler

            _buttonPin = gpio.OpenPin(ButtonPin);
            _buttonPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            _buttonPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);
            _buttonPin.ValueChanged += buttonPin_ValueChanged;

        }
      
        private void InitTimer()
        {
            TimerSwitch.Visibility = Visibility.Visible;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += Timer_Tick;

            // if LED is initialzed, start timer
            if (_ledPin != null)
            {
                _timer.Start();
            }
        }

        private void Timer_Tick(object sender, object e)
        {
            if (_ledPinValue == GpioPinValue.Low)
            {
                TurnLedOff();
            }
            else
            {
                TurnLedOn();
            }
        }

        private void TurnLedOn()
        {
            _ledPinValue = GpioPinValue.Low;
            _ledPin.Write(_ledPinValue);
            LED.Fill = _onBrush;
        }

        private void TurnLedOff()
        {
            _ledPinValue = GpioPinValue.High;
            _ledPin.Write(_ledPinValue);
            LED.Fill = _offBrush;
        }
                    
        private void TimerSwitch_Toggled(object sender, RoutedEventArgs e)
        {                        
            if (!_eventFromButton)
                ToggleTimer();

            _eventFromButton = false;
        }

        private void ToggleTimer(string source = "Graphics Interface")
        {
            if (_timer == null) return;
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                SetStatus($"Blinking stopped from {source}.");
                
                if (TimerSwitch.IsOn) TimerSwitch.IsOn = false;

                TurnLedOff();
            }
            else
            {
                _timer.Start();
                SetStatus($"Blinking activated from {source}.");                
                if (!TimerSwitch.IsOn) TimerSwitch.IsOn = true;

            }
        }       

        #endregion

        #region Button

        private GpioPin _buttonPin;
        private const int ButtonPin = 6;        

        private void buttonPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            // need to invoke UI updates on the UI thread because this event
            // handler gets invoked on a separate thread.
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (e.Edge == GpioPinEdge.FallingEdge)
                {
                    _eventFromButton = true;
                    ToggleTimer("Button on Device");
                }
            });
        }

        #endregion

        #region Helper methods

        private void SetStatus(string status)
        {
            var statusLines = StatusText.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            var statusList = new List<string>(statusLines);
            statusList.Add(status);
            if (statusList.Count > 5) statusList.RemoveAt(0);
            StatusText.Text = string.Join(Environment.NewLine, statusList);
        }

        private string FixEncoding(string value)
        {
            return value.Replace("Ä", "č");
        }

        #endregion
    }
}
