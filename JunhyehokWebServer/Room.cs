using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JunhyehokWebServer
{
    class Room
    {
        private int roomId;
        private List<ClientHandle> clients;

        public int RoomId { get { return roomId; } }
        public List<ClientHandle> Clients { get { return clients; } }

        public Room(int roomId)
        {
            this.roomId = roomId;
            clients = new List<ClientHandle>();
        }

        public void AddClient(ClientHandle client)
        {
            clients.Add(client);
        }
        public void RemoveClient(ClientHandle client)
        {
            clients.Remove(client);
        }
    }
}
