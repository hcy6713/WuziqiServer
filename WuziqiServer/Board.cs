using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WuziqiServer
{
    // 检查落子位置是否合法与落子后是否胜利
    class Board
    {
        // 检查落子位置是否合法
        public static bool IsLegal(string[] mess)
        {
            if(mess.Length == 2)
            {
                int x = -1;
                int y = -1;
                try
                {
                    x = int.Parse(mess[0]);
                    y = int.Parse(mess[1]);
                }
                catch
                {
                    throw new Exception("ILLEGAL");
                }
                if (!(0 <= x && x <= 14 && 0 <= y && y <= 14))
                {
                    throw new Exception("OUT");
                }
                if(MainWindow.ChessStatus[x, y] != 0)
                {
                    throw new Exception("EXIST");
                }
                return true;
            }
            return false;
        }

        public static Boolean IsWin(int chessColor)
        {
            return ExamHorizon(chessColor) || ExamVertical(chessColor)
                || ExamLeftTitled(chessColor) || ExamRightTitled(chessColor);
        }

        // 水平方向检查五子连心
        private static Boolean ExamHorizon(int temp)
        {
            for (int i = 0; i <= 10; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    if (MainWindow.ChessStatus[i, j] == temp)
                    {
                        int nextx = i + 1;
                        while (MainWindow.ChessStatus[nextx, j] == temp)
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
        private static Boolean ExamVertical(int temp)
        {
            for (int i = 0; i < 15; i++)
            {
                for (int j = 0; j <= 10; j++)
                {
                    if (MainWindow.ChessStatus[i, j] == temp)
                    {
                        int nexty = j + 1;
                        while (MainWindow.ChessStatus[i, nexty] == temp)
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
        private static Boolean ExamRightTitled(int temp)
        {
            for (int i = 0; i <= 10; i++)
            {
                for (int j = 0; j <= 10; j++)
                {
                    if (MainWindow.ChessStatus[i, j] == temp)
                    {
                        int nextx = i + 1;
                        int nexty = j + 1;
                        while (MainWindow.ChessStatus[nextx, nexty] == temp)
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

        // 斜下方向检查五子连心
        private static Boolean ExamLeftTitled(int temp)
        {
            for (int i = 4; i <= 14; i++)
            {
                for (int j = 0; j <= 10; j++)
                {
                    if (MainWindow.ChessStatus[i, j] == temp)
                    {
                        int nextx = i - 1;
                        int nexty = j + 1;
                        while (MainWindow.ChessStatus[nextx, nexty] == temp)
                        {
                            nextx--;
                            nexty++;
                            if (nextx - i == -5) return true;
                            if (nextx < 0) return false;
                        }
                    }
                }
            }
            return false;
        }
    }
}
