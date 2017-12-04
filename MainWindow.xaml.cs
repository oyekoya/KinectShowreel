//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Wole Oyekoya.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.GreenScreen
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Media.Effects;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Navigation;
    using System.Windows.Shapes;
    using Microsoft.Expression.Encoder;
    using Microsoft.Expression.Encoder.Devices;
    using Microsoft.Expression.Encoder.Live;
    using Microsoft.Kinect;
    using KinectDepthSmoothing;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {   
        /// <summary>
        /// Format we will use for the depth stream
        /// </summary>
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;

        /// <summary>
        /// Format we will use for the color stream
        /// </summary>
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;
        private bool _useFiltering;
        private FilteredSmoothing smoothingFilter = new FilteredSmoothing();

        private bool _useAverage;
        private AveragedSmoothing smoothingAverage = new AveragedSmoothing();

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Bitmap that will hold opacity mask information
        /// </summary>
        private WriteableBitmap playerOpacityMaskImage = null;
        //private WriteableBitmap playerOpacityMaskImage2 = null;

        /// <summary>
        /// Intermediate storage for the depth data received from the sensor
        /// </summary>
        private DepthImagePixel[] depthPixels;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// Intermediate storage for the green screen opacity mask
        /// </summary>
        private int[] greenScreenPixelData;
        //private int[] greenScreenPixelData2;

        /// <summary>
        /// Intermediate storage for the depth to color mapping
        /// </summary>
        private ColorImagePoint[] colorCoordinates;

        /// <summary>
        /// Inverse scaling factor between color and depth
        /// </summary>
        private int colorToDepthDivisor;

        /// <summary>
        /// Width of the depth image
        /// </summary>
        private int depthWidth;

        /// <summary>
        /// Height of the depth image
        /// </summary>
        private int depthHeight;

        /// <summary>
        /// Indicates opaque in an opacity mask
        /// </summary>
        private int opaquePixelValue = -1;

        // Data class that holds all the Encoder data
        public Data data;

        // Flag for if we are capturing or not
        private bool isCapturing;

        // What the cursor is before we change it.
        private Cursor defaultCursor;
        
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            IsPlaying(false);

            //// Sets default capture window dimensions
            //int x = 300, y = 300;
            //int w = 200, h = 200;

            // Set rectangle
            // To do: need to figure out how to set left and top correctly
            //Canvas.SetLeft(CaptureRect, 0);
            //Canvas.SetTop(CaptureRect, 0);
            //CaptureRect.Width = w;
            //CaptureRect.Height = h;

            data = new Data();

            DataContext = data;
            defaultCursor = this.Cursor;
        }

        #region IsPlaying(bool)
        private void IsPlaying(bool bValue)
        {
            btnStop.IsEnabled = bValue;
            btnMoveBackward.IsEnabled = bValue;
            btnMoveForward.IsEnabled = bValue;
            btnPlay.IsEnabled = bValue;
        }
        #endregion

        #region Play and Pause
        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            IsPlaying(true);
            if (btnPlay.Content.ToString() == "Play")
            {
                MediaEL.Play();
                btnPlay.Content = "Pause";
            }
            else
            {
                MediaEL.Pause();
                btnPlay.Content = "Play";
            }
        }
        #endregion

        #region Stop
        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            MediaEL.Stop();
            btnPlay.Content = "Play";
            IsPlaying(false);
            btnPlay.IsEnabled = true;
        }
        #endregion

        #region Back and Forward
        private void btnMoveForward_Click(object sender, RoutedEventArgs e)
        {
            MediaEL.Position = MediaEL.Position + TimeSpan.FromSeconds(10);
        }

        private void btnMoveBackward_Click(object sender, RoutedEventArgs e)
        {
            MediaEL.Position = MediaEL.Position - TimeSpan.FromSeconds(10);
        }
        #endregion
        
        #region Open Image
        private void imgOpen_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "Image Files(*.PNG;*.BMP;*.JPG;*.GIF)|*.PNG;*.BMP;*.JPG;*.GIF|All files (*.*)|*.*";// "Image Files (*.jpg)|*.jpg";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(ofd.FileName);
                // To save significant application memory, set the DecodePixelWidth or   
                // DecodePixelHeight of the BitmapImage value of the image source to the desired  
                // height or width of the rendered image. If you don't do this, the application will  
                // cache the image as though it were rendered as its normal size rather then just  
                // the size that is displayed. 
                // Note: In order to preserve aspect ratio, set DecodePixelWidth 
                // or DecodePixelHeight but not both.
                bi.DecodePixelWidth = 728;
                bi.EndInit();
                //Backdrop.Stretch = Stretch.Fill;
                Backdrop.Source = bi;
                Backdrop.Visibility = Visibility.Visible;
                MediaEL.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Open Media
        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "Video Files (*.wmv;*.avi)|*.wmv;*.avi";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MediaEL.Source = new Uri(ofd.FileName);
                btnPlay.IsEnabled = true;
                Backdrop.Visibility = Visibility.Collapsed;
                MediaEL.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region UI Event Handlers
        private void CheckBoxUseFilteringChanged(object sender, RoutedEventArgs e)
        {
            _useFiltering = this.checkBoxUseFiltering.IsChecked.Value;
        }

        private void CheckBoxUseAverageChanged(object sender, RoutedEventArgs e)
        {
            _useAverage = this.checkBoxUseAverage.IsChecked.Value;
        }


        private void SliderInnerBand_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.smoothingFilter.InnerBandThreshold = (int)e.NewValue;
        }

        private void SliderOuterBand_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.smoothingFilter.OuterBandThreshold = (int)e.NewValue;
        }

        private void SliderAverage_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.smoothingAverage.AverageFrameCount = (int)e.NewValue;
        }
        #endregion UI Event Handlers

        private void AudioDevicesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Sets the audio source index based on the selection
            data.SetSource(AudioDevicesComboBox.SelectedIndex);
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            if (isCapturing)
            {
                isCapturing = false;
                CaptureRect.Visibility = Visibility.Collapsed;
                CaptureButton.Content = "Video Capture";
                data.Job.StopEncoding();
            }
            else
            {
                isCapturing = true;
                CaptureRect.Visibility = Visibility.Visible;
                CaptureButton.Content = "Stop Capture";
                Point rectPoint = CaptureRect.PointToScreen(new Point(0, 0));
                data.Start((int)rectPoint.X, (int)rectPoint.Y, (int)CaptureRect.Width, (int)CaptureRect.Height);
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthFormat);

                this.depthWidth = this.sensor.DepthStream.FrameWidth;

                this.depthHeight = this.sensor.DepthStream.FrameHeight;

                this.sensor.ColorStream.Enable(ColorFormat);

                int colorWidth = this.sensor.ColorStream.FrameWidth;
                int colorHeight = this.sensor.ColorStream.FrameHeight;

                this.colorToDepthDivisor = colorWidth / this.depthWidth;

                // Turn on to get player masks
                this.sensor.SkeletonStream.Enable();

                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                this.greenScreenPixelData = new int[this.sensor.DepthStream.FramePixelDataLength];
                //this.greenScreenPixelData2 = new int[this.sensor.DepthStream.FramePixelDataLength];

                this.colorCoordinates = new ColorImagePoint[this.sensor.DepthStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.MaskedColor.Source = this.colorBitmap;
                //this.MaskedColor2.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new depth frame data
                this.sensor.AllFramesReady += this.SensorAllFramesReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
                this.sensor = null;
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // in the middle of shutting down, so nothing to do
            if (null == this.sensor)
            {
                return;
            }

            bool depthReceived = false;
            bool colorReceived = false;

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);
 
                    depthReceived = true;
                }
            }

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    colorReceived = true;
                }
            }

            // do our processing outside of the using block
            // so that we return resources to the kinect as soon as possible
            if (true == depthReceived)
            {
                ///////////////////////////////////////////////
                //need to figure out best place to put this
                if (this._useFiltering)
                    this.depthPixels = this.smoothingFilter.CreateFilteredDepthArray(this.depthPixels, this.depthWidth, this.depthHeight);
                if (this._useAverage)
                    this.depthPixels = this.smoothingAverage.CreateAverageDepthArray(this.depthPixels, this.depthWidth, this.depthHeight);
                ///////////////////////////////////////////////

                this.sensor.CoordinateMapper.MapDepthFrameToColorFrame(
                    DepthFormat,
                    this.depthPixels,
                    ColorFormat,
                    this.colorCoordinates);

                Array.Clear(this.greenScreenPixelData, 0, this.greenScreenPixelData.Length);
                //Array.Clear(this.greenScreenPixelData2, 0, this.greenScreenPixelData2.Length);


                // loop over each row and column of the depth
                for (int y = 0; y < this.depthHeight; ++y)
                {
                    for (int x = 0; x < this.depthWidth; ++x)
                    {
                        // calculate index into depth array
                        int depthIndex = x + (y * this.depthWidth);

                        DepthImagePixel depthPixel = this.depthPixels[depthIndex];

                        int player = depthPixel.PlayerIndex;

                        // if we're tracking a player for the current pixel, do green screen
                        if (player > 0)
                        {
                            // retrieve the depth to color mapping for the current depth pixel
                            ColorImagePoint colorImagePoint = this.colorCoordinates[depthIndex];

                            // scale color coordinates to depth resolution
                            int colorInDepthX = colorImagePoint.X / this.colorToDepthDivisor;
                            int colorInDepthY = colorImagePoint.Y / this.colorToDepthDivisor;

                            // make sure the depth pixel maps to a valid point in color space
                            // check y > 0 and y < depthHeight to make sure we don't write outside of the array
                            // check x > 0 instead of >= 0 since to fill gaps we set opaque current pixel plus the one to the left
                            // because of how the sensor works it is more correct to do it this way than to set to the right
                            if (colorInDepthX > 0 && colorInDepthX < this.depthWidth && colorInDepthY >= 0 && colorInDepthY < this.depthHeight)
                            {
                                // calculate index into the green screen pixel array
                                int greenScreenIndex = colorInDepthX + (colorInDepthY * this.depthWidth);

                                // set opaque
                                this.greenScreenPixelData[greenScreenIndex] = opaquePixelValue;//this.greenScreenPixelData2[greenScreenIndex] = 

                                // compensate for depth/color not corresponding exactly by setting the surrounding pixels
                                // to opaque as well
                                // + + + + +
                                // + + + + +
                                // + + - + +
                                // + + + + +
                                // + + + + +
                                // To-do: Use GUI to control the number of adjacent pixels to consider
                                if (greenScreenIndex > 1500 && greenScreenIndex < (this.sensor.DepthStream.FramePixelDataLength - 1500))
                                {
                                    this.greenScreenPixelData[greenScreenIndex - (this.depthWidth * 2) - 2] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex - (this.depthWidth * 2) - 1] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex - (this.depthWidth * 2)] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex - (this.depthWidth * 2) + 1] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex - (this.depthWidth * 2) + 2] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex - this.depthWidth - 2] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex - this.depthWidth - 1] = opaquePixelValue; //this.greenScreenPixelData2[greenScreenIndex - this.depthWidth - 1] = 
                                    this.greenScreenPixelData[greenScreenIndex - this.depthWidth] = opaquePixelValue; //this.greenScreenPixelData2[greenScreenIndex - this.depthWidth] = 
                                    this.greenScreenPixelData[greenScreenIndex - this.depthWidth + 1] = opaquePixelValue; //this.greenScreenPixelData2[greenScreenIndex - this.depthWidth + 1] = 
                                    this.greenScreenPixelData[greenScreenIndex - this.depthWidth + 2] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex - 2] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex - 1] = opaquePixelValue;//this.greenScreenPixelData2[greenScreenIndex - 1] = 
                                    this.greenScreenPixelData[greenScreenIndex + 1] = opaquePixelValue;//this.greenScreenPixelData2[greenScreenIndex + 1] = 
                                    this.greenScreenPixelData[greenScreenIndex + 2] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex + this.depthWidth - 2] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex + this.depthWidth - 1] = opaquePixelValue; //this.greenScreenPixelData2[greenScreenIndex + this.depthWidth - 1] = 
                                    this.greenScreenPixelData[greenScreenIndex + this.depthWidth] = opaquePixelValue; //this.greenScreenPixelData2[greenScreenIndex + this.depthWidth] = 
                                    this.greenScreenPixelData[greenScreenIndex + this.depthWidth + 1] = opaquePixelValue; //this.greenScreenPixelData2[greenScreenIndex + this.depthWidth + 1] = 
                                    this.greenScreenPixelData[greenScreenIndex + this.depthWidth + 2] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex + (this.depthWidth * 2) - 2] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex + (this.depthWidth * 2) - 1] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex + (this.depthWidth * 2)] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex + (this.depthWidth * 2) + 1] = opaquePixelValue;
                                    this.greenScreenPixelData[greenScreenIndex + (this.depthWidth * 2) + 2] = opaquePixelValue;
                                }

                                //DepthImagePixel adjacentPixel = this.depthPixels[x + (y * this.depthWidth)-1];
                                //if (adjacentPixel.PlayerIndex < 1)
                                //{
                                //  set to transparent pixel
                                //}
                            }
                        }
                    }
                }
            }

            // do our processing outside of the using block
            // so that we return resources to the kinect as soon as possible
            if (true == colorReceived)
            {
                // Write the pixel data into our bitmap
                this.colorBitmap.WritePixels(
                    new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                    this.colorPixels,
                    this.colorBitmap.PixelWidth * sizeof(int),
                    0);

                //******************
                if (this.playerOpacityMaskImage == null)
                {
                    this.playerOpacityMaskImage = new WriteableBitmap(
                        this.depthWidth,
                        this.depthHeight,
                        96,
                        96,
                        PixelFormats.Bgra32,
                        null);

                    MaskedColor.OpacityMask = new ImageBrush { ImageSource = this.playerOpacityMaskImage };
                }

                this.playerOpacityMaskImage.WritePixels(
                    new Int32Rect(0, 0, this.depthWidth, this.depthHeight),
                    this.greenScreenPixelData,
                    this.depthWidth * ((this.playerOpacityMaskImage.Format.BitsPerPixel + 7) / 8),
                    0);

                //MaskedColor.Opacity = 0.5; //
                MaskedColor.Effect = new BlurEffect() { Radius = 10 };
                MaskedColor.Effect = new DropShadowEffect() { ShadowDepth = 10 };
                //******************

                /*if (this.playerOpacityMaskImage2 == null)
                {
                    this.playerOpacityMaskImage2 = new WriteableBitmap(
                        this.depthWidth,
                        this.depthHeight,
                        96,
                        96,
                        PixelFormats.Bgra32,
                        null);

                    MaskedColor2.OpacityMask = new ImageBrush { ImageSource = this.playerOpacityMaskImage2 };
                }

                this.playerOpacityMaskImage2.WritePixels(
                    new Int32Rect(0, 0, this.depthWidth, this.depthHeight),
                    this.greenScreenPixelData2,
                    this.depthWidth * ((this.playerOpacityMaskImage.Format.BitsPerPixel + 7) / 8),
                    0);

                MaskedColor2.Effect = new DropShadowEffect() { ShadowDepth = 10 };*/
                //MaskedColor2.Opacity = 0.5;
                //******************

            }
        }

        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ButtonScreenshotClick(object sender, RoutedEventArgs e)
        {
            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                return;
            }

            int colorWidth = this.sensor.ColorStream.FrameWidth;
            int colorHeight = this.sensor.ColorStream.FrameHeight;

            // create a render target that we'll render our controls to
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                // render the backdrop
                VisualBrush backdropBrush = new VisualBrush(Backdrop);
                dc.DrawRectangle(backdropBrush, null, new Rect(new Point(), new Size(colorWidth, colorHeight)));

                // render the color image masked out by players
                VisualBrush colorBrush = new VisualBrush(MaskedColor);
                dc.DrawRectangle(colorBrush, null, new Rect(new Point(), new Size(colorWidth, colorHeight)));
                //VisualBrush colorBrush2 = new VisualBrush(MaskedColor2);
                //dc.DrawRectangle(colorBrush2, null, new Rect(new Point(), new Size(colorWidth, colorHeight)));
            }

            renderBitmap.Render(dv);
    
            // create a png bitmap encoder which knows how to save a .png file
            BitmapEncoder encoder = new PngBitmapEncoder();

            // create frame from the writable bitmap and add to encoder
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

            string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            string path = System.IO.Path.Combine(myPhotos, "KinectSnapshot-" + time + ".png");

            // write the new file to disk
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                this.statusBarText.Text = string.Format("{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path);
            }
            catch (IOException)
            {
                this.statusBarText.Text = string.Format("{0} {1}", Properties.Resources.ScreenshotWriteFailed, path);
            }
        }
        
        /// <summary>
        /// Handles the checking or unchecking of the near mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxNearModeChanged(object sender, RoutedEventArgs e)
        {
            if (this.sensor != null)
            {
                // will not function on non-Kinect for Windows devices
                try
                {
                    if (this.checkBoxNearMode.IsChecked.GetValueOrDefault())
                    {
                        this.sensor.DepthStream.Range = DepthRange.Near;
                    }
                    else
                    {
                        this.sensor.DepthStream.Range = DepthRange.Default;
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
    }
    public class Data : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public LiveJob Job;
        public LiveDeviceSource Source;

        private ObservableCollection<EncoderDevice> m_AudioDevices;
        public ObservableCollection<EncoderDevice> AudioDevices
        {
            get { return m_AudioDevices; }
            set { m_AudioDevices = value; onPropChanged("AudioDevices"); }
        }

        private string OutputDirectory;

        public Data()
        {
            // Initializes Job and collection objects
            Job = new LiveJob();
            AudioDevices = new ObservableCollection<EncoderDevice>();

            // Set the output directory for the job
            OutputDirectory = string.Format("{0}\\", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            // Setup the the live job parameters
            SetupJob();
        }

        private void SetupJob()
        {
            // Sets the audio devices to the collection. Adds a null at the beginning for user to select no device
            AudioDevices = new ObservableCollection<EncoderDevice>(EncoderDevices.FindDevices(EncoderDeviceType.Audio));
            AudioDevices.Insert(0, null);

            // Gets all the video devices and sets the screen source.
            EncoderDevice Video = null;
            Collection<EncoderDevice> videoSources = EncoderDevices.FindDevices(EncoderDeviceType.Video);
            foreach (EncoderDevice dev in videoSources)
                if (dev.Name.Contains("Screen Capture Source"))
                    Video = dev;

            // Creats the source
            Source = Job.AddDeviceSource(Video, null);

            // Activates sources and sets preset to job
            Job.ActivateSource(Source);
            Job.ApplyPreset(LivePresets.VC1HighSpeedBroadband4x3);
        }

        public void Start(int x, int y, int width, int height)
        {
            // Set capture rectangle
            Source.ScreenCaptureSourceProperties = new ScreenCaptureSourceProperties()
            {
                Left = x,
                Top = y,
                Width = width,
                Height = height,
            };

            // Set output file name to custom name based on timestamp
            string fileName = string.Format("capture_{0}.wmv", DateTime.Now);

            // Remove invalid characters
            fileName = fileName.Replace('/', '-');
            fileName = fileName.Replace(':', '-');

            // Clear any previous outputs
            Job.PublishFormats.Clear();

            // Add publish format and start encode
            Job.PublishFormats.Add(new FileArchivePublishFormat() { OutputFileName = string.Format("{0}{1}", OutputDirectory, fileName) });
            Job.StartEncoding();
        }

        public void SetSource(int index)
        {
            // Sets audio source based on the index selected
            Job.DeviceSources[0].AudioDevice = index < 0 ? null : AudioDevices[index];
        }

        private void onPropChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }
    }
}