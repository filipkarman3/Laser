using System;
using System.Collections.Generic;

using Riptide.Transports;
using Riptide;
using Riptide.Utils;

static class Driver {
    public static void Main(String[] args) {
        ServerBuilt server = new ServerBuilt(maxClientCount: 2);
        ClientBuilt client = new ClientBuilt("127.0.0.1");
        while (true) {
            Message msg = Message.Create(MessageSendMode.Reliable, 1);
            msg.AddString("hi");
            server.SendToAll(msg);
        }
    }
}

class ClientBuilt {
    Client client;

    public ClientBuilt(String ip, ushort port=7777) {
        client = new Client();
        // client.Connect($"{ip}:{port}");
        client.Connect($"127.0.0.1:7777");
    }

    public void FixedUpdate() {
        client.Update();
    }

    public void Send(Message msg) {
        client.Send(msg);
    }

    [MessageHandler(1)]
    public static void RecvMsg(Message msg) {
        Console.WriteLine(msg.GetString());
    }
}

class ServerBuilt {
    Server server;
    
    public ServerBuilt(ushort maxClientCount, ushort port=7777) {
        server = new Server();
        server.Start(port, maxClientCount);
    }

    public void FixedUpdate() {
        server.Update();
    }

    public void SendToOne(Message msg, ushort client) {
        server.Send(msg, client);
    }

    public void SendToAll(Message msg) {
        server.SendToAll(msg);
        Console.WriteLine(server.ClientCount);
    }
}