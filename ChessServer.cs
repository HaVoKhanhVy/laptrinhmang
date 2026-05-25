using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChessServer
{
    class Program
    {
        static TcpListener server;
        static TcpClient client1, client2;
        static NetworkStream stream1, stream2;
        static bool client1Ready = false, client2Ready = false;

        static void Main(string[] args)
        {
            Console.Title = "Cờ Tướng Online - SERVER";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║     CỜ TƯỚNG ONLINE - SERVER         ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.ResetColor();

            int port = 8888;
            server = new TcpListener(IPAddress.Any, port);
            server.Start();

            // Lấy IP của máy để hiển thị cho client biết
            string localIP = GetLocalIP();
            Console.WriteLine($"\n[SERVER] Đang chạy tại IP: {localIP} | Port: {port}");
            Console.WriteLine("[SERVER] Đang chờ 2 người chơi kết nối...\n");

            // Chờ client 1 kết nối
            client1 = server.AcceptTcpClient();
            stream1 = client1.GetStream();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[SERVER] Người chơi 1 (Quân ĐỎ) đã kết nối!");
            Console.ResetColor();
            // Thông báo cho client1 biết họ là phe 0 (Đỏ)
            SendMessage(stream1, "PARTY:0");

            // Chờ client 2 kết nối
            client2 = server.AcceptTcpClient();
            stream2 = client2.GetStream();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[SERVER] Người chơi 2 (Quân ĐEN) đã kết nối!");
            Console.ResetColor();
            // Thông báo cho client2 biết họ là phe 1 (Đen)
            SendMessage(stream2, "PARTY:1");

            // Thông báo bắt đầu game cho cả 2
            Thread.Sleep(500);
            SendMessage(stream1, "START");
            SendMessage(stream2, "START");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n[SERVER] Game bắt đầu! Đang relay nước đi...\n");
            Console.ResetColor();

            // Lắng nghe nước đi từ cả 2 client trong 2 thread riêng
            Thread t1 = new Thread(() => ListenFromClient(stream1, stream2, "Đỏ"));
            Thread t2 = new Thread(() => ListenFromClient(stream2, stream1, "Đen"));
            t1.IsBackground = true;
            t2.IsBackground = true;
            t1.Start();
            t2.Start();

            Console.WriteLine("Nhấn Enter để dừng server...");
            Console.ReadLine();
        }

        // Lắng nghe nước đi từ một client rồi forward sang client kia
        static void ListenFromClient(NetworkStream from, NetworkStream to, string playerName)
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (true)
                {
                    int bytesRead = from.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[{playerName}] → {message}");

                    // Forward nguyên xi sang client kia
                    SendMessage(to, message);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[SERVER] Mất kết nối với {playerName}: {ex.Message}");
                Console.ResetColor();
                // Thông báo cho client còn lại biết đối thủ đã ngắt kết nối
                try { SendMessage(to, "OPPONENT_DISCONNECTED"); } catch { }
            }
        }

        static void SendMessage(NetworkStream stream, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            stream.Write(data, 0, data.Length);
        }

        static string GetLocalIP()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint.Address.ToString();
                }
            }
            catch { return "127.0.0.1"; }
        }
    }
}
