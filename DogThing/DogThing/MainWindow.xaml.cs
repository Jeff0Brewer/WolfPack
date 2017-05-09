using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using EyeXFramework.Wpf;
using Tobii.EyeX.Framework;
using EyeXFramework;

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace DogThing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variables
        //SETUP VARIABLES//
        private static string defaultSenderIP = "169.254.50.139"; //169.254.41.115 A, 169.254.50.139 B
        string compID = "master"; //Set to a, b, or master
        string condition = "fill";
        string testNumber = "0";

        private bool SenderOn = true;
        private bool ReceiverOn = true;
        private static int ReceiverPort = 11000, SenderPort = 11000;//ReceiverPort is the port used by Receiver, SenderPort is the port used by Sender
        private bool communication_started_Receiver = false;//indicates whether the Receiver is ready to receive message(coordinates). Used for thread control
        private bool communication_started_Sender = false;//Indicate whether the program is sending its coordinates to others. Used for thread control
        private System.Threading.Thread communicateThread_Receiver; //Thread for receiver
        private System.Threading.Thread communicateThread_Sender;   //Thread for sender
        private static string SenderIP = "", ReceiverIP = ""; //The IP's for sender and receiver.
        private static string IPpat = @"(\d+)(\.)(\d+)(\.)(\d+)(\.)(\d+)\s+"; // regular expression used for matching ip address
        private Regex r = new Regex(IPpat, RegexOptions.IgnoreCase);//regular expression variable
        private static string NumPat = @"(\d+)\s+";
        private Regex regex_num = new Regex(NumPat, RegexOptions.IgnoreCase);
        private System.Windows.Threading.DispatcherTimer dispatcherTimer;
        private static String sending;
        private static String received;

        Point target = new Point(50,50);
        double targetAngle = 45;
        Point dog = new Point(0, 0);
        double oldx = 0;
        double oldy = 0;
        double angle = 0;
        double wallH = 0;
        double wallV = 0;
        bool habove = true;
        bool vleft = false;
        int numDeaths = 0;

        //Echo Vis
        bool echoOn = false;
        int numEchos = 6;
        int echoTick = 0;
        int echoInterval = 10;
        Point block = new Point(0, 0);
        EchoPoints echoPoints;
        Ellipse[] echos;
        Line[] lines;

        //Line Vis
        Point smoothTrack = new Point(0, 0);
        bool lineOn = false;
        Line[] echoLine;
        Ellipse expandPoint;
        EchoPoints linePoints;
        int lineLength = 30;
        int expandCount = 0;
        double smoothing = .85;
        int lineend = 0;
        int linestart = 0;

        //Elapsed Time
        int startTime = 0;
        int elapsedTime = 0;
        

        //Dot Vis
        bool dotOn = false;

        Point fastTrack = new Point(0, 0);
        Point oFastTrack = new Point(0, 0);
        Point prevPoint = new Point(0, 0);

        EyeXHost eyeXHost;

        //Logging
        StringBuilder csv = new StringBuilder();
        String pathStart = "C:/Users/ResearchSquad/Documents/DogLog/data";
        String filePath;
        DateTime uni = new DateTime(1970, 1, 1);
        string currGaze = "none";
        int testoffset = 0;
        bool finished = false;
        Point goal = new Point(0, 0);

        //Screen scaling
        double xadjust, yadjust;
        double xoffset, yoffset;

        //Performance
        bool wallswitch = true;
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            

            filePath = pathStart + testNumber + ".csv";
            while (File.Exists(filePath)) {
                testoffset++;
                filePath = pathStart + testNumber + "-" + testoffset.ToString() +".csv";
            }
            csv.AppendLine("X,Y,CompID,Time,Unix Time,Condition,Failures,Gaze On");

            startTime = Convert.ToInt32(DateTimeOffset.Now.ToUnixTimeSeconds());
            eyeXHost = new EyeXHost();
            eyeXHost.Start();
            var gazeData = eyeXHost.CreateGazePointDataStream(GazePointDataMode.LightlyFiltered);
            gazeData.Next += gazePoint;

            setupCanvas();
            echoSetup();
            lineSetup();

            if (ReceiverOn)
            {
                IPHostEntry ipHostInfo = Dns.GetHostByName(Dns.GetHostName());
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                //Receive_Status_Text.Text = "Receiving Data at\nIP:" + ipAddress.ToString();
                //Receive_Status_Text.Visibility = Visibility.Visible;
            }
            if (SenderOn)
            {
                SenderIP = defaultSenderIP;
                //Share_Status_Text.Text = "Sharing Data to\nIP:" + SenderIP.ToString();
                //Share_Status_Text.Visibility = Visibility.Visible;
                communication_started_Sender = false;
            }

            Clickeroni.Visibility = Visibility.Visible;
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
            dispatcherTimer.Tick += new EventHandler(update);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            dispatcherTimer.Start();
        }

        private void canv_Loaded(object sender, RoutedEventArgs e)
        {
            if (Clickeroni.Width / Clickeroni.Height < this.Width / this.Height)
            {
                double tx = ((this.Width / this.Height) * Clickeroni.Height);
                xadjust = tx / this.Width;
                xoffset = -((this.Width / this.Height) - (Clickeroni.Width / Clickeroni.Height)) * Clickeroni.Width/4;
                yoffset = 0;
                yadjust = Clickeroni.Height / this.Height;
            }
            else
            {
                double ty = (this.Height / this.Width) * Clickeroni.Width;
                yadjust = ty / this.Height;
                yoffset = -((this.Height / this.Width) - (Clickeroni.Height / Clickeroni.Width)) * Clickeroni.Height / 4;
                xoffset = 0;
                xadjust = Clickeroni.Width / this.Width;
            }
        }

        private void setupCanvas()
        {
            Rectangle handle;
            foreach (UIElement child in canv.Children) {
                if (child.GetType().Equals(dogger.GetType()) && (handle = child as Rectangle).Name.Substring(0, 1).Equals("x")) {
                    if (!handle.Name.Substring(1, 1).Equals(compID) && !handle.Name.Substring(1, 1).Equals("c") && !compID.Equals("master"))
                        handle.Visibility = Visibility.Hidden;
                }
            }
        }

        private void gazePoint(object s, EyeXFramework.GazePointEventArgs e)
        {
            fastTrack = new Point(e.X, e.Y);
        }

        private void update(object sender, EventArgs e)
        {
            movement();
            collision();
            if (wallswitch)
                wallSwitching();
            wallswitch = !wallswitch;

            sendRecieve();

            setGaze();
            if(lineOn)
                lineVis();
            //if(echoOn)
            //    echoVis();
            //if(dotOn)
            //    trackDot();

            //target.X = Canvas.GetLeft(track) - track.Width / 2;
            //target.Y = Canvas.GetTop(track) - track.Height / 2;

            if (!finished)
            {
                logData();
                updateTime();
            }
        }

        private void logData() {
            String newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7}",fastTrack.X,fastTrack.Y,compID,DateTime.Now.TimeOfDay,
                                            DateTimeOffset.Now.ToUnixTimeMilliseconds(),condition,numDeaths,currGaze);
            csv.AppendLine(newLine);
        }

        private void updateTime()
        {
            elapsedTime = Convert.ToInt32(DateTimeOffset.Now.ToUnixTimeSeconds()) - startTime;
            ElapsedTime.Text = (Convert.ToInt32(elapsedTime) / 60).ToString("00") + ":" + (Convert.ToInt32(elapsedTime) % 60).ToString("00");
        }

        private void setGaze() {
            if (Keyboard.IsKeyDown(Key.D0))
            {
                currGaze = "none";
                lineOn = false;
                echoOn = false;
                dotOn = false;
                foreach (Line ln in echoLine)
                    ln.Visibility = Visibility.Hidden;
                expandPoint.Visibility = Visibility.Hidden;
                foreach (Ellipse el in echos)
                    el.Visibility = Visibility.Hidden;
                foreach (Line ln in lines)
                    ln.Visibility = Visibility.Hidden;
                track.Visibility = Visibility.Hidden;
            }
            else if (Keyboard.IsKeyDown(Key.D1))
            {
                currGaze = "line";
                lineOn = true;
                echoOn = false;
                dotOn = false;
                foreach (Line ln in echoLine)
                    ln.Visibility = Visibility.Visible;
                expandPoint.Visibility = Visibility.Visible;
                foreach (Ellipse el in echos)
                    el.Visibility = Visibility.Hidden;
                foreach (Line ln in lines)
                    ln.Visibility = Visibility.Hidden;
                track.Visibility = Visibility.Hidden;
            }
            else if (Keyboard.IsKeyDown(Key.D2))
            {
                currGaze = "echo";
                lineOn = false;
                echoOn = true;
                dotOn = false;
                foreach (Line ln in echoLine)
                    ln.Visibility = Visibility.Hidden;
                expandPoint.Visibility = Visibility.Hidden;
                foreach (Ellipse el in echos)
                    el.Visibility = Visibility.Visible;
                foreach (Line ln in lines)
                    ln.Visibility = Visibility.Visible;
                track.Visibility = Visibility.Hidden;
            }
            else if (Keyboard.IsKeyDown(Key.D3))
            {
                currGaze = "dot";
                lineOn = false;
                echoOn = false;
                dotOn = true;
                foreach (Line ln in echoLine)
                    ln.Visibility = Visibility.Hidden;
                expandPoint.Visibility = Visibility.Hidden;
                foreach (Ellipse el in echos)
                    el.Visibility = Visibility.Hidden;
                foreach (Line ln in lines)
                    ln.Visibility = Visibility.Hidden;
                track.Visibility = Visibility.Visible;
            }
            else if (Keyboard.IsKeyDown(Key.Escape)) {
                File.WriteAllText(filePath,csv.ToString());
                eyeXHost.Dispose();
                Application.Current.Shutdown();
            }
        }

        private void sendRecieve() {
            sending = ((int)fastTrack.X).ToString() + "|" + ((int)fastTrack.Y).ToString();
            received = sending;
            //If user pressed Receiver or Cursor button but communication haven't started yet or has terminated, start a thread on tryCommunicateReceiver()
            if (ReceiverOn && communication_started_Receiver == false)
            {
                communication_started_Receiver = true;
                communicateThread_Receiver = new System.Threading.Thread(new ThreadStart(() => tryCommunicateReceiver(sending)));
                communicateThread_Receiver.Start();
            }

            //If user pressed Sender button but communication haven't started yet or has terminated, start a thread on tryCommunicateSender()
            if (SenderOn && communication_started_Sender == false)
            {
                communication_started_Sender = true;
                communicateThread_Sender = new System.Threading.Thread(new ThreadStart(() => tryCommunicateSender(sending)));
                communicateThread_Sender.Start();
            }
            if (received != null)
            {
                prevPoint.X = oFastTrack.X;
                prevPoint.Y = oFastTrack.Y;
                int p1, p2;
                int ind_1 = received.IndexOf("|");
                if (Int32.TryParse(received.Substring(0, ind_1), out p1))
                {
                    oFastTrack.X = p1;
                }
                if (Int32.TryParse(received.Substring(ind_1 + 1, received.Length - ind_1 - 1), out p2))
                {
                    oFastTrack.Y = p2;
                }
            }
        }

        private void lineSetup() {
            echoLine = new Line[lineLength];
            double opacity = 0;
            double thickness = .1;
            Brush br = new SolidColorBrush(System.Windows.Media.Colors.Black);
            for (int i = 0; i < lineLength; i++)
            {
                echoLine[i] = new Line();
                echoLine[i].StrokeThickness = thickness;
                echoLine[i].Stroke = br;
                echoLine[i].Opacity = opacity;
                echoLine[i].Visibility = Visibility.Hidden;
                canv.Children.Add(echoLine[i]);
                opacity += 1 / (double)lineLength;
                thickness += 7 / (double)lineLength;
            }
            expandPoint = new Ellipse();
            expandPoint.Fill = br;
            expandPoint.Width = 0;
            expandPoint.Height = 0;
            expandPoint.Visibility = Visibility.Hidden;
            canv.Children.Add(expandPoint);
            linePoints = new EchoPoints(lineLength);
        }

        private void lineVis()
        {
            Point temp = new Point(smoothTrack.X, smoothTrack.Y);
            smoothTrack.X = smoothTrack.X * smoothing + oFastTrack.X * (1 - smoothing);
            smoothTrack.Y = smoothTrack.Y * smoothing + oFastTrack.Y * (1 - smoothing);
            Point adjust = PointFromScreen(smoothTrack);
            adjust.X = adjust.X * xadjust + xoffset;
            adjust.Y = adjust.Y * yadjust + yoffset;
            linePush(adjust);
            if (distance(temp, smoothTrack) < 1.5 && distance(smoothTrack, oFastTrack) < 200)
            {
                expandCount++;
                expandPoint.Width = -20 / (1 + Math.Pow(Math.E, expandCount * .5 - 5)) + 20;
                expandPoint.Height = expandPoint.Width;
                Canvas.SetLeft(expandPoint, echoLine[linestart].X2 - expandPoint.Width / 2);
                Canvas.SetTop(expandPoint, echoLine[linestart].Y2 - expandPoint.Height / 2);
                if (expandPoint.Width > 5)
                {
                    linePush(adjust);
                    linePush(adjust);
                    linePush(adjust);
                    linePush(adjust);
                    smoothing = .97;
                }
            }
            else if(expandCount != 0)
            {
                expandCount = 0;
                expandPoint.Width = 0;
                expandPoint.Height = 0;
                smoothing = .85;
            }
            double thickness = .1;
            double opacity = .2;
            double opacityInc = 1 / (double)lineLength;
            double thicknessInc = 7 / (double)lineLength;
            int end = lineend + echoLine.Length;
            for (int i = lineend; i < end; i++)
            {
                int ind = i % echoLine.Length;
                echoLine[ind].StrokeThickness = thickness;
                echoLine[ind].Opacity = opacity;
                thickness += thicknessInc;
                opacity += opacityInc;
            }
        }

        private void linePush(Point p) {
            echoLine[lineend].X1 = echoLine[linestart].X2;
            echoLine[lineend].Y1 = echoLine[linestart].Y2;
            echoLine[lineend].X2 = p.X;
            echoLine[lineend].Y2 = p.Y;
            linestart = (linestart + 1) % echoLine.Length;
            if (linestart == lineend)
                lineend = (lineend + 1) % echoLine.Length;
        }

        ////OLD SLOW lineVis////
        //private void lineVis()
        //{
        //    Point temp = new Point(smoothTrack.X, smoothTrack.Y);
        //    smoothTrack.X = smoothTrack.X * smoothing + oFastTrack.X * (1 - smoothing);
        //    smoothTrack.Y = smoothTrack.Y * smoothing + oFastTrack.Y * (1 - smoothing);
        //    linePoints.push(PointFromScreen(smoothTrack));
        //    if (distance(temp, smoothTrack) < 5 && distance(smoothTrack, oFastTrack) < 200)
        //    {
        //        expandCount++;
        //        expandPoint.Width = -20 / (1 + Math.Pow(Math.E, expandCount * .5 - 5)) + 20;
        //        expandPoint.Height = expandPoint.Width;
        //        Canvas.SetLeft(expandPoint, linePoints.look(lineLength - 2).X - expandPoint.Width / 2);
        //        Canvas.SetTop(expandPoint, linePoints.look(lineLength - 2).Y - expandPoint.Height / 2);
        //        if (expandPoint.Width > 5)
        //        {
        //            linePoints.push(PointFromScreen(smoothTrack));
        //            linePoints.push(PointFromScreen(smoothTrack));
        //            linePoints.push(PointFromScreen(smoothTrack));
        //            linePoints.push(PointFromScreen(smoothTrack));
        //            smoothing = .99;
        //        }
        //    }
        //    else
        //    {
        //        expandCount = 0;
        //        expandPoint.Width = 0;
        //        expandPoint.Height = 0;
        //        smoothing = .90;
        //    }
        //    for (int i = 0; i < lineLength - 2; i++)
        //    {
        //        echoLine[i].X1 = linePoints.look(i).X;
        //        echoLine[i].Y1 = linePoints.look(i).Y;
        //        echoLine[i].X2 = linePoints.look(i + 1).X;
        //        echoLine[i].Y2 = linePoints.look(i + 1).Y;
        //    }
        //}

        private void echoSetup()
        {
            echos = new Ellipse[numEchos];
            lines = new Line[numEchos - 2];
            Brush br = new SolidColorBrush(System.Windows.Media.Colors.Black);
            for (int i = 0; i < numEchos; i++)
            {
                echos[i] = new Ellipse();
                echos[i].Width = 10;
                echos[i].Height = 10;
                echos[i].Fill = br;
                echos[i].Opacity = 0;
                echos[i].Name = "tr" + i.ToString();
                echos[i].Visibility = Visibility.Hidden;
                canv.Children.Add(echos[i]);
            }
            for (int i = 0; i < lines.Length; i++) {
                lines[i] = new Line();
                lines[i].StrokeThickness = 2;
                lines[i].Stroke = br;
                lines[i].Opacity = 0;
                lines[i].Visibility = Visibility.Hidden;
                canv.Children.Add(lines[i]);

            }
            echoPoints = new EchoPoints(numEchos);
        }

        private void echoVis() {
            if (distance(prevPoint, oFastTrack) < 10 && distance(block, oFastTrack) > 100)
                echoTick++;
            else
                echoTick = 0;

            if (distance(oFastTrack, block) < 50) {
                echos[numEchos - 2].Opacity = echos[numEchos - 2].Opacity + .01;
            }

            if (echoTick > echoInterval)
            {
                echoTick = 0;
                block.X = oFastTrack.X;
                block.Y = oFastTrack.Y;
                echoPoints.push(PointFromScreen(oFastTrack));
                for (int i = 0; i < numEchos - 1; i++) {
                    Canvas.SetLeft(echos[i], echoPoints.look(i).X);
                    Canvas.SetTop(echos[i], echoPoints.look(i).Y);
                    echos[i].Opacity = echos[i + 1].Opacity;
                }
                for (int i = 0; i < lines.Length; i++) {
                    lines[i].X1 = echoPoints.look(i).X + echos[i].Width/2;
                    lines[i].Y1 = echoPoints.look(i).Y + echos[i].Height/2;
                    lines[i].X2 = echoPoints.look(i + 1).X + echos[i+1].Width/2;
                    lines[i].Y2 = echoPoints.look(i + 1).Y + echos[i+1].Height/2;
                }
                echos[numEchos - 2].Opacity = .2;
            }
            double temp;
            for (int i = 0; i < numEchos; i++) {
                echos[i].Opacity = ((temp = echos[i].Opacity*.995) > 0) ? temp : 0;
                if(i < lines.Length)
                    lines[i].Opacity = echos[i].Opacity/2 + echos[i+1].Opacity/2;
            }
        }

        private class EchoPoints {
            Point[] points;
            int start;
            int end;

            public EchoPoints(int size) {
                points = new Point[size];
                start = 0;
                end = 0;
            }

            public void push(Point p) {
                points[end] = p;
                end = (end + 1) % points.Length;
                if (end == start)
                    start = (start + 1) % points.Length;
            }

            public Point look(int ind) {
                if((end + 1)%points.Length == start)
                    return points[(start + ind) % points.Length];
                return new Point(0, 0);
            }
        }

        private void trackDot() {
            Point fromScreen = PointFromScreen(oFastTrack);
            Canvas.SetLeft(track, Canvas.GetLeft(track)*.75 + .25*fromScreen.X);
            Canvas.SetTop(track, Canvas.GetTop(track)*.75 + .25*fromScreen.Y);
        }

        private void movement()
        {
            dog.X = Canvas.GetLeft(dogger);
            dog.Y = Canvas.GetTop(dogger);
            oldx = Canvas.GetLeft(dogger);
            oldy = Canvas.GetTop(dogger);
            if (target.X - dog.X != 0)
                targetAngle = Math.Atan2((target.Y - dog.Y), (target.X - dog.X)) * 180 / Math.PI + 90;
            else
                targetAngle = (target.Y > dog.Y) ? 0 : 180;
            if (targetAngle < 0)
                targetAngle += 360;
            if (Math.Abs(targetAngle - angle) < 180)
                angle = (angle * .75 + targetAngle * .25) % 360;
            else if (angle > 180)
                angle = (angle * .75 + (targetAngle + 360) * .25) % 360;
            else
                angle = (angle * .75 + (targetAngle - 360) * .25) % 360;
            if (angle < 0)
                angle += 360;
            if (Math.Abs(dog.X - target.X) > 3 || Math.Abs(dog.Y - target.Y) > 3)
            {
                double rad = degToRad(angle - 90);
                Canvas.SetLeft(dogger, dog.X += 3 * Math.Cos(rad));
                Canvas.SetTop(dogger, dog.Y += 3 * Math.Sin(rad));
            }
            dogger.RenderTransform = new RotateTransform(angle);
        }

        private void collision()
        {
            dog.X = Canvas.GetLeft(dogger);
            dog.Y = Canvas.GetTop(dogger);
            goal.X = Canvas.GetLeft(ElapsedTime);
            goal.Y = Canvas.GetTop(ElapsedTime);

            if (!finished && (dog.X > goal.X) && (dog.X < (goal.X + ElapsedTime.Width)) && (dog.Y > goal.Y) && (dog.Y < (goal.Y + ElapsedTime.Height)))
            {
                finished = true;
                elapsedTime = Convert.ToInt32(DateTimeOffset.Now.ToUnixTimeSeconds()) - startTime;
                FinalTime.Text = (Convert.ToInt32(elapsedTime) / 60).ToString("00") + ":" + (Convert.ToInt32(elapsedTime) % 60).ToString("00");
                puppy.Visibility = Visibility.Visible;
                FinalTime.Visibility = Visibility.Visible;
            }

            if ((oldx <= wallV && dog.X > wallV && !vleft) || (oldx >= wallV && dog.X < wallV && vleft)) {
                //Canvas.SetLeft(dogger, wallV);
                Canvas.SetLeft(dogger, 50);
                Canvas.SetTop(dogger, 50);
                target.X = 50;
                target.Y = 50;
                numDeaths++;
                startTime = startTime - 15;
            }
            else if ((oldy <= wallH && dog.Y > wallH && !habove) || (oldy >= wallH && dog.Y < wallH && habove)) {
                //Canvas.SetTop(dogger, wallH);
                Canvas.SetLeft(dogger, 50);
                Canvas.SetTop(dogger, 50);
                target.X = 50;
                target.Y = 50;
                numDeaths++;
                startTime = startTime - 15;
            }
        }

        private void wallSwitching() {
            Rectangle rect;
            double temp;
            dog.Y = Canvas.GetTop(dogger);
            dog.X = Canvas.GetLeft(dogger);
            double miny = 2000;
            double minx = 2000;
            bool hset = false;
            bool vset = false;
            foreach (UIElement child in canv.Children)
            {
                if (child.GetType().Equals(dogger.GetType()) && (rect = child as Rectangle).Name.Substring(0, 1).Equals("x")) {
                    //Setting wallH
                    if (dog.X > Canvas.GetLeft(rect) - dogger.Width && dog.X < Canvas.GetLeft(rect) + rect.Width) {
                        if (angle > 90 && angle < 270)
                        {
                            if ((temp = (Canvas.GetTop(rect) - dogger.Height + dogger.Width)) - dog.Y >= 0 && temp - dog.Y < miny)
                            {
                                wallH = temp;
                                habove = false;
                                miny = temp - dog.Y;
                                hset = true;
                            }
                        }
                        else {
                            if (dog.Y - (temp = (Canvas.GetTop(rect)) + rect.Height - dogger.Width) >= 0 && dog.Y - temp < miny)
                            {
                                wallH = temp;
                                habove = true;
                                miny = dog.Y - temp;
                                hset = true;
                            }
                        }
                    }
                    //Setting wallV
                    if (dog.Y > Canvas.GetTop(rect) - dogger.Height && dog.Y < Canvas.GetTop(rect) + rect.Height)
                    {
                        if (angle < 180)
                        {
                            if ((temp = Canvas.GetLeft(rect) -10) - dog.X >= 0 && temp - dog.X < minx)
                            {
                                wallV = temp;
                                vleft = false;
                                minx = temp - dog.X;
                                vset = true;
                            }
                        }
                        else
                        {
                            if (dog.X - (temp = (Canvas.GetLeft(rect) + rect.Width)) >= 0 && dog.X - temp < minx)
                            {
                                wallV = temp;
                                vleft = true;
                                minx = dog.X - temp;
                                vset = true;
                            }
                        }
                    }
                }
            }
            if (!hset)
                wallH = -10;
            if (!vset)
                wallV = -10;
        }

        private void Clickeroni_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                target.X = e.GetPosition(topLeft).X - dogger.Width / 2;
                target.Y = e.GetPosition(topLeft).Y - dogger.Height / 2;
            }
            else
            {
                Point adjust = PointFromScreen(fastTrack);
                adjust.X = adjust.X * xadjust + xoffset;
                adjust.Y = adjust.Y * yadjust + yoffset;
                double dist = distance(adjust, e.GetPosition(topLeft));
                if(dist < 50)
                    calTest.Fill = new SolidColorBrush(System.Windows.Media.Colors.CornflowerBlue);
                else if (dist < 90)
                    calTest.Fill = new SolidColorBrush(System.Windows.Media.Colors.Green);
                else
                    calTest.Fill = new SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }

        private double distance(Point a, Point b) {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        private double degToRad(double deg) {
            return deg * Math.PI / 180;
        }

        #region Sender/Receiver Methods
        public void tryCommunicateReceiver(String x)
        {
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            ReceiverIP = ipHostInfo.AddressList[0].ToString();

            while (ReceiverIP == "")
            {
                System.Threading.Thread.Sleep(1000);
            }
            AsynchronousSocketListener.StartListening();
        }
        public void tryCommunicateSender(String x)
        {
            while (SenderIP == "")
            {
                System.Threading.Thread.Sleep(1000);
            }
            SynchronousClient.StartClient(x); //start sending info
            communication_started_Sender = false;

            //AsynchronousSocketListener.StartListening();
        }

        public class StateObject
        {
            // Client  socket.
            public Socket workSocket = null;
            // Size of receive buffer.
            public const int BufferSize = 1024;
            // Receive buffer.
            public byte[] buffer = new byte[BufferSize];
            // Received data string.
            public StringBuilder sb = new StringBuilder();
        }

        //THis is the Receiver function (Asyncronous)
        // Citation: https://msdn.microsoft.com/en-us/library/fx6588te%28v=vs.110%29.aspx
        public class AsynchronousSocketListener
        {
            // Thread signal.
            public static ManualResetEvent allDone = new ManualResetEvent(false);
            public AsynchronousSocketListener()
            {
            }
            public static void StartListening()
            {
                if (ReceiverIP != "")
                {
                    // Data buffer for incoming data.
                    byte[] bytes = new Byte[1024];

                    // Establish the local endpoint for the socket.
                    // The DNS name of the computer
                    IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
                    IPAddress ipAddress = IPAddress.Parse(ReceiverIP);
                    IPEndPoint localEndPoint = new IPEndPoint(ipAddress, ReceiverPort);

                    // Create a TCP/IP socket.
                    Socket listener = new Socket(AddressFamily.InterNetwork,
                        SocketType.Stream, ProtocolType.Tcp);

                    // Bind the socket to the local endpoint and listen for incoming connections.
                    try
                    {
                        listener.Bind(localEndPoint);
                        listener.Listen(100);
                        //ommunication_received==false
                        while (true)
                        {
                            // Set the event to nonsignaled state.
                            allDone.Reset();

                            // Start an asynchronous socket to listen for connections.
                            //Console.WriteLine("Waiting for a connection...");
                            listener.BeginAccept(
                                new AsyncCallback(AcceptCallback),
                                listener);

                            allDone.WaitOne();

                            // Wait until a connection is made before continuing.
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                    //Console.WriteLine("\nPress ENTER to continue...");
                    //Console.Read();
                }
            }
            public static void AcceptCallback(IAsyncResult ar)
            {
                // Signal the main thread to continue.
                allDone.Set();

                // Get the socket that handles the client request.
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = handler;
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
            }
            public static void ReadCallback(IAsyncResult ar)
            {
                String content = String.Empty;

                // Retrieve the state object and the handler socket
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                // Read data from the client socket. 
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.
                    state.sb.Append(Encoding.ASCII.GetString(
                        state.buffer, 0, bytesRead));

                    // Check for end-of-file tag. If it is not there, read more data.
                    content = state.sb.ToString();
                    if (content.IndexOf("<EOF>") > -1)
                    {
                        // All the data has been read from the client. Display it on the console.
                        int x_start_ind = content.IndexOf("x: "), x_end_ind = content.IndexOf("xend ");
                        // int x_start_ind = content.IndexOf("x: "), x_end_ind = content.IndexOf("xend ");
                        // int y_start_ind = content.IndexOf("y: "), y_end_ind = content.IndexOf("yend ");

                        if (x_start_ind > -1 && x_end_ind > -1)
                        {
                            try
                            {
                                //convert the received string into x and y                                
                                // x_received = Convert.ToInt32(content.Substring(x_start_ind + 3, x_end_ind - (x_start_ind + 3)));
                                // y_received = Convert.ToInt32(content.Substring(y_start_ind + 3, y_end_ind - (y_start_ind + 3)));
                                string s = content.Substring(x_start_ind + 3, x_end_ind - (x_start_ind + 3));
                                //received_cards_arr = s.Split(',').Select(str => int.Parse(str)).ToArray(); ;
                                // received = Convert.ToInt32(content.Substring(x_start_ind + 3, x_end_ind - (x_start_ind + 3)));
                                received = s;
                            }
                            catch (FormatException)
                            {
                                Console.WriteLine("Input string is not a sequence of digits.");
                            }
                            catch (OverflowException)
                            {
                                Console.WriteLine("The number cannot fit in an Int32.");
                            }
                        }
                        // Show the data on the console.
                        //Console.WriteLine("x : {0}  y: {1}", x_received, y_received);

                        // Echo the data back to the client.
                        Send(handler, content);
                    }
                    else
                    {
                        // Not all data received. Get more.
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                    }
                }
            }

            private static void Send(Socket handler, String data)
            {
                // Convert the string data to byte data using ASCII encoding.
                byte[] byteData = Encoding.ASCII.GetBytes(data);

                // Begin sending the data to the remote device.
                handler.BeginSend(byteData, 0, byteData.Length, 0,
                    new AsyncCallback(SendCallback), handler);
            }

            private static void SendCallback(IAsyncResult ar)
            {
                try
                {
                    // Retrieve the socket from the state object.
                    Socket handler = (Socket)ar.AsyncState;

                    // Complete sending the data to the remote device.
                    int bytesSent = handler.EndSend(ar);
                    //Console.WriteLine("Sent {0} bytes to client.", bytesSent);x

                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        //This is the sending function (Syncronous)
        public class SynchronousClient
        {

            public static void StartClient(String x)
            {
                // Data buffer for incoming data.
                byte[] bytes = new byte[1024];

                // Connect to a remote device.
                try
                {
                    // Establish the remote endpoint for the socket.
                    // This example uses port 11000 on the local computer.
                    IPHostEntry ipHostInfo = Dns.GetHostByName(Dns.GetHostName());
                    IPAddress ipAddress = IPAddress.Parse(SenderIP);
                    IPEndPoint remoteEP = new IPEndPoint(ipAddress, SenderPort);

                    // Create a TCP/IP  socket.
                    Socket sender = new Socket(AddressFamily.InterNetwork,
                        SocketType.Stream, ProtocolType.Tcp);

                    // Connect the socket to the remote endpoint. Catch any errors.
                    try
                    {
                        sender.Connect(remoteEP);

                        Console.WriteLine("Socket connected to {0}",
                            sender.RemoteEndPoint.ToString());
                        //
                        string array_to_string = string.Join(",", x);
                        string message_being_sent = "x: " + x + "xend <EOF>";
                        //string message_being_sent = "x: " + x + "xend y: " + y + "yend cursorx: " +
                        //    System.Windows.Forms.Cursor.Position.X + "cursorxend cursory: " +
                        //    System.Windows.Forms.Cursor.Position.Y + "cursoryend <EOF>";
                        // Encode the data string into a byte array.
                        byte[] msg = Encoding.ASCII.GetBytes(message_being_sent);

                        // Send the data through the socket.
                        int bytesSent = sender.Send(msg);

                        // Receive the response from the remote device.
                        int bytesRec = sender.Receive(bytes);
                        Console.WriteLine("Echoed test = {0}",
                            Encoding.ASCII.GetString(bytes, 0, bytesRec));

                        // Release the socket.
                        sender.Shutdown(SocketShutdown.Both);
                        sender.Close();

                    }
                    catch (ArgumentNullException ane)
                    {
                        Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                    }
                    catch (SocketException se)
                    {
                        Console.WriteLine("SocketException : {0}", se.ToString());
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unexpected exception : {0}", e.ToString());
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            public static string data = null;


        }
        #endregion
    }
}
