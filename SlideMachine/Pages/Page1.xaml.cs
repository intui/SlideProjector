using System;
using Windows.UI.Xaml.Controls;
using Windows.Media.SpeechSynthesis;
using Windows.Media.SpeechRecognition;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using Windows.UI.Core;
using Windows.Devices.Gpio;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SlideMachine.Pages
{
    public sealed partial class Page1 : Page
    {
        private CoreDispatcher dispatcher;
        private SpeechRecognizer recognizer;
        private const int Light_PIN = 23;
        private const int Trigger_PIN = 24;
        private GpioPin lightPin;
        private GpioPin triggerPin;
        private GpioPinValue lightPinValue;
        private GpioPinValue triggerPinValue;
        private DispatcherTimer timer;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);
        private bool gpioEnabled = false;
        private int slideCounter = 1;
        private int slideCounterMax = 20;
        private bool slideshow = false;
        private bool lightOn = false;
        public Page1()
        {
            this.InitializeComponent();
            InitGPIO();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            //bool permissionGained = await AudioCapturePermissions.RequestMicrophoneCapture();
            if (recognizer == null)
            {
                recognizer = new SpeechRecognizer();
                var languages = SpeechRecognizer.SupportedGrammarLanguages;
                var SysSpeech = SpeechRecognizer.SystemSpeechLanguage;
            }
            string[] possibleAnswers = { "Light on", "Light off", "on", "off", "light", "dark", "bright", "next", "previous", "forward", "back", "slideshow", "stop" }; //, "start slideshow", "stop slideshow", };
            var listConstraint = new SpeechRecognitionListConstraint(possibleAnswers, "Answer");
            recognizer.Constraints.Add(listConstraint);
            listenText.Text = recognizer.CurrentLanguage.DisplayName;
            await recognizer.CompileConstraintsAsync();
            recognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
            recognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            await recognizer.ContinuousRecognitionSession.StartAsync();
            timer = new DispatcherTimer();
        }

        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            SpeechRecognitionResult tmpRes = args.Result;
            if (tmpRes != null && tmpRes.Status.Equals(SpeechRecognitionResultStatus.Success))

            {
                if (tmpRes.Confidence == SpeechRecognitionConfidence.Rejected)
                    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        listenText.Text = "didn't get cha.";
                    });
                else
                    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        listenText.Text = tmpRes.Text;
                        if (!slideshow)
                        {
                            if (listenText.Text.Equals("Light on") || listenText.Text.Equals("on") || listenText.Text.Equals("light") || listenText.Text.Equals("bright"))
                            {
                                lightPinValue = GpioPinValue.Low;
                                lightPin.Write(lightPinValue);
                                LED.Fill = redBrush;
                                lightOn = true;
                            }
                            if (listenText.Text.Equals("Light off") || listenText.Text.Equals("off") || listenText.Text.Equals("dark"))
                            {
                                lightPinValue = GpioPinValue.High;
                                lightPin.Write(lightPinValue);
                                LED.Fill = grayBrush;
                                lightOn = false;
                            }
                            if (listenText.Text.Equals("next") || listenText.Text.Equals("forward"))
                            {
                                triggerPinValue = GpioPinValue.Low;
                                timer.Interval = TimeSpan.FromMilliseconds(200);
                                timer.Tick += Timer_Tick;
                                triggerPin.Write(triggerPinValue);
                                timer.Start();
                                slideCounter++;
                            }
                            if (listenText.Text.Equals("previous") || listenText.Text.Equals("back"))
                            {
                                triggerPinValue = GpioPinValue.Low;
                                timer.Interval = TimeSpan.FromMilliseconds(700);
                                timer.Tick += Timer_Tick;
                                triggerPin.Write(triggerPinValue);
                                timer.Start();
                                slideCounter--;
                            }
                            if (listenText.Text.Equals("start slideshow") || listenText.Text.Equals("slideshow"))
                            {
                                //triggerPinValue = GpioPinValue.Low;
                                //triggerPin.Write(triggerPinValue);
                                timer = new DispatcherTimer();
                                timer.Interval = TimeSpan.FromMilliseconds(100);
                                timer.Tick += Slideshow_Tick;
                                timer.Start();
                                slideshow = true;
                            }
                        }
                        else //slideshow mode
                        {
                            if (listenText.Text.Equals("stop slideshow") || listenText.Text.Equals("stop"))
                            {

                                timer.Stop();
                                timer.Tick -= Slideshow_Tick;
                                slideshow = false;
                                timer = new DispatcherTimer(); //??
                                //if (timer.Tick != null)
                                {

                                }
                            }
                        }
                    });
            }
        }

        private void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            //throw new NotImplementedException();
        }

        private void InitGPIO()
        {
            GpioController gpio=null;
            try
            {
                gpio = GpioController.GetDefault();
            }
            catch(Exception ex)
            {
                // no Gpio on device
            }

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                lightPin = null;
                GpioStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            lightPin = gpio.OpenPin(Light_PIN);
            lightPinValue = GpioPinValue.High;
            lightPin.Write(lightPinValue);
            lightPin.SetDriveMode(GpioPinDriveMode.Output);
            triggerPin = gpio.OpenPin(Trigger_PIN);
            triggerPinValue = GpioPinValue.High;
            triggerPin.Write(triggerPinValue);
            triggerPin.SetDriveMode(GpioPinDriveMode.Output);

            GpioStatus.Text = "GPIO pins initialized correctly.";
            gpioEnabled = true;
        }

        private void Timer_Tick(object sender, object e)
        {
            triggerPinValue = GpioPinValue.High;
            triggerPin.Write(triggerPinValue);
            timer.Stop();
        }
        private void Slideshow_Tick(object sender, object e)
        {
            if (lightOn)
            {
                triggerPinValue = GpioPinValue.High;
                triggerPin.Write(triggerPinValue);
                timer.Interval = TimeSpan.FromSeconds(2);
            }
            else
            {
                triggerPinValue = GpioPinValue.Low;
                triggerPin.Write(triggerPinValue);
                timer.Interval = TimeSpan.FromMilliseconds(200);
            }
        }
        private void Slideshow_Tack(object sender, object e)
        {
            triggerPinValue = GpioPinValue.Low;
            triggerPin.Write(triggerPinValue);
            timer.Interval = TimeSpan.FromMilliseconds(200);
            timer.Tick -= Slideshow_Tack;
            timer.Tick += Slideshow_Tick;
        }
        private async Task<SpeechRecognitionResult> RecognizeSpeech()
        {
            try
            {
                if (recognizer == null)
                {
                    recognizer = new SpeechRecognizer();
                    var languages = SpeechRecognizer.SupportedGrammarLanguages;
                    var SysSpeech = SpeechRecognizer.SystemSpeechLanguage;

                    string[] possibleAnswers = { "Light on", "Light off", "on", "off", "light", "dark", "bright", "next", "previous", "forward", "back" };
                    var listConstraint = new SpeechRecognitionListConstraint(possibleAnswers, "Answer");
                    //recognizer.UIOptions.ExampleText = @"Bsp. 'ja','nein'";
                    recognizer.Constraints.Add(listConstraint);
                    listenText.Text = recognizer.CurrentLanguage.DisplayName;
                    await recognizer.CompileConstraintsAsync();
                }
                SpeechRecognitionResult result = await recognizer.RecognizeAsync(); //.RecognizeWithUIAsync();
                return result;
            }
            catch (Exception exception)
            {
                const uint HResultPrivacyStatementDeclined = 0x80045509;
                if ((uint)exception.HResult == HResultPrivacyStatementDeclined)
                {
                    var messageDialog = new Windows.UI.Popups.MessageDialog("You must accept the speech privacy policy");
                    messageDialog.ShowAsync().GetResults();
                }
                else
                {
                    //Debug.WriteLine("Error: " + exception.Message);
                }
            }
            return null;
        }

        private async Task SpeakText(string textToSpeak)
        {
            using (var speech = new SpeechSynthesizer())
            {

                //Retrieve the first German female voice
                speech.Voice = SpeechSynthesizer.AllVoices.First(i => (i.Gender == VoiceGender.Female && i.Description.Contains("English")));
                //Generate the audio stream from plain text
                SpeechSynthesisStream ttsStream = await speech.SynthesizeTextToStreamAsync(textToSpeak);
                mediaPlayer.SetSource(ttsStream, ttsStream.ContentType);
                mediaPlayer.Play();
                //mediaPlayer.CurrentStateChanged += OnStateChanged;
            }
        }
    }
}
