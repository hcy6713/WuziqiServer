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
        // 定义一个集合，存储客服端信息
        private Dictionary<string, Socket> ClientConnectionItems = new Dictionary<string, Socket> { };
        // 存放客服端ip地址的list
        private List<string> iplist = new List<string> { };
        // 记录游戏状态
        private enum GameStatus{WAIT,RUN,END};
        private GameStatus gameStatus = GameStatus.WAIT;
        // 分别接收客户端的信息线程
        private Thread ThreadBlack = null;
        private Thread ThreadWhite = null;
        private Thread threadwatch = null;
        private Thread gameThread = null;
        // 记录两个客户端连接
        Socket ClientBlack = null;
        Socket ClientWhite = null;
        // 记录棋盘上的棋子状态，0：无棋；1：黑棋；2：白棋
        private int[,] ChessStatus = new int[15,15];
        private Dictionary<string, Ellipse> chess = new Dictionary<string, Ellipse> { };
        // 记录轮到哪方行动
        private string Turn = null;
        // 记录哪方发来信息
        private string ClientTurn = null;
        // 记录接收到信息后该方的输赢状态
        private enum RES{WIN,LOSE,UNCERTAIN};
        private RES blackRes = RES.UNCERTAIN;
        private RES whiteRes = RES.UNCERTAIN;
        // 棋子半径
        double R = 15;
        // 颜色
        Brush BlackBrush = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
        Brush WhiteBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        Brush halfBlack = new SolidColorBrush(Color.FromArgb(127, 0, 0, 0));
        Brush halfWhite = new SolidColorBrush(Color.FromArgb(127, 255, 255, 255));
        // 记录时间信息
        DateTime last;
      

        public MainWindow()
        {
            InitializeComponent();
            startButton.IsEnabled = true;
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

        // 监听客户端发来的请求  
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
                if(ClientConnectionItems.Count < 2)
                {
                    // 添加客户端信息
                    ClientConnectionItems.Add(remoteEndPoint, connection);
                    // 将客服端ip放入ipList中
                    iplist.Add(remoteEndPoint);
                    BoxShow("客户端数量：" + ClientConnectionItems.Count);
                }
                else
                {
                    SendMessage("WAIT", connection);
                }
                if(ClientConnectionItems.Count == 2)
                {
                    // 创建一个游戏线程      
                    gameThread = new Thread(game);
                    // 启动线程     
                    gameThread.Start();
                }
            }
        }

        private void game()
        {
            while (true)
            {
                // 检查是否满足游戏开始条件
                if (iplist.Count == 2 && gameStatus == GameStatus.WAIT)
                {
                    gameStatus = GameStatus.RUN;
                    // 创建通信线程      
                    ThreadBlack = new Thread(ReceiveMessage);
                    ThreadWhite = new Thread(ReceiveMessage);
                    // 取得ip对应的socket连接
                    ClientConnectionItems.TryGetValue(iplist[0], out ClientBlack);
                    ClientConnectionItems.TryGetValue(iplist[1], out ClientWhite);
                    

                    this.blackIp.Dispatcher.Invoke(
                        new Action(
                            delegate
                            {
                                blackIp.Text = iplist[0];
                                whiteIp.Text = iplist[1];
                            }
                        )
                    );
                    // 生成0~2(不含)之间的随机整数，为1时，白棋先；为0时，黑棋先
                    Random rd = new Random();
                    int rand = rd.Next(0, 2);
                    ThreadBlack.Start(ClientBlack);
                    ThreadWhite.Start(ClientWhite);
                    if (rand == 1)
                    {
                        SendMessage("BEGIN", ClientWhite);
                        Turn = "白色方";
                    }
                    else if(rand == 0)
                    {
                        SendMessage("BEGIN", ClientBlack);
                        Turn = "黑色方";
                    }
                    last = DateTime.Now;
                }
                // 某一个客户端断开连接且游戏在进行中，认定断开的客户端认输
                else if(ClientConnectionItems.Count == 1 && gameStatus == GameStatus.RUN)
                {
                    // 更改游戏状态
                    gameStatus = GameStatus.END;
                    foreach (var socketTemp in ClientConnectionItems)
                    {
                        SendMessage("WIN", socketTemp.Value);
                    }
                    closeThread();
                }
                if(gameStatus == GameStatus.RUN && (DateTime.Now - last).TotalSeconds > 30)
                {
                    gameStatus = GameStatus.END;
                    BoxShow("游戏结束");
                    if (Turn.Equals("黑色方"))
                    {
                        blackRes = RES.LOSE;
                        whiteRes = RES.WIN;
                        SendMessage("TIME OUT", ClientBlack);
                        SendMessage("WIN", ClientWhite);
                    }
                    else if (Turn.Equals("白色方"))
                    {
                        blackRes = RES.WIN;
                        whiteRes = RES.LOSE;
                        SendMessage("WIN", ClientBlack);
                        SendMessage("TIME OUT", ClientWhite);
                    }
                    closeThread();
                }
            }
            
        }

        private void ReceiveMessage(object socketclientpara)
        {
            Socket socketServer = socketclientpara as Socket;
            string ClientIP = socketServer.RemoteEndPoint.ToString();
            while (true)
            {
                // 创建一个内存缓冲区，其大小为1024*1024字节  即1M     
                byte[] arrServerRecMsg = new byte[1024 * 1024];
                // 将接收到的信息存入到内存缓冲区，并返回其字节数组的长度    
                try
                {
                    int length = socketServer.Receive(arrServerRecMsg);
                    // 将机器接受到的字节数组转换为人可以读懂的字符串     
                    string message = Encoding.UTF8.GetString(arrServerRecMsg, 0, length);
                    // 如果收到信息
                    if (length != 0 && gameStatus == GameStatus.RUN)
                    {
                        // 判断信息由哪方发来
                        if (ClientIP.Equals(iplist[0]))
                        {
                            ClientTurn = "黑色方";
                        }
                        else if (ClientIP.Equals(iplist[1]))
                        {
                            ClientTurn = "白色方";
                        }
                        // 如果轮到信息发送方下棋
                        if (ClientTurn.Equals(Turn))
                        {
                            // 将last更新为最新收到行动方消息的时间
                            last = DateTime.Now;
                            string[] messages = message.Split(',');
                            // 如果接收到的信息匹配成功
                            if(MatchMess(messages, ClientTurn, socketServer))
                            {
                                if (blackRes == RES.WIN || whiteRes == RES.WIN)
                                {
                                    SendWinAndFail();
                                    gameStatus = GameStatus.END;
                                    BoxShow("游戏结束！");
                                    closeThread();
                                    // socketServer.Close();
                                    break;
                                }
                                else
                                {
                                    SendMessage(message);
                                }
                            }  
                        }
                        else if (!ClientTurn.Equals(Turn))
                        {
                            if (ClientIP == iplist[0])
                            {
                                SendMessage("对方回合", ClientBlack);
                            }
                            else if (ClientIP == iplist[1])
                            {
                                SendMessage("对方回合", ClientWhite);
                            }
                        }
                    }
                }
                catch (Exception ee)
                {
                    // 清除对应的记录
                    ClientConnectionItems.Remove(ClientIP);
                    iplist.Remove(ClientIP);
                    // 提示套接字监听异常
                    BoxShow("客户端数量：" + ClientConnectionItems.Count);
                    MessageBox.Show(ee.ToString());
                    // 关闭之前accept出来的和客户端进行通信的套接字 
                    // socketServer.Close();
                    break;
                }
            }
        }

        // 匹配信息功能
        private Boolean MatchMess(String[] mess, string from, Socket sock)
        {
            // 信息是落子位置信息
            if (mess[0].Equals("position"))
            {
                int status = 0;
                Brush chessBrush = null;
                if (from.Equals("黑色方"))
                {
                    chessBrush = BlackBrush;
                    status = 1;
                }
                else if(from.Equals("白色方"))
                {
                    chessBrush = WhiteBrush;
                    status = 2;
                }
                // 判断信息中的位置是否合法
                if (IsLegal(mess))
                {
                    int x = int.Parse(mess[1]);
                    int y = int.Parse(mess[2]);
                    // 判断是否有棋子
                    if (!IsExist(x, y))
                    {
                        BoxShow(DateTime.Now.ToString("HH:mm:ss") + Turn + ":[" + mess[1] + "," + mess[2] + "]");
                        DrawPiece(chessBrush, x, y);
                        // 修改棋盘上棋子状态
                        ChessStatus[x, y] = status;
                        // 判断落棋方是否胜利
                        if (ExamHorizon(status) || ExamVertical(status) || ExamTitled(status))
                        {
                            if(status == 1)
                            {
                                blackRes = RES.WIN;
                                whiteRes = RES.LOSE;
                            }
                            else if(status == 2)
                            {
                                blackRes = RES.LOSE;
                                whiteRes = RES.WIN;
                            }
                        }
                        return true;
                    }
                    else
                    {
                        SendMessage("EXIST", sock);
                        return false;
                    }
                }
                else
                {
                    SendMessage("ILLEGAL", sock);
                    return false;
                }
            }
            else if (mess[0].Equals("SURRENDER"))
            {
                BoxShow(DateTime.Now.ToString("HH:mm:ss") + Turn + ":" + mess[0]);
                if (from.Equals("黑色方"))
                {
                    blackRes = RES.LOSE;
                    whiteRes = RES.WIN;
                }
                else if (from.Equals("白色方"))
                {
                    blackRes = RES.WIN;
                    whiteRes = RES.LOSE;
                }
                return true;
            }
            SendMessage("ILLEHAL", sock);
            return false;
        }

        // 画棋子
        private void DrawPiece(Brush brush, int x, int y)
        {
            this.chessBoard.Dispatcher.Invoke(
                new Action(
                    delegate
                    {
                        Ellipse result = new Ellipse();
                        result.Fill = brush;
                        result.Width = R * 2;
                        result.Height = R * 2;
                        chessBoard.Children.Add(result);
                        Canvas.SetLeft(result, CalPos(x));
                        Canvas.SetTop(result, CalPos(y));
                        result.Visibility = Visibility.Visible;
                        chess.Add(x + "," + y, result);
                    }));
        }

        // 给sock发送message
        private void SendMessage(String message, Socket sock)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                List<byte> list = new List<byte>();
                list.Add(0);                                                                       //将0添加到list的首部，用于之后的判断
                list.AddRange(buffer);
                //将泛型集合转换为数组
                byte[] newBuffer = list.ToArray();
                sock.Send(newBuffer);
            }
            catch(Exception)
            {
                MessageBox.Show("发送失败");
            }
            
        }

        // 根据blackRes和whiteRes发送WIN和FAIL指令
        private void SendWinAndFail()
        {
            if (blackRes == RES.WIN)
            {
                SendMessage("WIN", ClientBlack);
                SendMessage("FAIL", ClientWhite);
            }
            else if (whiteRes == RES.WIN)
            {
                SendMessage("WIN", ClientWhite);
                SendMessage("FAIL", ClientBlack);
            }
        }

        // 根据Turn发送其他信息
        private void SendMessage(string message)
        {
            string temp = null;
            if (Turn.Equals("黑色方"))
            {
                SendMessage(message, ClientWhite);
                temp = "白色方";
            }
            else if (Turn.Equals("白色方"))
            {
                SendMessage(message, ClientBlack);
                temp = "黑色方";
            }
            Turn = temp;
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
        
        // 计算棋子的位置
        private double CalPos(int i)
        {
            double pos = 23.0 + 35.0 * i - R;
            return pos;
        }

        // 检查该位置是否有棋子
        private Boolean IsExist(int x, int y)
        {
            if(ChessStatus[x, y] != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // 检查发送的位置信息是否合法
        private Boolean IsLegal(string[] mess)
        {
            if(mess.Length != 3)
            {
                return false;
            }
            if(mess[1] == null || mess[2] == null)
            {
                return false;
            }
            if(int.Parse(mess[1]) < 0 || int.Parse(mess[1]) > 14)
            {
                return false;
            }
            if (int.Parse(mess[2]) < 0 || int.Parse(mess[2]) > 14)
            {
                return false;
            }
            return true;
        }

        // 水平方向检查五子连心
        private Boolean ExamHorizon(int temp)
        {
            for(int i =0; i <= 10; i++)
            {
                for(int j = 0; j < 15; j++)
                {
                    if(ChessStatus[i, j] == temp)
                    {
                        int nextx = i + 1;
                        while (ChessStatus[nextx, j] == temp)
                        {
                            nextx++;
                            if (nextx - i == 5) return true;
                        }
                    }
                }
            }
            return false;
        }

        // 竖直方向检查五子连心
        private Boolean ExamVertical(int temp)
        {
            for (int i = 0; i < 15; i++)
            {
                for (int j = 0; j <= 10; j++)
                {
                    if (ChessStatus[i, j] == temp)
                    {
                        int nexty = j + 1;
                        while (ChessStatus[i, nexty] == temp)
                        {
                            nexty++;
                            if (nexty - j == 5) return true;
                        }
                    }
                }
            }
            return false;
        }

        // 斜下方向检查五子连心
        private Boolean ExamTitled(int temp)
        {
            for (int i = 0; i <= 10; i++)
            {
                for (int j = 0; j <= 10; j++)
                {
                    if (ChessStatus[i, j] == temp)
                    {
                        int nextx = i + 1;
                        int nexty = j + 1;
                        while (ChessStatus[nextx, nexty] == temp)
                        {
                            nextx++;
                            nexty++;
                            if (nextx - i == 5) return true;
                        }
                    }
                }
            }
            return false;
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
            catch(Exception ee)
            {
                Thread.ResetAbort();
            }
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (gameStatus == GameStatus.END)
                {
                    closeThread();
                    ClientConnectionItems.Clear();
                    iplist.Clear();
                    Turn = null;
                    gameStatus = GameStatus.WAIT;
                    ClientBlack = null;
                    ClientWhite = null;
                    ClientTurn = null;
                    blackRes = RES.UNCERTAIN;
                    whiteRes = RES.UNCERTAIN;
                    Ellipse temp = null;
                    for (int i = 0; i < 15; i++)
                    {
                        for (int j = 0; j < 15; j++)
                        {
                            if(ChessStatus[i, j] != 0)
                            {
                                chess.TryGetValue(i + "," + j, out temp);
                                temp.Visibility = Visibility.Hidden;
                                ChessStatus[i, j] = 0;
                            }
                        }
                    }
                    chess.Clear();
                    blackIp.Text = null;
                    whiteIp.Text = null;
                    DataText.Text = null;
                    BoxShow("监听中......");
                }
            }
            catch(Exception ee)
            {
                MessageBox.Show(ee.ToString());
            }
        }
    }
}
