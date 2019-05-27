using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

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

        private string getPresentationCode()
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://north-teleprompter.herokuapp.com/v1/presentation");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = new JavaScriptSerializer().Serialize(new
                {
                    state = "presentation_focused",
                    title = "Muh Presentation Hax"
                });

                streamWriter.Write(json);
            }

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
            //I'm lazy, this works.
            return presentationCode
                .Replace('0', 'A')
                .Replace('1', 'B')
                .Replace('2', 'C')
                .Replace('3', 'D')
                .Replace('4', 'E')
                .Replace('5', 'F')
                .Replace('6', 'G');
        }

        private string presentationCode = "";
        private Boolean isConnected = false;


        private static async Task ChatWithServer(string presentationCode)
        {
            using (ClientWebSocket ws = new ClientWebSocket())
            {
                Uri serverUri = new Uri("ws://north-teleprompter.herokuapp.com:80/?presentation_code=" + presentationCode + "&role=browser_extension");
                await ws.ConnectAsync(serverUri, CancellationToken.None);
                while (true)
                {

                    Thread.Sleep(10000);
                    string json = new JavaScriptSerializer().Serialize(new
                    {
                        type = "current_state",
                        data = new SlideData() { state="current_state", title="muh title", notes="notes go here", slide_number=1, total_slides=2 }
                    });
                    Console.WriteLine(json);

                    ArraySegment<byte> bytesToSend = new ArraySegment<byte>(
                        Encoding.UTF8.GetBytes(json));
                    await ws.SendAsync(
                        bytesToSend, WebSocketMessageType.Text,
                        true, CancellationToken.None);
                    ArraySegment<byte> bytesReceived = new ArraySegment<byte>(new byte[1024]);
                    WebSocketReceiveResult result = await ws.ReceiveAsync(
                        bytesReceived, CancellationToken.None);
                    Console.WriteLine(Encoding.UTF8.GetString(
                        bytesReceived.Array, 0, result.Count));
                    if (ws.State != WebSocketState.Open)
                    {
                        break;
                    }
                }
            }
        }
        private void Button1_Click(object sender, EventArgs e)
        {
            
            Console.WriteLine("WHATR");
            presentationCode = getPresentationCode();
            Console.WriteLine(presentationCode);
            button1.Text = convertCodeToLetters(presentationCode);
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            Task t = ChatWithServer(presentationCode);
            t.Wait();
        }
    }
}
