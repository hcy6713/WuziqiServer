using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WuziqiServer
{
    class Message
    {
        public static string Match(byte nameID, byte commandID, Socket from)
        {
            string mess = "";
            switch (nameID)
            {
                case 0xBB:
                    mess += "黑";
                    break;
                case 0xCC:
                    mess += "白:";
                    break;
                default:
                    // 发送0x77命令，数据包内容有误
                    from.Send(Protocol.BuildDataPackage(0x77));
                    return null;
            }
            switch (commandID)
            {
                case 0xAA:
                    mess += "0xAA";
                    // 认输
                    MainWindow.result = MainWindow.RES.LOSE;
                    break;
                default:
                    // 发送0x77命令，数据包内容有误
                    from.Send(Protocol.BuildDataPackage(0x77));
                    return null;
            }
            return mess;
        }

        public static string Match(byte nameID, byte commandID, string[] mess, Socket from)
        {
            string Full_Mess = "";
            Socket opponent = null;
            int chessColor = 0;
            switch (nameID)
            {
                case 0xBB:
                    Full_Mess += "黑";
                    chessColor = 1;
                    opponent = MainWindow.ClientWhite;
                    break;
                case 0xCC:
                    Full_Mess += "白:";
                    chessColor = 2;
                    opponent = MainWindow.ClientBlack;
                    break;
                default:
                    // 发送0x77命令，数据包内容有误
                    from.Send(Protocol.BuildDataPackage(0x77));
                    return null;
            }
            switch (commandID)
            {
                case 0x44:
                    Full_Mess += "0x44";
                    // 检查落子位置是否能够落子
                    Boolean IsLegal = false;
                    try
                    {
                        IsLegal = Board.IsLegal(mess);
                    }
                    catch(Exception ee)
                    {
                        if (ee.Message.Equals("ILLEGAL") || ee.Message.Equals("OUT"))
                        {
                            // 发送0x77命令，数据包内容有误
                            from.Send(Protocol.BuildDataPackage(0x77));
                            return null;
                        }else if (ee.Message.Equals("EXIST"))
                        {
                            from.Send(Protocol.BuildDataPackage(0x55));
                            return null;
                        }
                    }
                    // 绘棋
                    MainWindow.DrawChess(int.Parse(mess[0]), int.Parse(mess[1]));
                    Full_Mess += " " + mess[0] + " " + mess[1];
                    // 检查是否胜利
                    if (Board.IsWin(chessColor))
                    {
                        MainWindow.result = MainWindow.RES.WIN;
                    }
                    // 转发落子位置
                    else if (opponent.Connected)
                    {
                        opponent.Send(Protocol.BuildDataPackage(commandID, mess));
                        // 未结束时更改下棋方
                        MainWindow.Turn = (MainWindow.Turn == (byte)0xBB) ? (byte)0xCC : (byte)0xBB;
                    }
                    break;
                default:
                    // 发送0x77命令，数据包内容有误
                    from.Send(Protocol.BuildDataPackage(0x77));
                    return null;
            }
            return Full_Mess;
        }
    }
}
