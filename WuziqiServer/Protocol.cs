using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WuziqiServer
{
    // 根据协议构建、拆解、检查数据包
    class Protocol
    {
        const byte HEADER = 0xA5;           //帧头
        const byte FOOTER = 0x5A;           //帧尾
        const byte nameID = 0xAA;           //信息来自服务器
        

        public static byte[] BuildDataPackage(byte commandID)
        {
            Int32 checkCode = GetCheckCode(nameID, commandID); 
            //转换为字节数组（非字符型数据需先转换为网络序  HostToNetworkOrder:主机序转网络序）
            byte[] checkCodeByte = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(checkCode));

            byte[] totalByte = new byte[8];
            totalByte[0] = HEADER;
            totalByte[1] = nameID;
            totalByte[2] = commandID;
            checkCodeByte.CopyTo(totalByte, 3);
            totalByte[7] = FOOTER;
            // 当命令不是通知"不是你的回合"时更新时间信息
            if(commandID != 0x66)
            {
                MainWindow.LastTime = DateTime.Now;
            }
            return totalByte;
        }

        public static byte[] BuildDataPackage(byte commandID, string[] mess)
        {

            Int32 messLength = mess.Length * 4;
            for(int i = 0; i < mess.Length; i++)
            {
                messLength += mess[i].Length;
            }

            Int32 checkCode = GetCheckCode(nameID, commandID, messLength);
            byte[] messLengthByte = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messLength));
            byte[] checkCodeByte = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(checkCode));

            //将消息转为字节数组
            byte[] messByte = new byte[messLength];
            int copyIndex = 0;
            for (int i = 0; i < mess.Length; i++)
            {
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder(mess[i].Length)).CopyTo(messByte, copyIndex);
                copyIndex += 4;
                byte[] temp = Encoding.UTF8.GetBytes(mess[i]);
                temp.CopyTo(messByte, copyIndex);
                copyIndex += temp.Length;
            }

            byte[] totalByte = new byte[12 + messLength];
            totalByte[0] = HEADER;
            totalByte[1] = nameID;
            totalByte[2] = commandID;
            messLengthByte.CopyTo(totalByte, 3);
            messByte.CopyTo(totalByte, 7);
            checkCodeByte.CopyTo(totalByte, 7 + messLength);
            totalByte[11 + messLength] = FOOTER;
            MainWindow.LastTime = DateTime.Now;
            return totalByte;
        }

        public static string UnpackDataPackage(byte[] data, Socket from)
        {
            int dataLen = data.Length;
            if (dataLen == 8)
            {
                // 拆解数据包
                byte header = data[0];
                byte nameID = data[1];
                byte commandID = data[2];
                byte[] checkCodeByte = new byte[4];
                Array.Copy(data, 3, checkCodeByte, 0, 4);
                Int32 checkCode = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(checkCodeByte, 0));
                byte footer = data[7];
                // 数据包正常
                if (checkCode == GetCheckCode(nameID, commandID) && header == HEADER && footer == FOOTER)
                {
                    if (!CheckName(nameID))
                    {
                        from.Send(BuildDataPackage(0x66));
                        return null;
                    }
                    return Message.Match(nameID, commandID, from);
                }
                else
                {
                    // 数据包格式有误
                    if (from.Connected)
                    {
                        from.Send(BuildDataPackage(0xBB));
                    }
                    return null;
                }
            }
            else
            {
                // 拆解数据包
                byte header = data[0];
                byte nameID = data[1];
                byte commandID = data[2];
                byte[] messLenBytes = new byte[4];
                Array.Copy(data, 3, messLenBytes, 0, 4);
                Int32 messLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(messLenBytes, 0));
                byte[] messByte = new byte[messLen];
                Array.Copy(data, 7, messByte, 0, messLen);
                int index = 0;
                List<string> mess_list = new List<string> { };
                while (index < messLen)
                {
                    byte[] len_i_Byte = new byte[4];
                    Array.Copy(messByte, index, len_i_Byte, 0, 4);
                    int len_i = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(len_i_Byte, 0));
                    index += 4;
                    byte[] mess_i_Byte = new byte[len_i];
                    Array.Copy(messByte, index, mess_i_Byte, 0, len_i);
                    string mess_i = Encoding.UTF8.GetString(mess_i_Byte, 0, len_i);
                    index += len_i;
                    mess_list.Add(mess_i);
                }
                string[] mess = mess_list.ToArray();
                byte[] checkCodeByte = new byte[4];
                Array.Copy(data, 7 + messLen, checkCodeByte, 0, 4);
                Int32 checkCode = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(checkCodeByte, 0));
                byte footer = data[11 + messLen];
                // 若数据包匹配，则根据数据包进行相应操作
                if (checkCode == GetCheckCode(nameID, commandID, messLen) && header == HEADER && footer == FOOTER)
                {
                    if (!CheckName(nameID))
                    {
                        from.Send(BuildDataPackage(0x66));
                        return null;
                    }
                    return Message.Match(nameID, commandID, mess, from);
                }
                else
                {
                    // 数据包格式有误
                    if(from.Connected)
                    {
                        from.Send(BuildDataPackage(0xBB));
                    }
                    return null;
                }
            }
        }

        public static Int32 GetCheckCode(byte nameID, byte commandID)
        {
            return nameID * commandID;
        }

        public static Int32 GetCheckCode(byte nameID, byte commandID, Int32 length)
        {
            return nameID * commandID * length;
        }

        private static Boolean CheckName(byte nameID)
        {
            return nameID == MainWindow.Turn;
        }

    }
}
