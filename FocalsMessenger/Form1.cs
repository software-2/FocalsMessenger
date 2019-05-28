using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
//using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

using WebSocketSharp;

namespace FocalsMessenger
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        class Response
        {
            public string accessCode { get; set; }
        }

        class SlideData
        {
            public string state { get; set; }
            public string title { get; set; }
            public string notes { get; set; }
            public int slide_number { get; set; }
            public int total_slides { get; set; }
        }

        class SocketResonse
        {
            public string type { get; set; }
        }

        private string getPresentationCode()
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://north-teleprompter.herokuapp.com/v1/presentation");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                Console.WriteLine(result);

                var deserialized = new JavaScriptSerializer().Deserialize<Response>(result);
                return deserialized.accessCode;
            }
        }

        private string convertCodeToLetters(string presentationCode)
        {
            //Numeric codes are just a cipher mapping to A-E
            return presentationCode
                .Replace('0', 'A')
                .Replace('1', 'B')
                .Replace('2', 'C')
                .Replace('3', 'D')
                .Replace('4', 'E');
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Connect Clicked");
            string presentationCode = getPresentationCode();
            Console.WriteLine("Presentation Code: " + presentationCode);
            button1.Text = convertCodeToLetters(presentationCode);
            ws = createSocket(presentationCode);
            ws.Connect();
        }


        WebSocket ws;
        private WebSocket createSocket(string presentationCode)
        {
            var sock = new WebSocket("ws://north-teleprompter.herokuapp.com:80/?presentation_code=" + presentationCode + "&role=browser_extension");
            sock.OnOpen += (sender, e) => {
                Console.WriteLine("Connected socket!");
            };

            sock.OnClose += (sender, e) => {
                button1.Text = "Click To Restart";
            };

            sock.EmitOnPing = true;
            sock.OnMessage += (sender, e) => {
                if (e.IsPing)
                {
                    Console.WriteLine("PING RECEIVED: " + e.Data.ToString());
                    return;
                }

                if (e.IsText)
                {
                    Console.WriteLine("Message Received: " + e.Data.ToString());
                    SocketResonse response = new JavaScriptSerializer().Deserialize<SocketResonse>(e.Data);
                    processResponse(response);
                    return;
                }

                if (e.IsBinary)
                {
                    Console.WriteLine("Binary Message Received... Shit.");
                    return;
                }

            };
            return sock;
        }


        private void processResponse(SocketResonse response)
        {
            if (response.type == "connected")
            {
                string json = new JavaScriptSerializer().Serialize(new
                {
                    type = "current_state",
                    data = new SlideData() { state = "currently_presenting", title = "Test Title 1", notes = "Nooooootes", slide_number = 1, total_slides = 2 }
                });
                ws.Send(json);
            }

            else if (response.type == "next_slide")
            {
                incrementSlide();
            }

            else if (response.type == "previous_slide")
            {
                decrementSlide();
            }
        }



        delegate void SetTextCallback(string text);

        private void SetText(string text)
        {
            if (this.textBox1.InvokeRequired || this.richTextBox1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.textBox1.Text = text;
                int lineNumber = 0;
                if (Int32.TryParse(textBox1.Text, out lineNumber))
                {
                    highlightLine(lineNumber);
                }
            }
        }

        private void decrementSlide()
        {
            int lineNumber = 0;
            if (!Int32.TryParse(textBox1.Text, out lineNumber))
            {
                lineNumber = -1;
            }
            lineNumber--;
            SetText(lineNumber.ToString());
            sendSlide();
        }

        private void incrementSlide()
        {
            int lineNumber = 0;
            if (!Int32.TryParse(textBox1.Text, out lineNumber))
            {
                lineNumber = -1;
            }
            lineNumber++;
            SetText(lineNumber.ToString());
            sendSlide();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            decrementSlide();
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            incrementSlide();
        }

        delegate void sendSlideCallback();
        private void sendSlide()
        {
            if (this.textBox1.InvokeRequired || this.richTextBox1.InvokeRequired)
            {
                sendSlideCallback d = new sendSlideCallback(sendSlide);
                this.Invoke(d, new object[] { });
                return;
            }

            int lineNumber = 0;
            if (!Int32.TryParse(textBox1.Text, out lineNumber))
            {
                lineNumber = -1;
            }

            var index = Math.Min(richTextBox1.Lines.Length, lineNumber);
            string notes;

            if (index >= 0 && index <= richTextBox1.Lines.Length)
            {
                notes = richTextBox1.Lines[index];
                
            }
            else
            {
                index = 0;
                notes = "Invalid line number sent. Oh noes!";
            }

            string json = new JavaScriptSerializer().Serialize(new
            {
                type = "current_state",
                data = new SlideData() { state = "currently_presenting", title = null, notes = notes, slide_number = -1 /*Disables title*/, total_slides = richTextBox1.Lines.Length }
            });

            ArraySegment<byte> bytesToSend = new ArraySegment<byte>(
                        Encoding.UTF8.GetBytes(json));
            ws.Send(json);
        }

        private void highlightLine(int lineNumber)
        {
            richTextBox1.SelectAll();
            richTextBox1.SelectionBackColor = richTextBox1.BackColor;
            if (lineNumber < 0 || lineNumber > richTextBox1.Lines.Length)
            {
                return;
            }
            var startIndex = richTextBox1.GetFirstCharIndexFromLine(lineNumber);
            richTextBox1.Select(startIndex, startIndex + richTextBox1.Lines[lineNumber].Length);
            richTextBox1.SelectionBackColor = Color.CornflowerBlue;
        }
    }
}
