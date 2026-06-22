using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Board
{
    public static class BoardRenderer
    {
        public const int CellSize = 53;
        public const int BoardLeft = 61;
        public const int BoardTop = 80;
        public const int BoardCols = 9;
        public const int BoardRows = 10;

        private static readonly Dictionary<string, Bitmap> PieceCache = new Dictionary<string, Bitmap>();

        public static void DrawBoardOn(Graphics g, int width, int height)
        {
            g.Clear(Color.FromArgb(35, 35, 35));

            int boardWidth = (BoardCols - 1) * CellSize;
            int boardHeight = (BoardRows - 1) * CellSize;
            int bx = BoardLeft;
            int by = BoardTop;

            using (Brush wood = new SolidBrush(Color.FromArgb(210, 175, 115)))
            {
                g.FillRectangle(wood, bx - 8, by - 8, boardWidth + 16, boardHeight + 16);
            }

            using (Pen gridPen = new Pen(Color.Black, 1.5f))
            {
                for (int col = 0; col < BoardCols; col++)
                {
                    int x = bx + col * CellSize;
                    int yStart = by;
                    int yEnd = by + boardHeight;
                    if (col == 0 || col == BoardCols - 1)
                        g.DrawLine(gridPen, x, yStart, x, yEnd);
                    else
                    {
                        g.DrawLine(gridPen, x, yStart, x, by + 4 * CellSize);
                        g.DrawLine(gridPen, x, by + 5 * CellSize, x, yEnd);
                    }
                }

                for (int row = 0; row < BoardRows; row++)
                {
                    int y = by + row * CellSize;
                    g.DrawLine(gridPen, bx, y, bx + boardWidth, y);
                }

                int palaceLeft = bx + 3 * CellSize;
                int palaceRight = bx + 5 * CellSize;
                int redPalaceTop = by + 7 * CellSize;
                int redPalaceBottom = by + 9 * CellSize;
                int blackPalaceTop = by;
                int blackPalaceBottom = by + 2 * CellSize;

                g.DrawLine(gridPen, palaceLeft, redPalaceTop, palaceRight, redPalaceBottom);
                g.DrawLine(gridPen, palaceRight, redPalaceTop, palaceLeft, redPalaceBottom);
                g.DrawLine(gridPen, palaceLeft, blackPalaceTop, palaceRight, blackPalaceBottom);
                g.DrawLine(gridPen, palaceRight, blackPalaceTop, palaceLeft, blackPalaceBottom);
            }

            using (Pen riverPen = new Pen(Color.FromArgb(180, 60, 40), 2f))
            {
                int riverY = by + 4 * CellSize + CellSize / 2;
                g.DrawLine(riverPen, bx, riverY, bx + boardWidth, riverY);
            }

            using (Font titleFont = new Font("Arial", 14, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(Color.Gold))
            {
                g.DrawString("CỜ TƯỚNG", titleFont, titleBrush, bx + boardWidth / 2 - 50, by - 35);
            }
        }

        public static void RefreshBackBuffer(Bitmap backBuffer)
        {
            if (backBuffer == null) return;
            using (Graphics g = Graphics.FromImage(backBuffer))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                try
                {
                    Bitmap bg = Properties.Resources.bg1;
                    if (bg != null)
                        g.DrawImage(bg, 0, 0, backBuffer.Width, backBuffer.Height);
                    else
                        DrawBoardOn(g, backBuffer.Width, backBuffer.Height);
                }
                catch
                {
                    DrawBoardOn(g, backBuffer.Width, backBuffer.Height);
                }
            }
        }

        public static Bitmap GetPieceImage(int party, string name)
        {
            string key = party + "_" + name;
            if (PieceCache.ContainsKey(key))
                return PieceCache[key];

            string label = GetPieceLabel(party, name);
            Color fill = party == 0 ? Color.FromArgb(200, 40, 40) : Color.FromArgb(30, 30, 30);
            Color border = Color.FromArgb(220, 180, 80);

            Bitmap bmp = new Bitmap(42, 42);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(fill), 3, 3, 36, 36);
                g.DrawEllipse(new Pen(border, 2f), 3, 3, 36, 36);
                using (Font font = new Font("SimSun", 14, FontStyle.Bold))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(label, font, Brushes.White, new RectangleF(0, 0, 42, 42), sf);
                }
            }

            PieceCache[key] = bmp;
            return bmp;
        }

        private static string GetPieceLabel(int party, string name)
        {
            switch (name)
            {
                case "tuong": return party == 0 ? "帥" : "將";
                case "sy": return "士";
                case "tinh": return party == 0 ? "相" : "象";
                case "xe": return "車";
                case "phao": return party == 0 ? "炮" : "砲";
                case "ma": return "馬";
                case "chot": return party == 0 ? "兵" : "卒";
                default: return "?";
            }
        }

        public static Bitmap GetResourceBitmap(string resourceName, int party, string pieceName)
        {
            try
            {
                Bitmap img = null;
                switch (resourceName)
                {
                    case "tuong": img = party == 0 ? Properties.Resources._1tuong : Properties.Resources._2tuong; break;
                    case "sy": img = party == 0 ? Properties.Resources._1sy : Properties.Resources._2sy; break;
                    case "tinh": img = party == 0 ? Properties.Resources._1tinh : Properties.Resources._2tinh; break;
                    case "xe": img = party == 0 ? Properties.Resources._1xe : Properties.Resources._2xe; break;
                    case "phao": img = party == 0 ? Properties.Resources._1phao : Properties.Resources._2phao; break;
                    case "ma": img = party == 0 ? Properties.Resources._1ma : Properties.Resources._2ma; break;
                    case "chot": img = party == 0 ? Properties.Resources._1chot : Properties.Resources._2chot; break;
                }
                if (img != null) return img;
            }
            catch { }
            return GetPieceImage(party, pieceName ?? resourceName);
        }
    }
}
