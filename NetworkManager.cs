using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Board
{
    /// <summary>
    /// Quản lý toàn bộ kết nối mạng cho game Cờ Tướng Online
    /// </summary>
    public static class NetworkManager
    {
        private static TcpClient tcpClient;
        private static NetworkStream stream;
        private static Thread listenThread;

        public static bool IsOnline = false;        // Đang chơi online hay offline
        public static bool IsConnected = false;     // Đã kết nối server chưa
        public static int MyParty = -1;             // Phe của mình: 0 = Đỏ, 1 = Đen
        public static bool GameStarted = false;     // Game đã bắt đầu chưa (đủ 2 người)

        // Event để ChessBoard.cs lắng nghe khi nhận được nước đi từ đối thủ
        public delegate void MoveHandler(int fromRow, int fromCol, int toRow, int toCol);
        public static event MoveHandler OnMoveReceived;
        // Event khi game bắt đầu (đủ 2 người kết nối)
        public static event Action<int> OnGameStarted;
        // Event khi đối thủ ngắt kết nối
        public delegate void SimpleHandler();
        public static event SimpleHandler OnOpponentDisconnected;
        // Event thông báo trạng thái kết nối
        public static event Action<string> OnStatusChanged;

        /// <summary>
        /// Kết nối tới Server
        /// </summary>
        public static bool Connect(string ip, int port)
        {
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(ip, port);
                stream = tcpClient.GetStream();
                IsConnected = true;

                // Bắt đầu lắng nghe message từ server trong thread riêng
                listenThread = new Thread(ListenFromServer);
                listenThread.IsBackground = true;
                listenThread.Start();

                OnStatusChanged?.Invoke("Đã kết nối server! Đang chờ người chơi 2...");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                OnStatusChanged?.Invoke("Lỗi kết nối: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gửi nước đi lên Server (fromRow, fromCol, toRow, toCol)
        /// Format: "MOVE:fromRow,fromCol,toRow,toCol"
        /// </summary>
        public static void SendMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            if (!IsConnected || stream == null) return;
            try
            {
                string message = $"MOVE:{fromRow},{fromCol},{toRow},{toCol}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke("Lỗi gửi nước đi: " + ex.Message);
            }
        }

        /// <summary>
        /// Thread lắng nghe message từ Server liên tục
        /// </summary>
        private static void ListenFromServer()
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (IsConnected)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    ProcessMessage(message);
                }
            }
            catch
            {
                IsConnected = false;
                OnOpponentDisconnected?.Invoke();
            }
        }

        /// <summary>
        /// Xử lý message nhận được từ Server
        /// </summary>
        private static void ProcessMessage(string message)
        {
            // PARTY:0 hoặc PARTY:1 → server thông báo mình là phe nào
            if (message.StartsWith("PARTY:"))
            {
                MyParty = int.Parse(message.Split(':')[1]);
                OnStatusChanged?.Invoke(MyParty == 0
                    ? "Bạn là Quân ĐỎ (đi trước). Đang chờ đối thủ..."
                    : "Bạn là Quân ĐEN. Đang chờ đối thủ...");
            }

            // START → đủ 2 người, bắt đầu game
            else if (message == "START")
            {
                GameStarted = true;
                OnGameStarted?.Invoke(MyParty);
                OnStatusChanged?.Invoke("Game bắt đầu!");
            }

            // MOVE:fromRow,fromCol,toRow,toCol → nhận nước đi từ đối thủ
            else if (message.StartsWith("MOVE:"))
            {
                string[] parts = message.Replace("MOVE:", "").Split(',');
                int fromRow = int.Parse(parts[0]);
                int fromCol = int.Parse(parts[1]);
                int toRow   = int.Parse(parts[2]);
                int toCol   = int.Parse(parts[3]);
                OnMoveReceived?.Invoke(fromRow, fromCol, toRow, toCol);
            }

            // Đối thủ ngắt kết nối
            else if (message == "OPPONENT_DISCONNECTED")
            {
                OnOpponentDisconnected?.Invoke();
            }
        }

        /// <summary>
        /// Ngắt kết nối
        /// </summary>
        public static void Disconnect()
        {
            IsConnected = false;
            IsOnline = false;
            GameStarted = false;
            MyParty = -1;
            try { stream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }
        }
    }
}
