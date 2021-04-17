using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WuziqiServer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        // 服务器网络终结点
        private IPEndPoint myEndPoint = null;
        // 创建一个和客服端通信的套接字
        private Socket serverSock = null;
        // 存放客服端ip地址的list
        private List<string> ipList = new List<string> { };

        // 记录两个客户端连接
        public static Socket ClientBlack = null;
        public static Socket ClientWhite = null;
        
        // 分别接收客户端的信息线程
        private Thread ThreadBlack = null;
        private Thread ThreadWhite = null;
        private Thread threadwatch = null;
        private Thread gameThread = null;
        
        // 记录游戏状态
        private enum GameStatus { WAIT, RUN, END };
        private static GameStatus gameStatus = GameStatus.WAIT;
        // 落子后是否胜利
        public enum RES{ WIN, LOSE, UNCERTAIN };
        public static RES result = RES.UNCERTAIN;
        // 记录轮到哪方行动
        public static byte Turn;
        
        // 记录最近一次发送数据包的时间
        public static DateTime LastTime;

        // 棋子半径
        private static double R = 15;
        // 颜色
        private static Brush BlackBrush = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
        private static Brush WhiteBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        private static Brush halfBlack = new SolidColorBrush(Color.FromArgb(127, 0, 0, 0));
        private static Brush halfWhite = new SolidColorBrush(Color.FromArgb(127, 255, 255, 255));

        // 记录棋盘上的棋子状态，0：无棋；1：黑棋；2：白棋
        public static int[,] ChessStatus = new int[15, 15];
        private static Canvas Board = null;
        
        
        public MainWindow()
        {
            InitializeComponent();
            startButton.IsEnabled = true;
            Board = this.chessBoard;
            for (int i = 0; i <= 14; i++)
            {
                for (int j = 0; j <= 14; j++)
                {
                    ChessStatus[i, j] = 0;
                }
            }
        }
        

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            Int32 port = Int32.Parse(PortTextBox.Text);
            IPAddress iPAddress = IPAddress.Parse(GetLocalIP());
            myEndPoint = new IPEndPoint(iPAddress, port);
            // 定义一个套接字用于监听客户端发来的消息，包含三个参数（IP4寻址协议，流式连接，Tcp协议） 
            serverSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // 监听绑定的网络节点
            serverSock.Bind(myEndPoint);
            //将套接字的监听队列长度限制为2  
            serverSock.Listen(1);
            // 负责监听客户端的线程:创建一个监听线程  
            threadwatch = new Thread(WatchConnecting);
            //将窗体线程设置为与后台同步，随着主线程结束而结束  
            threadwatch.IsBackground = true;
            //启动线程     
            threadwatch.Start();
            BoxShow("监听中...");
            startButton.IsEnabled = false;
        }
        

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            
        }
        
       
        private void WatchConnecting()
        {
            Socket connection = null;
            // 持续不断监听客户端发来的请求     
            while (true)
            {
                try
                {
                    connection = serverSock.Accept();
                }
                catch (Exception ex)
                {   
                    // 提示套接字监听异常     
                    MessageBox.Show(ex.Message);
                    break;
                }
                // 客户端网络结点号  
                string remoteEndPoint = connection.RemoteEndPoint.ToString();
                
                // ClientBlack没有处于连接状态
                if(!(ClientBlack != null && ClientBlack.Connected))
                {
                    ClientBlack = connection;
                    ipList.Add(remoteEndPoint);
                    // 建立通信
                    ThreadBlack = new Thread(ReceiveMessage);
                    ThreadBlack.Start(ClientBlack);
                    BoxShow("客户端数量：" + ipList.Count);
                }
                // ClientBlack连接，ClientWhite未连接
                else if(!(ClientWhite != null && ClientWhite.Connected))
                {
                    ClientWhite = connection;
                    ipList.Add(remoteEndPoint);
                    // 建立通信
                    ThreadWhite = new Thread(ReceiveMessage);
                    ThreadWhite.Start(ClientWhite);
                    BoxShow("客户端数量：" + ipList.Count);
                    // 开始游戏
                    gameThread = new Thread(game);
                    // 通知执棋颜色
                    ClientWhite.Send(Protocol.BuildDataPackage(0x22, new string[] { "White" }));
                    ClientBlack.Send(Protocol.BuildDataPackage(0x22, new string[] { "Black" }));
                    gameThread.IsBackground = true;
                    gameThread.Start();
                }
                // ClientBlack,ClientWhite都连接
                else
                {
                    // 发送服务器正忙
                    connection.Send(Protocol.BuildDataPackage(0x11));
                }
            }
        }
        

        private void game()
        {
            while (true)
            {
                // 检查是否满足游戏开始条件
                if (gameStatus == GameStatus.WAIT)
                {
                    gameStatus = GameStatus.RUN;
                    this.blackIp.Dispatcher.Invoke(
                        new Action(
                            delegate
                            {
                                blackIp.Text = ipList[0];
                                whiteIp.Text = ipList[1];
                            }
                        )
                    );
                    // 生成0~2(不含)之间的随机整数，为1时，白棋先；为0时，黑棋先
                    Random rd = new Random();
                    int rand = rd.Next(0, 2);
                    if (rand == 1)
                    {
                        ClientWhite.Send(Protocol.BuildDataPackage(0x33));
                        Turn = 0xCC;
                    }
                    else if(rand == 0)
                    {
                        ClientBlack.Send(Protocol.BuildDataPackage(0x33));
                        Turn = 0xBB;
                    }
                }

                if (gameStatus == GameStatus.RUN)
                {
                    // 某一个客户端断开连接，认定断开的客户端认输
                    if (ipList.Count == 1 && gameStatus == GameStatus.RUN)
                    {
                        // 更改游戏状态
                        gameStatus = GameStatus.END;
                        // 给未断开的一方发送信息
                        if (ClientWhite.Connected)
                        {
                            ClientWhite.Send(Protocol.BuildDataPackage(0x88));
                        }
                        if (ClientBlack.Connected)
                        {
                            ClientBlack.Send(Protocol.BuildDataPackage(0x88));
                        }
                    }

                    // 30s内未响应
                    if ((DateTime.Now - LastTime).TotalSeconds > 30)
                    {
                        gameStatus = GameStatus.END;
                        if (Turn == 0xBB)
                        {
                            if (ClientBlack.Connected)
                            {
                                ClientBlack.Send(Protocol.BuildDataPackage(0x99));
                            }
                            if (ClientWhite.Connected)
                            {
                                ClientWhite.Send(Protocol.BuildDataPackage(0x88));
                            }
                        }else if(Turn == 0xCC)
                        {
                            if (ClientBlack.Connected)
                            {
                                ClientBlack.Send(Protocol.BuildDataPackage(0x88));
                            }
                            if (ClientWhite.Connected)
                            {
                                ClientWhite.Send(Protocol.BuildDataPackage(0x99));
                            }
                        }

                        BoxShow("游戏结束");
                    }
                    // 判断是否胜利
                    if (result == RES.WIN)
                    {
                        gameStatus = GameStatus.END;
                        if (Turn == 0xBB)
                        {
                            if (ClientBlack.Connected)
                            {
                                ClientBlack.Send(Protocol.BuildDataPackage(0x88));
                            }
                            if (ClientWhite.Connected)
                            {
                                ClientWhite.Send(Protocol.BuildDataPackage(0x99));
                            }
                        }
                        else if (Turn == 0xCC)
                        {
                            if (ClientBlack.Connected)
                            {
                                ClientBlack.Send(Protocol.BuildDataPackage(0x99));
                            }
                            if (ClientWhite.Connected)
                            {
                                ClientWhite.Send(Protocol.BuildDataPackage(0x88));
                            }
                        }
                        BoxShow("游戏结束");
                    }
                    if (result == RES.LOSE)
                    {
                        gameStatus = GameStatus.END;
                        if (Turn == 0xBB)
                        {
                            if (ClientBlack.Connected)
                            {
                                ClientBlack.Send(Protocol.BuildDataPackage(0x99));
                            }
                            if (ClientWhite.Connected)
                            {
                                ClientWhite.Send(Protocol.BuildDataPackage(0x88));
                            }
                        }
                        else if (Turn == 0xCC)
                        {
                            if (ClientBlack.Connected)
                            {
                                ClientBlack.Send(Protocol.BuildDataPackage(0x88));
                            }
                            if (ClientWhite.Connected)
                            {
                                ClientWhite.Send(Protocol.BuildDataPackage(0x99));
                            }
                        }
                        BoxShow("游戏结束");
                    }
                }
            }
            
        }


        private void ReceiveMessage(object socketclientpara)
        {
            Socket socketFrom = socketclientpara as Socket;
            string IPFrom = socketFrom.RemoteEndPoint.ToString();
            while (true)
            {
                // 创建一个内存缓冲区，其大小为1024*1024字节  即1M     
                byte[] arrServerRecMsg = new byte[1024 * 1024];
                try
                {
                    // 将接收到的信息存入到内存缓冲区，并返回其字节数组的长度    
                    int length = socketFrom.Receive(arrServerRecMsg);
                    // 如果收到信息
                    if (length != 0 )
                    {
                        // 游戏尚未开始
                        if(gameStatus == GameStatus.WAIT)
                        {

                        }
                        if (gameStatus == GameStatus.RUN && result == RES.UNCERTAIN)
                        {
                            byte[] data = new byte[length];
                            Array.Copy(arrServerRecMsg, 0, data, 0, length);
                            string message = Protocol.UnpackDataPackage(data, socketFrom);
                            if (message != null)
                            {
                                BoxShow(message);
                            }
                        }
                    }
                }
                catch (Exception ee)
                {
                    // 如果断开连接
                    if (!socketFrom.Connected)
                    {
                        ipList.Remove(IPFrom);
                        BoxShow("客户端数量：" + ipList.Count);
                    }
                    MessageBox.Show(ee.Message);
                    break;
                }
            }
        }


        public static void DrawChess(int x, int y)
        {
            Brush brush = (Turn == (byte)0xBB) ? BlackBrush : WhiteBrush;          
            MainWindow.Board.Dispatcher.Invoke(
                new Action(
                    delegate
                    {
                        Ellipse result = new Ellipse();
                        result.Fill = brush;
                        result.Width = R * 2;
                        result.Height = R * 2;
                        Board.Children.Add(result);
                        Canvas.SetLeft(result, CalPos(x));
                        Canvas.SetTop(result, CalPos(y));
                        result.Visibility = Visibility.Visible;
                        ChessStatus[x, y] = (Turn == (byte)0xBB) ? 1 : 2;
                        //chess.Add(x + "," + y, result);
                    }));
        }
        

        public static double CalPos(int i)
        {
            double pos = 23.0 + 35.0 * i - R;
            return pos;
        }
        

        private void closeThread()
        {
            try
            {
                if (gameThread != null)
                {
                    gameThread.Abort();
                    gameThread = null;
                }
                if (ThreadBlack != null)
                {
                    ThreadBlack.Abort();
                    ThreadBlack = null;
                }
                if (ThreadWhite != null)
                {
                    ThreadWhite.Abort();
                    ThreadWhite = null;
                }
            }
            catch (Exception ee)
            {
                Thread.ResetAbort();
            }
        }


        // 获取本机ipv4地址
        private string GetLocalIP()
        {
            string AddressIP = string.Empty;
            foreach (IPAddress _IPAddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (_IPAddress.AddressFamily.ToString() == "InterNetwork")
                {
                    AddressIP = _IPAddress.ToString();
                }
            }
            return AddressIP;
        }


        // 消息框显示信息
        private void BoxShow(String str)
        {
            this.DataText.Dispatcher.Invoke(
                    new Action(
                        delegate
                        {
                            DataText.Text += str + "\n";
                            DataText.ScrollToEnd();
                        }
                    )
                );
        }

       
    }
}
