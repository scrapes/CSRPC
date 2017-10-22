using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace rpc_net_lib
{
    public class net_manager
    {

        public net_manager(IPEndPoint server, Type function_stack, bool startThread = true)
        {
            main_con_ipe = server;
            main_auth = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            net_main = new Thread(new ThreadStart(this.net_main_reciever));
            nfuncs = new net_functions(function_stack);
            if (startThread)
                net_main.Start();
        }

        public net_manager(Socket server_sock, Type function_stack, bool startThread = true)
        {
            if ((server_sock.SocketType != SocketType.Dgram) || (server_sock.ProtocolType != ProtocolType.Udp))
                throw (new Exception("False Socket Type"));

            main_auth = server_sock;
            net_main = new Thread(new ThreadStart(this.net_main_reciever));
            nfuncs = new net_functions(function_stack);
            has_authority = true;
            if (startThread)
                net_main.Start();
        }


        private Thread net_main;

        public bool has_authority = false;  //Will allow Execution of Blacklisted Functions -> Only intended for Server use

        private Socket main_auth;

        private IPEndPoint main_con_ipe;

        public object Execute(string net_event_name, object[] eventargs, EndPoint to, bool isvoid = false)
        {
            return nfuncs.netevent(net_event_name, eventargs, to, main_auth, isvoid);
        }

        public object Execute(string net_event_name, object[] eventargs, bool isvoid = false)
        {
            return nfuncs.netevent(net_event_name, eventargs, main_con_ipe, main_auth, isvoid);
        }

        public bool Init()
        {
            return (bool)Execute("_Init", new object[0], main_con_ipe);
        }

        public void Abort()
        {
            net_main.Abort();
        }

        private net_functions nfuncs;

        private void net_main_reciever()
        {
            while (true)
            {
                try
                {
                    EndPoint mp = new IPEndPoint(IPAddress.Any, 0);
                    byte[] buff = new byte[1024];
                    int rec = main_auth.ReceiveFrom(buff, ref mp);
                    if (rec > 0)
                    {
                        byte[] buffe = new byte[rec];
                        for (int i = 0; i < rec; i++)
                            buffe[i] = buff[i];

                        nfuncs.netevent(buffe, mp, has_authority, main_auth);
                    }
                }
                catch
                {
                    a:
                    try
                    {
                        if (!has_authority)
                            main_auth.Connect(main_con_ipe);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        goto a;
                    }
                }

            }
        }

        private class net_functions
        {

            public net_functions(Type function_stack)
            {
                netp = new net_event_proccessing(function_stack);
            }

            private class return_object
            {
                public string return_id;
                public object return_obj;

                public return_object(string rid, object robj)
                {
                    return_id = rid;
                    return_obj = robj;
                }
            }

            private List<return_object> return_list = new List<return_object>();

            private net_event_proccessing netp;

            private static string RandomString(Int64 Length)
            {
                var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                var stringChars = new char[Length];
                var random = new Random();

                for (int i = 0; i < stringChars.Length; i++)
                {
                    stringChars[i] = chars[random.Next(chars.Length)];
                }

                return new String(stringChars);
            }

            public object netevent(string net_event_name, object[] eventargs, EndPoint to, Socket sender, bool isvoid = false)
            {
                string rid = RandomString(32);

                sender.SendTo(net_event_parse(net_event_name, eventargs, rid), to);

                if (!isvoid)
                {
                    for (int i = 0; i < 500; i++)
                    {
                        foreach (return_object ri in return_list)
                            if (ri.return_id == rid)
                            {
                                return_list.Remove(ri);
                                return ri.return_obj;
                            }

                        Thread.Sleep(100);
                    }
                    throw (new Exception("NetEvent Return Timeout"));
                }

                return null;
            }

            public void netevent(byte[] arrived, EndPoint from, bool has_authority, Socket sender)
            {
                net_event_t main = net_event_parse(arrived);

                if (main.event_name == "___net___return")
                {
                    return_list.Add(new return_object((string)main.args[0], main.args[1]));
                    return;
                }

                net_event_t temp = netp.net_event_proccess(main);

                if(temp.main.Name == "_Init")
                {
                    temp.args = new object[] { from };
                }

                if (main != temp)
                {
                    object returnobj = null;
                    if (!has_authority)
                    {
                        if (!temp.blacklisted)
                            returnobj = temp.main.Invoke(null, temp.args);
                    }
                    else
                    {
                        returnobj = temp.main.Invoke(null, temp.args);
                    }

                    this.netevent("___net___return", new object[] { main.return_id, returnobj }, from, sender, true);
                }
                else
                {
                    throw new Exception("Function Not Defined");
                }
            }

            public static byte[] net_event_parse(string event_name, object[] args, string returnid)
            {
                byte[] buff = new byte[40];
                CopyInArray(UTF8Encoding.UTF8.GetBytes(event_name), ref buff, 0);
                AddToArray(UTF8Encoding.UTF8.GetBytes(returnid), ref buff);
                StringWriter sww = new StringWriter();
                XmlWriter writer = XmlWriter.Create(sww);

                XmlSerializer xss = new XmlSerializer(args.GetType());
                xss.Serialize(sww, args);
               


                AddToArray(UTF8Encoding.UTF8.GetBytes(sww.ToString()), ref buff);
                writer.Close();
                return buff;
            }

            public static net_event_t net_event_parse(byte[] orig)
            {
                net_event_t main = new net_event_t();

                main.event_name = UTF8Encoding.UTF8.GetString(orig, 0, 40).Replace("\0", string.Empty);
                main.return_id = UTF8Encoding.UTF8.GetString(orig, 40, 32).Replace("\0", string.Empty);

                XmlSerializer xss = new XmlSerializer(typeof(object[]));
                StringReader wss = new StringReader(UTF8Encoding.UTF8.GetString(orig, 40 + 32, orig.Length - (40 + 32)));
                XmlReader ls = XmlReader.Create(wss);
                main.args = (object[])xss.Deserialize(ls);
                return main;
            }

            private static void AddToArray(byte[] add, ref byte[] org)
            {
                byte[] bk = org;
                if (add != null)
                {
                    org = new byte[bk.Length + add.Length];
                    bk.CopyTo(org, 0);
                    add.CopyTo(org, bk.Length);
                }
            }

            private static void CopyInArray(byte[] add, ref byte[] org, int offset)
            {
                for (int i = offset; i < org.Length; i++)
                {
                    if (i - offset < add.Length)
                        org[i] = add[i - offset];
                }
            }

            private static byte[] ObjectToByteArray(object obj)
            {
                if (obj == null)
                    return null;
                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream ms = new MemoryStream();
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }

            private static object ByteArrayToObject(byte[] arrBytes, int index, int offset)
            {
                MemoryStream memStream = new MemoryStream();
                BinaryFormatter binForm = new BinaryFormatter();
                memStream.Write(arrBytes, index, offset);
                memStream.Seek(0, SeekOrigin.Begin);
                object obj = (object)binForm.Deserialize(memStream);
                return obj;
            }
        }

        private class net_event_t
        {
            public string event_name;
            public object[] args;
            public bool blacklisted;
            public string return_id;

            public void add(object mm)
            {
                object[] bkk = args;
                args = new object[args.Length + 1];
                for (int i = 0; i < bkk.Length; i++)
                    args[i] = bkk[i];
                args[args.Length - 1] = mm;
            }

            public MethodInfo main;

            public net_event_t(string eve, MethodInfo invk, bool black)
            {
                event_name = eve;
                main = invk;
                blacklisted = black;
            }

            public net_event_t()
            {

            }
        }

        private class net_event_proccessing
        {
            public net_event_proccessing(Type function_stack)
            {
                foreach (MethodInfo ls in function_stack.GetMethods())
                {
                    bool bl = false;
                    object[] ms = ls.GetCustomAttributes(true);
                    foreach (object ob in ms)
                        if (ob.GetType() == typeof(SERVER))
                            bl = true;

                    events.Add(new net_event_t(ls.Name, ls, bl));
                }
            }
            public List<net_event_t> events = new List<net_event_t>();

            public net_event_t net_event_proccess(net_event_t main)
            {
                for (int i = 0; i < events.Count; i++)
                    if (events[i].event_name == main.event_name)
                    {
                        net_event_t tmmm = events[i];
                        tmmm.args = main.args;
                        return tmmm;
                    }

                return main;
            }
        }
    }

    public class SERVER : Attribute
    {

    }
}
