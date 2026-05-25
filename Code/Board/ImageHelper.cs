using System;
using System.Drawing;
using System.Windows.Forms;

namespace Board
{
    public static class ImageHelper
    {
        /// <summary>
        /// Gán image an toàn - nếu image là null thì bỏ qua lỗi
        /// </summary>
        public static void SetImage(PictureBox control, Bitmap image)
        {
            try
            {
                if (image != null)
                    control.Image = image;
            }
            catch
            {
                // Bỏ qua lỗi nếu image không tồn tại
            }
        }
    }
}
