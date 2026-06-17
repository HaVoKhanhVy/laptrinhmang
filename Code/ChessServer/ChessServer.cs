using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChessServer
{
    class Program
    {
        public const int Port = 8888;
        public const int MaxRooms = 2;
        public const int MaxClientsPerRoom = 3;

        static TcpListener server;
        static readonly object LockObj = new object();
        static readonly Dictionary<int, Room> Rooms = new Dictionary<int, Room>();

        static void Main(string[] args)
        {
            Console.Title = "Cờ Tướng Online - SERVER";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║     CỜ TƯỚNG ONLINE - SERVER         ║");
            Console.WriteLine("║   Tối đa 2 phòng, 3 người/phòng      ║");
            Console.WriteLine("║   (2 người chơi + 1 khán giả)        ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.ResetColor();

            for (int i = 1; i <= MaxRooms; i++)
                Rooms[i] = new Room(i);

            server = new TcpListener(IPAddress.Any, Port);
            server.Start();

            string localIP = GetLocalIP();
            Console.WriteLine($"\n[SERVER] IP: {localIP} | Port: {Port}");
            Console.WriteLine("[SERVER] Đang chờ kết nối...\n");
            Console.WriteLine("Nhấn Enter để dừng server...");

            Thread acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();

            Console.ReadLine();
            server.Stop();
        }

        static void AcceptLoop()
        {
            while (true)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    Thread handler = new Thread(() => HandleClient(client));
                    handler.IsBackground = true;
                    handler.Start();
                }
                catch
                {
                    break;
                }
            }
        }

        static void HandleClient(TcpClient client)
        {
            NetworkStream stream = null;
            ClientSlot slot = null;

            try
            {
                stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) return;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                if (!message.StartsWith("JOIN:"))
                {
                    SendMessage(stream, "ERROR:INVALID_JOIN");
                    client.Close();
                    return;
                }

                int roomId;
                if (!int.TryParse(message.Substring(5), out roomId) || roomId < 1 || roomId > MaxRooms)
                {
                    SendMessage(stream, "ERROR:INVALID_ROOM");
                    client.Close();
                    return;
                }

                lock (LockObj)
                {
                    Room room = Rooms[roomId];
                    slot = room.TryAddClient(client, stream);
                    if (slot == null)
                    {
                        SendMessage(stream, "ERROR:ROOM_FULL");
                        client.Close();
                        return;
                    }

                    string roleMsg = "ROLE:" + slot.Role;
                    SendMessage(stream, roleMsg);

                    string roleName = slot.Role == 0 ? "Quân ĐỎ" :
                                      slot.Role == 1 ? "Quân ĐEN" : "Khán giả";
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[Phòng {roomId}] {roleName} đã vào ({room.ClientCount}/3)");
                    Console.ResetColor();

                    if (room.HasTwoPlayers() && !room.GameStarted)
                    {
                        room.GameStarted = true;
                        room.Broadcast("START");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"[Phòng {roomId}] Game bắt đầu!");
                        Console.ResetColor();
                    }
                    else if (!room.HasTwoPlayers())
                    {
                        SendMessage(stream, "WAITING");
                    }
                    else if (room.GameStarted && slot.Role == 2)
                    {
                        SendMessage(stream, "START");
                    }
                }

                ListenFromClient(slot);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[SERVER] Lỗi client: " + ex.Message);
                Console.ResetColor();
            }
            finally
            {
                if (slot != null)
                    RemoveClient(slot);
            }
        }

        static void ListenFromClient(ClientSlot slot)
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (true)
                {
                    int bytesRead = slot.Stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (message.StartsWith("MOVE:"))
                    {
                        if (slot.Role == 2)
                            continue;

                        string playerName = slot.Role == 0 ? "Đỏ" : "Đen";
                        Console.WriteLine($"[Phòng {slot.RoomId}][{playerName}] → {message}");

                        lock (LockObj)
                        {
                            Room room = Rooms[slot.RoomId];
                            room.BroadcastExcept(slot, message);
                        }
                    }
                }
            }
            catch
            {
                string roleName = slot.Role == 0 ? "Quân ĐỎ" :
                                  slot.Role == 1 ? "Quân ĐEN" : "Khán giả";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Phòng {slot.RoomId}] {roleName} ngắt kết nối");
                Console.ResetColor();
            }
        }

        static void RemoveClient(ClientSlot slot)
        {
            lock (LockObj)
            {
                Room room = Rooms[slot.RoomId];
                room.RemoveClient(slot);

                if (slot.Role != 2)
                    room.Broadcast("OPPONENT_DISCONNECTED");

                room.GameStarted = false;
                Console.WriteLine($"[Phòng {slot.RoomId}] Còn {room.ClientCount} người");
            }

            try { slot.Stream.Close(); } catch { }
            try { slot.Client.Close(); } catch { }
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

    class ClientSlot
    {
        public int RoomId;
        public int Role;
        public TcpClient Client;
        public NetworkStream Stream;

        public ClientSlot(int roomId, int role, TcpClient client, NetworkStream stream)
        {
            RoomId = roomId;
            Role = role;
            Client = client;
            Stream = stream;
        }
    }

    class Room
    {
        public int Id;
        public bool GameStarted;
        readonly List<ClientSlot> Clients = new List<ClientSlot>();

        public Room(int id) { Id = id; }

        public int ClientCount { get { return Clients.Count; } }

        public bool HasTwoPlayers()
        {
            int players = 0;
            foreach (ClientSlot c in Clients)
                if (c.Role == 0 || c.Role == 1) players++;
            return players >= 2;
        }

        public ClientSlot TryAddClient(TcpClient client, NetworkStream stream)
        {
            if (Clients.Count >= Program.MaxClientsPerRoom)
                return null;

            int role;
            bool hasRed = false, hasBlack = false;
            foreach (ClientSlot c in Clients)
            {
                if (c.Role == 0) hasRed = true;
                if (c.Role == 1) hasBlack = true;
            }

            if (!hasRed) role = 0;
            else if (!hasBlack) role = 1;
            else role = 2;

            ClientSlot slot = new ClientSlot(Id, role, client, stream);
            Clients.Add(slot);
            return slot;
        }

        public void RemoveClient(ClientSlot slot)
        {
            Clients.Remove(slot);
        }

        public void Broadcast(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (ClientSlot c in Clients)
            {
                try { c.Stream.Write(data, 0, data.Length); } catch { }
            }
        }

        public void BroadcastExcept(ClientSlot sender, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (ClientSlot c in Clients)
            {
                if (c == sender) continue;
                try { c.Stream.Write(data, 0, data.Length); } catch { }
            }
        }
    }
}
