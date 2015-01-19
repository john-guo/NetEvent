using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NetEvent
{
    public sealed class ProtocolMessage
    {
        public const int Broadcast = 0;
        public const int Join = 1;
        public const int Start = 2;
        public const int Stop = 3;
    }


    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ProtocolIdAttribute : Attribute
    {
        public int id { get; private set; }

        public ProtocolIdAttribute(int id)
        {
            this.id = id;
        }

    }

    public static class ProtocolFactory
    {
        private static Protocol CreateProtocol(int id, int host)
        {
            var protocol = new Protocol();
            protocol.id = id;
            protocol.host = host;
            return protocol;
        }

        public static byte[] CreateProtocol<T>(T data, int host)
        {
            var attrs = typeof(T).GetCustomAttributes(typeof(ProtocolIdAttribute), false);
            var pid = attrs[0] as ProtocolIdAttribute;

            var p = ProtocolFactory.CreateProtocol(pid.id, host);

            using (var stream = new MemoryStream())
            {
                stream.Position = 0;
                ProtoBuf.Serializer.Serialize(stream, data);
                stream.Flush();
                p.data = stream.ToArray();

                stream.Seek(0, SeekOrigin.Begin);
                ProtoBuf.Serializer.Serialize(stream, p);
                stream.Flush();

                return stream.ToArray();
            }
        }

        public static Protocol GetProtocol(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return ProtoBuf.Serializer.Deserialize<Protocol>(stream);
            }
        }

        public static T GetSubProtocol<T>(Protocol p)
        {
            using (var stream = new MemoryStream(p.data))
            {
                return ProtoBuf.Serializer.Deserialize<T>(stream);
            }
        }

        public static T GetProtocol<T>(byte[] data)
        {
            var p = GetProtocol(data);

            var attrs = typeof(T).GetCustomAttributes(typeof(ProtocolIdAttribute), false);
            var pid = attrs[0] as ProtocolIdAttribute;

            if (pid.id != p.id)
                throw new NotSupportedException();

            return GetSubProtocol<T>(p);
        }
    }

    [ProtoContract]
    public class Protocol
    {
        [ProtoMember(1)]
        public int id;
        [ProtoMember(2)]
        public int host;
        [ProtoMember(3)]
        public byte[] data;
    }


    [ProtoContract]
    [ProtocolId(ProtocolMessage.Broadcast)]
    public class BroadcastProtocol
    {
        [ProtoMember(1)]
        public int ip;
        [ProtoMember(2)]
        public int port;
        [ProtoMember(3)]
        public int type;
    }


    [ProtoContract]
    [ProtocolId(ProtocolMessage.Join)]
    public class JoinProtocol
    {
        [ProtoMember(1)]
        public int ip;
    }

    [ProtoContract]
    [ProtocolId(ProtocolMessage.Start)]
    public class StartProtocol
    {
        [ProtoMember(1)]
        public int ip;
    }

    [ProtoContract]
    [ProtocolId(ProtocolMessage.Stop)]
    public class StopProtocol
    {
        [ProtoMember(1)]
        public int ip;
    }
}
