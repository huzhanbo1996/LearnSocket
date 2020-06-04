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
                string tmp = server.ReadAllData();
                if (tmp == "") continue;
                Console.Write(tmp);
                server.AddDataToSend(tmp + " FROM CLIENT \n");
                if ( (t - System.DateTime.Now).Minutes>=1  )
                {
                    break;
                }
            }
            utile.EndConnect(server);
        }
    }
}
