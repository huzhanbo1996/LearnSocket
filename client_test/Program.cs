using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using SocketUtiles;

namespace client_test
{
    
    class Program
    {
       
        static void Main(string[] args)
        {
            UtileClient utile = new UtileClient("127.0.0.1", 11000);
            var server = utile.CreateClientUntilDone();
            //
            DateTime t = System.DateTime.Now;
            
            while (true)
            {
                byte[] tmp = server.ReadAllData();
                if (tmp.Length == 0) continue;
                string str = Encoding.ASCII.GetString(tmp);
                Console.Write(Encoding.ASCII.GetString(tmp));
                server.AddDataToSend(Encoding.ASCII.GetBytes(str + " FROM CLIENT \n"));
                if ( (System.DateTime.Now - t).Minutes>=1  )
                {
                    break;
                }
            }
            utile.EndConnect(server);
        }
    }
}
