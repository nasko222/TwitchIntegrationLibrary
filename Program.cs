using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;

namespace TwitchIntegrationLibrary
{
    public class DOSTwitch
    {
        private short checkCon;
        private TwitchIRC myTwitchIRC;
        private bool amConnected;

        static void Main()
        {
            DOSTwitch dt = new DOSTwitch();
            dt.warmDOSTwitch();
        }

        private void warmDOSTwitch()
        {
            checkCon = 0;
            Console.Write("Enter OAauth: ");
            string setOAuth = Convert.ToString(Console.ReadLine());
            Console.Write("Enter Nickname: ");
            string setNickName = Convert.ToString(Console.ReadLine()).ToLower();
            this.myTwitchIRC = new TwitchIRC();
            myTwitchIRC.StartTwitch(setOAuth, setNickName);
            myTwitchIRC.myDOSTwitch = this;
            if (!this.myTwitchIRC.isConnected)
            {
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(30));
                return;
            }
            this.amConnected = true;
            this.myTwitchIRC.SendMsg("Twitch is connected!");
        checkTwitchMessage:
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(3));
            myTwitchIRC.Update();
            goto checkTwitchMessage;
        }

        public void chatMessageRecv(string theMSG)
        {
            string[] array = theMSG.Split(new string[]
		    {
			    "PRIVMSG"
		    }, StringSplitOptions.None);
                string[] array2 = array[0].Split(new string[]
		    {
			    "!"
		    }, StringSplitOptions.None);
                string[] array3 = array[1].Split(new string[]
		    {
			    ":"
		    }, StringSplitOptions.None);
            string text = array2[0].Replace(":", string.Empty);
            string text2 = array3[1];
            text2 = text2.ToUpper();
            Console.WriteLine(text + ": " + text2);
        }
    }

    public class TwitchIRC
    {
        public string oauth;
        public string nickName;
        public string channelName;
        public bool stopThreads;
        private Queue<string> commandQueue;
        public bool isConnected;
        private string server;
        private int port;
        private Thread outProc;
        private Thread inProc;
        private string buffer;
        private List<string> recievedMsgs;
        public DOSTwitch myDOSTwitch;

        public TwitchIRC()
        {
            this.commandQueue = new Queue<string>();
            this.server = "irc.chat.twitch.tv";
            this.port = 6667;
            this.buffer = string.Empty;
            this.recievedMsgs = new List<string>();
        }

        public void StartTwitch(string setOAuth, string setNickName)
        {
            this.oauth = setOAuth;
            this.nickName = setNickName;
            this.channelName = setNickName;
            this.StartIRC();
        }

        private void StartIRC()
        {
            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(this.server, this.port);
            if (!tcpClient.Connected)
            {
                return;
            }
            this.isConnected = true;
            NetworkStream networkStream = tcpClient.GetStream();
            StreamReader input = new StreamReader(networkStream);
            StreamWriter output = new StreamWriter(networkStream);
            output.WriteLine("PASS " + this.oauth);
            output.WriteLine("NICK " + this.nickName.ToLower());
            output.Flush();
            this.outProc = new Thread(delegate()
            {
                this.IRCOutputProcedure(output);
            });
            this.outProc.Start();
            this.inProc = new Thread(delegate()
            {
                this.IRCInputProcedure(input, networkStream);
            });
            this.inProc.Start();
            this.stopThreads = false;
        }

        private void IRCInputProcedure(TextReader input, NetworkStream networkStream)
        {
            while (!this.stopThreads)
            {
                if (networkStream.DataAvailable)
                {
                    this.buffer = input.ReadLine();
                    if (this.buffer.Contains("PRIVMSG #"))
                    {
                        object obj = this.recievedMsgs;
                        lock (obj)
                        {
                            this.recievedMsgs.Add(this.buffer);
                        }
                    }
                    if (this.buffer.StartsWith("PING "))
                    {
                        this.SendCommand(this.buffer.Replace("PING", "PONG"));
                    }
                    if (this.buffer.Split(new char[]
				{
					' '
				})[1] == "001")
                    {
                        this.SendCommand("JOIN #" + this.channelName);
                    }
                }
            }
        }

        private void IRCOutputProcedure(TextWriter output)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (!this.stopThreads)
            {
                object obj = this.commandQueue;
                lock (obj)
                {
                    if (this.commandQueue.Count > 0 && stopwatch.ElapsedMilliseconds > 1750L)
                    {
                        output.WriteLine(this.commandQueue.Peek());
                        output.Flush();
                        this.commandQueue.Dequeue();
                        stopwatch.Reset();
                        stopwatch.Start();
                    }
                }
            }
        }

        public void SendCommand(string cmd)
        {
            object obj = this.commandQueue;
            lock (obj)
            {
                this.commandQueue.Enqueue(cmd);
            }
        }

        public void SendMsg(string msg)
        {
            object obj = this.commandQueue;
            lock (obj)
            {
                this.commandQueue.Enqueue("PRIVMSG #" + this.channelName + " :" + msg);
            }
        }

        public void SendMsg(string msg, float delay)
        {
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(delay));
            this.SendMsg(msg);
        }

        public void Update()
        {
            object obj = this.recievedMsgs;
            lock (obj)
            {
                if (this.recievedMsgs.Count > 0)
                {
                    for (int i = 0; i < this.recievedMsgs.Count; i++)
                    {
                        myDOSTwitch.chatMessageRecv(this.recievedMsgs[i]);
                    }
                    this.recievedMsgs.Clear();
                }
            }
        }
    }
}
