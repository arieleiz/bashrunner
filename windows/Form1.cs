using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Diagnostics;

namespace bashrunner
{
    public partial class Form1 : Form
    {
        const int BUFFER_SIZE = 4096;
        const int PORT = 712;
        const string ConfigFileName = "bashrunner.cfg";
        const string LXSS_KEY = "!LXSS!";

        public Form1()
        {
            start_listen();
            load_aliases();
        }

        private void load_aliases()
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ConfigFileName);
                if(!File.Exists(path))
                {
                    path = ConfigFileName;
                    if (!File.Exists(path))
                        return;
                }
                
                foreach(var line in File.ReadAllLines(path))
                {
                    int ofs = line.IndexOf(' ');
                    if (ofs < 0)
                        continue;
                    _aliases.Add(line.Substring(0, ofs), line.Substring(ofs + 1));
                }
            }
            catch(Exception)
            {

            }
        }

        private void start_listen()
        {
            InitializeComponent();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.Bind(new IPEndPoint(IPAddress.Loopback, PORT));
            var args = new SocketAsyncEventArgs();
            args.Completed += SockAccepted;
            _socket.Listen(10);
            _socket.AcceptAsync(args);
        }

        private void SockAccepted(object sender, SocketAsyncEventArgs e)
        {
            Socket listener = (Socket)sender;
            do
            {
                try
                {
                    var sock = e.AcceptSocket;
                    var args = new SocketAsyncEventArgs();
                    args.SetBuffer(new byte[BUFFER_SIZE], 0, BUFFER_SIZE);
                    args.Completed += DataReceived;
                    sock.ReceiveAsync(args);
                }
                catch
                {
                    // handle any exceptions here;
                }
                finally
                {
                    e.AcceptSocket = null; // to enable reuse
                }
            } while (!listener.AcceptAsync(e));
        }

        UTF8Encoding utf8 = new UTF8Encoding();

        private void DataReceived(object sender, SocketAsyncEventArgs e)
        {
            var sock = (Socket)sender;
            var str = utf8.GetString(e.Buffer, 0, e.BytesTransferred);
            sock.Close();
            try
            {
                str = str.Trim();
                int idx = str.IndexOf('@');
                string args = "";
                if(idx >= 0)
                {
                    args = str.Substring(idx + 1);
                    str = str.Substring(0, idx);
                }

                while (args.Contains(LXSS_KEY))
                {
                    var tgt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lxss\\rootfs");
                    args = args.Replace(LXSS_KEY, tgt);
                }
                string workdir = "";
                idx = args.IndexOf('@');
                if (idx >= 0)
                {
                    workdir = args.Substring(0, idx);
                    args = args.Substring(idx + 1);
                }


                if (str.StartsWith("#"))
                {
                    string cmd;
                    if(str.CompareTo("#quit") == 0)
                    {
                        Application.Exit();
                        return;
                    }
                    if (_aliases.TryGetValue(str.Substring(1), out cmd))
                        str = cmd;
                }

                var si = new ProcessStartInfo() { FileName = str, Arguments = args, WorkingDirectory = workdir };
                System.Diagnostics.Process.Start(si);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "BashRunner");
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            value = false;
            if (!this.IsHandleCreated) CreateHandle();
            base.SetVisibleCore(value);
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        Socket _socket;
        Dictionary<string, string> _aliases = new Dictionary<string, string>();
    }
}
