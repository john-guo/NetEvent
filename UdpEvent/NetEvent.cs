using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.IO;
using System.Diagnostics;

namespace NetEvent
{
    public class UdpEvent
    {
        int _ip;
        IPAddress me;

        const int broadcastPort = 33333;
        const int serverPort = 44444;

        UdpClient bsendClient;
        UdpClient brecvClient;

        UdpClient ssendClient;
        UdpClient srecvClient;

        int host;
        int type;

        private static UdpEvent _event;
        public static UdpEvent Instance
        {
            get
            {
                return _event;
            }
        }

        static UdpEvent()
        {
            _event = new UdpEvent();
        }

        private UdpEvent()
        {
            bsendClient = new UdpClient(broadcastPort + 1);
            brecvClient = new UdpClient(broadcastPort);

            bsendClient.EnableBroadcast = true;
            bsendClient.DontFragment = true;
            brecvClient.EnableBroadcast = true;
            brecvClient.DontFragment = true;

            ssendClient = new UdpClient(serverPort + 1);
            srecvClient = new UdpClient(serverPort);

            ssendClient.EnableBroadcast = true;
            ssendClient.DontFragment = true;
            srecvClient.EnableBroadcast = true;
            srecvClient.DontFragment = true;

            _ip = GetIp();
            me = new IPAddress(_ip);

            host = 0;
            type = 0;
        }

        public event Action<Protocol, IPEndPoint> OnLog;
        
        public void Host(int type)
        {
            if (this.host != 0)
                return;

            this.type = type;

            var client = bsendClient;

            client.Connect(IPAddress.Broadcast, broadcastPort);

            BroadcastProtocol data = new BroadcastProtocol();
            data.ip = _ip;
            data.port = serverPort;
            data.type = type;

            var buf = ProtocolFactory.CreateProtocol(data, 0);
            client.Send(buf, buf.Length);
        }

        public int Ip
        {
            get { return _ip; }
        }

        public bool IsHost
        {
            get { return host == 0; }
        }

        public void Connect(int host)
        {
            if (this.host != 0)
                return;

            this.host = host;

            var data = new JoinProtocol();
            data.ip = host;
            
            UdpEvent.Instance.Notify(data);
        }

        public void Close()
        {
            host = 0;
        }

        private int GetIp(IPAddress addr)
        {
            using (var stream = new MemoryStream(addr.GetAddressBytes()))
            {
                var reader = new BinaryReader(stream);
                return reader.ReadInt32();
            }
        }

        private int GetIp()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var i in interfaces)
            {
                if (i.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                if (i.OperationalStatus != OperationalStatus.Up)
                    continue;

                var ips = i.GetIPProperties().UnicastAddresses;

                foreach (var ip in ips)
                {
                    if (ip.Address.IsIPv6LinkLocal)
                        continue;

                    return GetIp(ip.Address);
                }
            }

            return 0;
        }

        private void LogReceiveCallback(IAsyncResult ar)
        {
            var end = new IPEndPoint(IPAddress.Any, 0);
            var bytes = brecvClient.EndReceive(ar, ref end);
            brecvClient.BeginReceive(LogReceiveCallback, null);
            OnLog(ProtocolFactory.GetProtocol(bytes), end);
        }

        public void Find(int type, Action<BroadcastProtocol> callback, int findTimeout = 1000)
        {
            var client = brecvClient;
            var wait = true;
            var ia = client.BeginReceive(ar => 
            {
                var end = new IPEndPoint(IPAddress.Any, 0);
                var bytes = client.EndReceive(ar, ref end);
                if (OnLog != null)
                {
                    client.BeginReceive(LogReceiveCallback, null);
                    OnLog(ProtocolFactory.GetProtocol(bytes), end);
                    return;
                }

                if (!wait)
                {
                    return;
                }
                if (end.Address.Equals(me))
                {
                    Debug.WriteLine("me");
                    callback(null);
                    return;
                }

                var b = ProtocolFactory.GetProtocol<BroadcastProtocol>(bytes);
                if (b.type == type)
                    callback(b);
                else
                    callback(null);
            }, null);
            if (!ia.AsyncWaitHandle.WaitOne(findTimeout))
            {
                wait = false;
                callback(null);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var end = new IPEndPoint(IPAddress.Any, 0);
            var bytes = srecvClient.EndReceive(ar, ref end);
            var callback = ar.AsyncState as Action<Protocol, bool, int>;
            var self = false;
            if (end.Address.Equals(me))
                self = true;
            int from = GetIp(end.Address);

            srecvClient.BeginReceive(ReceiveCallback, callback);

            if (OnLog != null)
            {
                OnLog(ProtocolFactory.GetProtocol(bytes), end);
                return;
            }

            if (!self && host != 0 && host != from)
                return;

            var p = ProtocolFactory.GetProtocol(bytes);
            if (p.host != 0 && p.host != _ip && p.host != host)
                return;

            callback(p, self, from);
        }

        public void Serve(Action<Protocol, bool, int> callback)
        {
            srecvClient.BeginReceive(ReceiveCallback, callback);
        }

        public void Notify<T>(T data)
        {
            ssendClient.Connect(IPAddress.Broadcast, serverPort);

            var buf = ProtocolFactory.CreateProtocol(data, host == 0 ? _ip : host);
            ssendClient.Send(buf, buf.Length);
        }

    }
}
