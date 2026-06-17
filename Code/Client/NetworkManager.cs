using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Board
{
    /// <summary>
    /// Quản lý kết nối mạng - hỗ trợ 2 phòng, mỗi phòng 3 client (2 chơi + 1 xem).
    /// </summary>
    public static class NetworkManager
    {
        private static TcpClient tcpClient;
        private static NetworkStream stream;
        private static Thread listenThread;

        public static bool IsOnline = false;
        public static bool IsConnected = false;
        public static int MyRole = -1;
        public static int MyParty = -1;
        public static bool IsSpectator = false;
        public static bool GameStarted = false;
        public static int RoomId = 0;

        public delegate void MoveHandler(int fromRow, int fromCol, int toRow, int toCol);
        public static event MoveHandler OnMoveReceived;
        public delegate void GameStartedHandler(int role);
        public static event GameStartedHandler OnGameStarted;
        public delegate void SimpleHandler();
        public static event SimpleHandler OnOpponentDisconnected;
        public delegate void StatusHandler(string status);
        public static event StatusHandler OnStatusChanged;

        public static bool Connect(string ip, int port, int roomId)
        {
            try
            {
                Disconnect();

                tcpClient = new TcpClient();
                tcpClient.Connect(ip, port);
                stream = tcpClient.GetStream();
                IsConnected = true;
                RoomId = roomId;

                string joinMsg = "JOIN:" + roomId;
                byte[] joinData = Encoding.UTF8.GetBytes(joinMsg);
                stream.Write(joinData, 0, joinData.Length);

                listenThread = new Thread(ListenFromServer);
                listenThread.IsBackground = true;
                listenThread.Start();

                if (OnStatusChanged != null)
                    OnStatusChanged("Đã kết nối phòng " + roomId + "! Đang chờ...");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                if (OnStatusChanged != null)
                    OnStatusChanged("Lỗi kết nối: " + ex.Message);
                return false;
            }
        }

        public static void SendMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            if (!IsConnected || stream == null || IsSpectator) return;
            try
            {
                string message = "MOVE:" + fromRow + "," + fromCol + "," + toRow + "," + toCol;
                byte[] data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                if (OnStatusChanged != null)
                    OnStatusChanged("Lỗi gửi nước đi: " + ex.Message);
            }
        }

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
                if (OnOpponentDisconnected != null)
                    InvokeOnUI(delegate { OnOpponentDisconnected(); });
            }
        }

        private static void ProcessMessage(string message)
        {
            if (message.StartsWith("ROLE:"))
            {
                MyRole = int.Parse(message.Split(':')[1]);
                IsSpectator = (MyRole == 2);
                MyParty = IsSpectator ? -1 : MyRole;

                string status;
                if (MyRole == 0)
                    status = "Bạn là Quân ĐỎ (đi trước). Đang chờ đối thủ...";
                else if (MyRole == 1)
                    status = "Bạn là Quân ĐEN. Đang chờ game bắt đầu...";
                else
                    status = "Bạn là Khán giả. Đang chờ 2 người chơi...";

                string statusCopy = status;
                InvokeOnUI(delegate
                {
                    if (OnStatusChanged != null)
                        OnStatusChanged(statusCopy);
                });
            }
            else if (message == "WAITING")
            {
                InvokeOnUI(delegate
                {
                    if (OnStatusChanged != null)
                        OnStatusChanged("Đang chờ người chơi thứ 2...");
                });
            }
            else if (message == "START")
            {
                GameStarted = true;
                InvokeOnUI(delegate
                {
                    if (OnGameStarted != null)
                        OnGameStarted(MyRole);
                    if (OnStatusChanged != null)
                        OnStatusChanged(IsSpectator ? "Đang xem trực tiếp!" : "Game bắt đầu!");
                });
            }
            else if (message.StartsWith("MOVE:"))
            {
                string[] parts = message.Replace("MOVE:", "").Split(',');
                int fromRow = int.Parse(parts[0]);
                int fromCol = int.Parse(parts[1]);
                int toRow = int.Parse(parts[2]);
                int toCol = int.Parse(parts[3]);
                InvokeOnUI(delegate
                {
                    if (OnMoveReceived != null)
                        OnMoveReceived(fromRow, fromCol, toRow, toCol);
                });
            }
            else if (message == "OPPONENT_DISCONNECTED")
            {
                if (OnOpponentDisconnected != null)
                    InvokeOnUI(delegate { OnOpponentDisconnected(); });
            }
            else if (message.StartsWith("ERROR:"))
            {
                string error = message.Substring(6);
                string msg = error == "ROOM_FULL" ? "Phòng đã đầy (3/3)!" :
                             error == "INVALID_ROOM" ? "Phòng không hợp lệ!" : "Lỗi: " + error;
                string msgCopy = msg;
                InvokeOnUI(delegate
                {
                    if (OnStatusChanged != null)
                        OnStatusChanged(msgCopy);
                });
                IsConnected = false;
            }
        }

        private delegate void VoidHandler();

        private static void InvokeOnUI(VoidHandler action)
        {
            if (action == null) return;
            if (ChessBoard.Instance != null && ChessBoard.Instance.InvokeRequired)
                ChessBoard.Instance.Invoke(action);
            else
                action();
        }

        public static void Disconnect()
        {
            IsConnected = false;
            IsOnline = false;
            GameStarted = false;
            MyRole = -1;
            MyParty = -1;
            IsSpectator = false;
            RoomId = 0;
            try { if (stream != null) stream.Close(); } catch { }
            try { if (tcpClient != null) tcpClient.Close(); } catch { }
        }
    }
}
