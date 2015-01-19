using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetEvent;
using System.Net;

namespace winFormTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Invoke2(Action a)
        {
            Invoke(a);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (start)
                return;

            UdpEvent.Instance.Find(1, b =>
            {
                Invoke2(() => 
                {
                    comboBox1.Items.Clear();
                });
                if (b != null)
                {
                    string v = String.Format("{0}:{1}", new IPAddress(b.ip), b.port);
                    Invoke2(() =>
                    {
                        timer1.Stop();
                        comboBox1.Items.Add(v);
                        comboBox1.SelectedIndex = 0;
                    });

                    UdpEvent.Instance.Connect(b.ip);
                }
                else
                {
                    Invoke2(() => timer1.Start());
                }
            });
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            //UdpEvent.Instance.OnLog += Instance_OnLog;

            UdpEvent.Instance.Serve(OnRecv);
            button1_Click(sender, e);
        }

        void Instance_OnLog(Protocol arg1, IPEndPoint arg2)
        {
            if (arg1.id == ProtocolMessage.Broadcast)
            {
                var b = ProtocolFactory.GetSubProtocol<BroadcastProtocol>(arg1);
                Invoke2(() => textBox1.AppendText(String.Format("{0} : {1}:{2} {3}" + Environment.NewLine, arg2.ToString(), b.ip, b.port, b.type)));
                return;
            }

            Invoke2(() => textBox2.AppendText(String.Format("{0} : {1} {2}" + Environment.NewLine, arg2.ToString(), arg1.id, arg1.host)));
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (start)
                return;
            UdpEvent.Instance.Host(1);
        }

        private void OnJoin(JoinProtocol p, bool self, int from)
        {
            if (start)
                return;

            count = 10;
            Invoke2(() =>
            {
                label2.Text = "Receive Join " + self;
                timer2.Start();
            });

        }

        private void OnStart(StartProtocol p, bool self, int from)
        {
            if (start)
                return;

            start = true;
            Invoke2(() =>
            {
                label1.Text = "0";
                label2.Text = "Start " + self;
                timer2.Stop();
                timer1.Stop();
            });
        }

        private void OnStop(StopProtocol p, bool self, int from)
        {
            if (!start)
                return;

            UdpEvent.Instance.Close();
            start = false;

            Invoke2(() =>
            {
                label2.Text = "Stop " + self;
                timer1.Stop();
            });
        }

        private void OnRecv(Protocol p, bool self, int from)
        {
            switch (p.id)
            {
                case ProtocolMessage.Join:
                    OnJoin(ProtocolFactory.GetSubProtocol<JoinProtocol>(p), self, from);
                    break;
                case ProtocolMessage.Start:
                    OnStart(ProtocolFactory.GetSubProtocol<StartProtocol>(p), self, from);
                    break;
                case ProtocolMessage.Stop:
                    OnStop(ProtocolFactory.GetSubProtocol<StopProtocol>(p), self, from);
                    break;
                default:
                    break;
            }
        }

        bool start = false;
        int count = 10;
        private void timer2_Tick(object sender, EventArgs e)
        {
            label1.Text = String.Format("{0}", count--);
            if (!UdpEvent.Instance.IsHost)
                return;
            if (count <= 0)
            {
                //START
                var data = new StartProtocol();
                data.ip = UdpEvent.Instance.Ip;
                UdpEvent.Instance.Notify(data);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var data = new StopProtocol();
            data.ip = UdpEvent.Instance.Ip;
            UdpEvent.Instance.Notify(data);
            //button1_Click(sender, e);
        }
    }
}
