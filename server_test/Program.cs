using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using SocketUtiles;
namespace server_test
{
    class Program
    {
        static void Main(string[] args)
        {
            UtileClient utile = new UtileClient("127.0.0.1", 11000);
            List<Client> server = utile.CreateServer();
            //

            while (server.Count < 2)
            {

            }
            int cnt = 0;
            while (true)
            { 
                foreach(var c in server.Skip(1))
                {
                    Thread.Sleep(1000);
                    byte[] tmp = c.ReadAllData();
                    //if (tmp.Length == 0) continue;
                    Console.Write(Encoding.ASCII.GetString(tmp));
                    Console.Write("Sent: " + "msg: " + cnt.ToString() + "\n");
                    c.AddDataToSend(Encoding.ASCII.GetBytes("msg: " + cnt.ToString()+"\n"));
                    cnt += 1;
                }
            }
            //utile.EndConnect(server);
        }
    }
}
