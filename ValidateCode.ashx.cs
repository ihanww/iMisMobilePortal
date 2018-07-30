using System;
using System.Drawing;
using System.Web;
using System.Drawing.Drawing2D;
using System.IO;
using System.Web.SessionState;

namespace iMisMvc
{
    /// <summary>
    /// ValidateCode 的摘要说明
    /// </summary>
    public class ValidateCode : IHttpHandler, IRequiresSessionState
    {

        public void ProcessRequest(HttpContext context) {
            //生成随机字符串
            var random = new Random();
            var checkCode = string.Empty;
            for(var i = 0; i <= 3; i++) {
                var number = random.Next(Int16.MaxValue);
                var code = (char)(65 + number % 26);
                if(Convert.ToInt32(code) == 79) code = 'A';
                checkCode += code.ToString();
            }

            //context.Response.Cookies.Add(new HttpCookie("CheckCode", checkCode));
            context.Session["CheckCode"] = checkCode;

            GenerateValidateCode(context, checkCode);
        }

        /// <summary>
        /// 生成验证码
        /// </summary>
        /// <param name="context"></param>
        /// <param name="checkCode"></param>
        private static void GenerateValidateCode(HttpContext context, string checkCode) {
            
            var random = new Random();
            var image = new Bitmap(Convert.ToInt32(Math.Ceiling(checkCode.Length * 12.5)), 20);
            var g = Graphics.FromImage(image);

            try {

                //清空图片背景色
                g.Clear(Color.White);

                //绘制噪音线 
                for(var i = 0; i <= 24; i++) {
                    var x1 = random.Next(image.Width * 2);
                    var y1 = random.Next(image.Height * 2);
                    var x2 = random.Next(image.Width * 2);
                    var y2 = random.Next(image.Height * 2);
                    g.DrawLine(new Pen(Color.Silver), x1, y1, x2, y2);
                }

                //绘制验证码
                var rect = new Rectangle(0, 0, image.Width, image.Height);
                var font = new Font("Arial", 14F, FontStyle.Italic, GraphicsUnit.Pixel);
                var brush = new LinearGradientBrush(rect, Color.DarkGreen, Color.Green, 1.2F, true);
                g.DrawString(checkCode, font, brush, 3, 0);

                //绘制图片的前景噪音点
                for(var i = 0; i <= 49; i++) {
                    var x1 = random.Next(image.Width);
                    var y1 = random.Next(image.Height);
                    image.SetPixel(x1, y1, Color.FromArgb(random.Next()));
                }

                //绘制图片的边框线
                g.DrawRectangle(new Pen(Color.Silver), 0, 0, image.Width - 1, image.Height - 1);
                var ms = new MemoryStream();
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Gif);
                context.Response.ClearContent();
                context.Response.ContentType = "image/gif";
                context.Response.BinaryWrite(ms.ToArray());
                context.Response.Flush();
                context.Response.End();
                context.Response.Close();
            } catch {
                g.Dispose();
                image.Dispose();
            }
        }

        public bool IsReusable => false;
    }
}