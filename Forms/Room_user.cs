﻿using Chat_video_app.Classes;
using Google.Cloud.Firestore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chat_video_app.Forms
{
    public partial class Room_user : Form
    {
        string id;
        string username;
        private bool connected = false;
        private Thread client = null;
        private struct MyClient
        {
            public string username;
            public TcpClient client;
            public NetworkStream stream;
            public byte[] buffer;
            public StringBuilder data;
            public EventWaitHandle handle;
        };
        private MyClient obj;
        private Task send = null;
        private bool exit = false;
        public Room_user(string username,string id)
        {
            InitializeComponent();
            usernameTextBox.Text = username;
            portTextBox.Text = id;
            this.id = id;
            this.username = username;
            sendTextBox.KeyDown += SendTextBox_KeyDown;
            listView1.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(listView1_MouseDoubleClick_1);
            DisplayMem(id);
        }
        private void DisplayMem(string id)
        {
            var db = FirestoreHelper.Database;
            DocumentReference docRef = db.Collection("RoomData").Document(id);
            RoomData data = docRef.GetSnapshotAsync().Result.ConvertTo<RoomData>();
            foreach (string i in data.Mem)
            {
                if (i == data.Mem[0]) continue;
                DocumentReference docRef2 = db.Collection("UserData").Document(i);
                UserData data2 = docRef2.GetSnapshotAsync().Result.ConvertTo<UserData>();
                AddGrid(data2.Id, data2.Username);
            }
        }
        private void AddGrid(string id, string name)
        {
            string[] row = new string[] { id, name };//fix
            clientsDataGridView.Rows.Add(row);
        }
        private void button2_Click(object sender, EventArgs e)
        {
            Hide();
            Lobby form = new Lobby(username);
            form.ShowDialog();
            Close();
        }
        private async void AddData(string text)
        {
            var db = FirestoreHelper.Database;
            DocumentReference docRef = db.Collection("RoomData").Document(id);
            RoomData data = docRef.GetSnapshotAsync().Result.ConvertTo<RoomData>();
            List<string> his = data.His.ToList();
            his.Add(text);
            data.His = his.ToArray();
            await docRef.SetAsync(data);
        }
        private void Log(string msg = "") // clear the log if message is not supplied or is empty
        {
            if (!exit)
            {
                logTextBox.Invoke((MethodInvoker)delegate
                {
                    if (msg.Length > 0) { 

                        string text = string.Format("[ {0} ] {1}", DateTime.Now.ToString("HH:mm"), msg);
                        if(connected==true)AddData(text);
                        logTextBox.AppendText(string.Format("[ {0} ] {1}{2}", DateTime.Now.ToString("HH:mm"), msg, Environment.NewLine));
                    }
                    else
                    {
                        logTextBox.Clear();
                    }
                });
            }
        }
        private string ErrorMsg(string msg)
        {
            return string.Format("ERROR: {0}", msg);
        }

        private string SystemMsg(string msg)
        {
            return string.Format("SYSTEM: {0}", msg);
        }
        private void ReadAuth(IAsyncResult result)
        {
            int bytes = 0;
            if (obj.client.Connected)
            {
                try
                {
                    bytes = obj.stream.EndRead(result);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }
            if (bytes > 0)
            {
                obj.data.AppendFormat("{0}", Encoding.UTF8.GetString(obj.buffer, 0, bytes));
                try
                {
                    if (obj.stream.DataAvailable)
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(ReadAuth), null); // Thay null bằng obj
                    }
                    else
                    {
                        Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(obj.data.ToString());
                        if (data.ContainsKey("status") && data["status"].Equals("authorized"))
                        {
                            Connected(true);
                        }
                        obj.data.Clear();
                        obj.handle.Set();
                    }
                }
                catch (Exception ex)
                {
                    obj.data.Clear();
                    Log(ErrorMsg(ex.Message));
                    obj.handle.Set();
                }
            }
            else
            {
                obj.client.Close();
                obj.handle.Set();
            }
        }
        private bool Authorize()
        {
            bool success = false;
            Dictionary<string, string> data = new Dictionary<string, string>();
            data.Add("username", obj.username.ToString());

            string jsonData = JsonConvert.SerializeObject(data); // Chuyển đổi dữ liệu sang chuỗi JSON

            Send(jsonData);

            while (obj.client.Connected)
            {
                try
                {
                    obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(ReadAuth), null);
                    obj.handle.WaitOne();
                    if (connected)
                    {
                        success = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }

            if (!connected)
            {
                Log(SystemMsg("Unauthorized"));
            }

            return success;
        }

        private void Connected(bool status)
        {
            if (!exit)
            {
                connectButton.Invoke((MethodInvoker)delegate
                {
                    connected = status;
                    if (status)
                    {
                        connectButton.Text = "Disconnect";
                        Log(SystemMsg("You are now connected"));
                    }
                    else
                    {
                        connectButton.Text = "Connect";
                        Log(SystemMsg("You are now disconnected"));
                    }
                });
            }
        }
        private void Read(IAsyncResult result)
        {
            int bytes = 0;
            if (obj.client.Connected)
            {
                try
                {
                    bytes = obj.stream.EndRead(result);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }
            if (bytes > 0)
            {
                obj.data.AppendFormat("{0}", Encoding.UTF8.GetString(obj.buffer, 0, bytes));
                try
                {
                    if (obj.stream.DataAvailable)
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), null);
                    }
                    else
                    {
                        Log(obj.data.ToString());
                        obj.data.Clear();
                        obj.handle.Set();
                    }
                }
                catch (Exception ex)
                {
                    obj.data.Clear();
                    Log(ErrorMsg(ex.Message));
                    obj.handle.Set();
                }
            }
            else
            {
                obj.client.Close();
                obj.handle.Set();
            }
        }
        private void Connection(IPAddress ip, int port, string username)
        {

            try
            {
                obj = new MyClient();
                obj.username = username;
                obj.client = new TcpClient();
                obj.client.Connect(ip, port);
                obj.stream = obj.client.GetStream();
                obj.buffer = new byte[obj.client.ReceiveBufferSize];
                obj.data = new StringBuilder();
                obj.handle = new EventWaitHandle(false, EventResetMode.AutoReset);
                if (Authorize())
                {
                    while (obj.client.Connected)
                    {
                        try
                        {
                            obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), null);
                            obj.handle.WaitOne();
                        }
                        catch (Exception ex)
                        {
                            Log(ErrorMsg(ex.Message));
                        }
                    }
                    obj.client.Close();
                    Connected(false);
                }
            }
            catch (Exception ex)
            {
                Log(ErrorMsg(ex.Message));
            }
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            if (connected)
            {
                obj.client.Close();
            }
            else if (client == null || !client.IsAlive)
            {
                string address = "0.tcp.ap.ngrok.io";
                string number = textBox5.Text.Trim();
                string username = usernameTextBox.Text.Trim();
                bool error = false;
                IPAddress ip = null;
                if (address.Length < 1)
                {
                    error = true;
                    Log(SystemMsg("Address is required"));
                }
                else
                {
                    try
                    {
                        IPAddress[] addresses = Dns.GetHostAddresses(address);
                        if (addresses.Length > 0)
                        {
                            ip = addresses[0];
                        }
                    }
                    catch
                    {
                        error = true;
                        Log(SystemMsg("Address is not valid"));
                    }
                }
                int port = -1;
                if (number.Length < 1)
                {
                    error = true;
                    Log(SystemMsg("Port number is required"));
                }
                else if (!int.TryParse(number, out port))
                {
                    error = true;
                    Log(SystemMsg("Port number is not valid"));
                }
                else if (port < 0 || port > 65535)
                {
                    error = true;
                    Log(SystemMsg("Port number is out of range"));
                }
                if (username.Length < 1)
                {
                    error = true;
                    Log(SystemMsg("Username is required"));
                }
                if (!error)
                {
                    // encryption key is optional
                    client = new Thread(() => Connection(ip, port, username))
                    {
                        IsBackground = true
                    };
                    client.Start();
                }
            }
        }
        private void Write(IAsyncResult result)
        {
            if (obj.client.Connected)
            {
                try
                {
                    obj.stream.EndWrite(result);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }
        }

        private void BeginWrite(string msg)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(msg);
            if (obj.client.Connected)
            {
                try
                {
                    obj.stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(Write), null);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                }
            }
        }

        private void Send(string msg)
        {
            if (send == null || send.IsCompleted)
            {
                send = Task.Factory.StartNew(() => BeginWrite(msg));
            }
            else
            {
                send.ContinueWith(antecendent => BeginWrite(msg));
            }
        }
        private void SendTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (sendTextBox.Text.Length > 0)
                {
                    string msg = sendTextBox.Text;
                    sendTextBox.Clear();
                    Log(string.Format("{0} (You): {1}", obj.username, msg));
                    if (connected)
                    {
                        Send(msg);
                    }
                }
            }
        }
        private void Client_FormClosing(object sender, FormClosingEventArgs e)
        {
            exit = true;
            if (connected)
            {
                obj.client.Close();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var db = FirestoreHelper.Database;
            DocumentReference docRef = db.Collection("RoomData").Document(id);
            RoomData data = docRef.GetSnapshotAsync().Result.ConvertTo<RoomData>();
            if (data.URL == null) MessageBox.Show("Host not yet added url");
            else
            {
                string tmp = "";
                for(int i=17;i<data.URL.Length;i++)
                {
                    tmp+= data.URL[i];
                }
                textBox5.Text = tmp;
            }

        }

        private void button6_Click(object sender, EventArgs e)
        {
            ImageList emojiImageList = new ImageList();
            emojiImageList.ImageSize = new Size(20, 20);

            string emojiDirectory = "Emoji";
            string[] emojiFiles = Directory.GetFiles(emojiDirectory);

            foreach (string emojiFile in emojiFiles)
            {
                emojiImageList.Images.Add(Image.FromFile(emojiFile));
            }

            listView1.LargeImageList = emojiImageList;

            for (int i = 0; i < emojiImageList.Images.Count; i++)
            {
                ListViewItem item = new ListViewItem();
                item.ImageIndex = i; // Sử dụng chỉ số của hình ảnh trong ImageList
                listView1.Items.Add(item);
            }
        }
        // Hàm này chuyển đổi chỉ số hình ảnh trong ImageList thành ký tự emoji tương ứng
        private string GetEmojiText(int imageIndex)
        {
            // Dựa vào chỉ số hình ảnh trong ImageList, chúng ta sẽ trả về emoji tương ứng
            switch (imageIndex)
            {
                case 4:
                    return "😀";
                case 1:
                    return "😢";
                case 0:
                    return "😡";
                case 6:
                    return "😎";
                case 8:
                    return "🤔";
                case 7:
                    return "😲";
                case 9:
                    return "😜";
                case 5:
                    return "😏";
                case 2:
                    return "😍";
                case 3:
                    return "😱";
                default:
                    return "";
            }
        }
        private void listView1_MouseDoubleClick_1(object sender, MouseEventArgs e)
        {
            // Lấy ra mục đang được chọn trong ListView
            ListViewItem selectedItem = listView1.GetItemAt(e.X, e.Y);

            // Kiểm tra xem mục đã được chọn hay không
            if (selectedItem != null)
            {
                // Lấy ra chỉ số của hình ảnh trong ImageList
                int imageIndex = selectedItem.ImageIndex;

                // Kiểm tra xem chỉ số hình ảnh có hợp lệ không
                if (imageIndex >= 0 && imageIndex < listView1.LargeImageList.Images.Count)
                {
                    // Lấy hình ảnh từ ImageList dựa trên chỉ số
                    Image emojiImage = listView1.LargeImageList.Images[imageIndex];

                    // Hiển thị emoji trong textbox
                    if (emojiImage != null)
                    {
                        // Chèn emoji vào vị trí hiện tại của con trỏ trong textbox
                        int selectionStart = sendTextBox.SelectionStart;
                        sendTextBox.Text = sendTextBox.Text.Insert(selectionStart, GetEmojiText(imageIndex));
                        sendTextBox.SelectionStart = selectionStart + 2; // Di chuyển con trỏ đến phía sau emoji vừa chèn
                    }
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Search_mess form = new Search_mess(id, textBox6.Text);
            form.ShowDialog();
        }
    }
}
