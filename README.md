

## 一、五子棋服务器运行环境

IDE:Visual Studio 2017(C# + WPF)

框架：.NET Framework 4.6.1

## 二、连接服务器

ipAddress为五子棋服务器程序运行计算机ipv4地址，可以通过<span style='color:#44dddd;'>WIN + R</span>打开运行框，输入<span style='color:#44dddd;'>cmd</span>进入命令行终端，输入<span style='color:#44dddd;'>ipconfig</span>命令查看

port为五子棋服务器监听的端口，通过port框进行设置

可以用自己计算机运行五子棋服务器程序进行测试，也可以连接10.203.183.169:8680进行测试

## 二、游戏开始

当连接服务器的客户端数量为2时自动开始游戏，并通过随机数的方式决定哪方先走，且会给先落子一方发送“<span style='color:#44dddd;'>BEGIN</span>"指令

## 三、游戏规则

- 当客户端接收到”BEGIN"指令时，需要通过发送"<span style='color:#44dddd;'>position,x,y</span>"指令告诉服务器落子位置
  - 由于棋盘大小为15 * 15，所以$0\leq x \leq 14$, $0 \leq y \leq 14$
- 落子后服务器会检查落子一方是否胜利，若胜利，则给胜利一方发送"<span style='color:#44dddd;'>WIN</span>"指令，失败一方发送"<span style='color:#44dddd;'>FAIL</span>"指令，并结束游戏
- 若未胜利，服务器会将落子位置信息转发给对手方，对手方需要发送位置指令告诉服务器落子位置

## 四、其他规则

- 客户端可以通过发送"<span style='color:#44dddd;'>SURRENDER</span>"指令认输，服务器收到该指令后，同样会发送"<span style='color:#44dddd;'>WIN</span>"指令和"<span style='color:#44dddd;'>FAIL</span>"指令给相应的客户端
- 当客户端发送的指令不符合上述规则时，服务器会回应"<span style='color:#44dddd;'>ILLEGAL</span>"指令，客户端需要重新发送指令
- 当客户端发送的落子位置超出棋盘范围时，服务器会回应"<span style='color:#44dddd;'>ILLEGAL</span>"指令，客户端需要重新发送指令
- 当客户端发送的落子位置上已有棋子时，服务器会回应"<span style='color:#44dddd;'>EXIST</span>"指令，客户端需要重新发送指令
- 当对战中的客户端之一掉线（客户端程序关闭）时，判定为该方认输，并向另一方发送"<span style='color:#44dddd;'>WIN</span>"指令
- 当客户端30s内没有回复任何信息则认定该客户端认输













