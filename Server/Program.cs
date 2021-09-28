using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SocketTcpServer
{
    class Program
    {
        private static readonly Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static readonly List<Socket> clientSockets = new List<Socket>();
        private const int BUFFER_SIZE = 2048;
        private const int PORT = 100;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];
        private static int price;
        private static Timer timer;


        private static void TimeExpired()
		{
            Console.WriteLine("Bidding stopped. Final price: " + price);
		}

        static void Main()
        {
            Console.Title = "Server";
            SetupServer();

            Task.Delay(new TimeSpan(0, 0, 20)).ContinueWith(o => { TimeExpired(); });

            Console.ReadLine(); 
            CloseAllSockets();
        }

        private static void SetupServer()
        {
            Console.WriteLine("Welcome to the bidding app! Please place the lot's starting price: ");
            price = Convert.ToInt32(Console.ReadLine());
            Console.WriteLine("Awesome! Your lot's starting price is " + price + "\n");

            Console.WriteLine("Setting up server...");
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
            serverSocket.Listen(0);
            serverSocket.BeginAccept(AcceptCallback, null);
            Console.WriteLine("Server setup complete\n");
        }

        private static void CloseAllSockets()
        {
            foreach (Socket socket in clientSockets)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            serverSocket.Close();
        }

        private static void AcceptCallback(IAsyncResult AR)
        {
            Socket socket;

            try
            {
                socket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            clientSockets.Add(socket);
            socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);
            Console.WriteLine("Client connected, waiting for the bid...\n");
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        private static void ReceiveCallback(IAsyncResult AR)
        {
            Socket current = (Socket)AR.AsyncState;
            int received;

            try
            {
                received = current.EndReceive(AR);
            }
			catch (ObjectDisposedException)
			{
                return;
			}
            catch (SocketException)
            {
                Console.WriteLine("Client forcefully disconnected\n");
                current.Close();
                clientSockets.Remove(current);
                return;
            }

            byte[] recBuf = new byte[received];
            Array.Copy(buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);
            Console.WriteLine("Received bid: " + text);

            if (int.TryParse(text, out int n) && Convert.ToInt32(text) > price) 
            {
                Console.WriteLine("Request is a proper bid");
                price = Convert.ToInt32(text);
                byte[] data = Encoding.ASCII.GetBytes("Your bid was accepted");
                current.Send(data);
                Console.WriteLine("Confirmation Sent\n");
                Console.WriteLine("New lot's price: " + price + "\n");
            }
            else if (int.TryParse(text, out int m) && Convert.ToInt32(text) <= price)
            {
                Console.WriteLine("Bid is too low");
                byte[] data = Encoding.ASCII.GetBytes("Please place a higher bid");
                current.Send(data);
                Console.WriteLine("Warning Sent\n");
            }
            else if (text.ToLower() == "exit") 
            {
                current.Shutdown(SocketShutdown.Both);
                current.Close();
                clientSockets.Remove(current);
                Console.WriteLine("Client disconnected\n");
                return;
            }
            else
            {
                Console.WriteLine("Text is an invalid request");
                byte[] data = Encoding.ASCII.GetBytes("Invalid request");
                current.Send(data);
                Console.WriteLine("Warning Sent\n");
            }

            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
        }
    
    }
}