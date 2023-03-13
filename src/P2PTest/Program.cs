// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Net.Sockets;
using Yanmonet.NetSync;
using System.Text;
using System.Text.Json;
using Yanmonet.Multiplayer;

Console.WriteLine($"IsLittleEndian: {BitConverter.IsLittleEndian}");
int i = 1;
byte[] iBytes = BitConverter.GetBytes(i);
Console.WriteLine($"Int32 1, byte0: {iBytes[0]}, byte1: {iBytes[1]}, byte2: {iBytes[2]}, byte3: {iBytes[3]}");


int port = 7777;
int remotePort = 7777;
string remoteAddress;

remoteAddress = "34.96.147.159";

var args2 = CommandArguments.GetEnvironmentArguments();

args2.Get("-port", ref port);
args2.Get("-remote-port", ref remotePort);
args2.Get("-remote-address", ref remoteAddress);

object lockObj = new();
WriteLine(remoteAddress + ", " + remotePort);
IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteAddress), remotePort);
IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
var udpClient = new UdpClient(localEndPoint);

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


Queue<Packet> sendPackets = new();
Dictionary<ushort, Action<IPEndPoint, byte[], int, int>> msgHandles = new();

msgHandles[(ushort)MsgIds.GetPublicIPAndPort] = (endPoint, buffer, offset, length) =>
{

    GetPublicIPAndPort msg = new GetPublicIPAndPort();
    msg.address = endPoint.Address.ToString();
    msg.port = endPoint.Port;

    //string text = Encoding.UTF8.GetString(buffer, offset, buffer.Length - offset);
    var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));

    SendMsg(endPoint, (ushort)MsgIds.PublicIPAndPort, data);

    WriteLine($"Get Public IP: {endPoint.Address}, Port: {endPoint.Port}");

};

msgHandles[(ushort)MsgIds.PublicIPAndPort] = (endPoint, buffer, offset, length) =>
{
    string text = Encoding.UTF8.GetString(buffer, offset, length);
    var msg = JsonSerializer.Deserialize<GetPublicIPAndPort>(text);

    WriteLine($"This Public IP: {msg.address}, Port: {msg.port}");

};

string line;

WriteLine($"Address: {localEndPoint.Address}, Port: {localEndPoint.Port}, Remote Address: {remoteAddress}, Remote Port: {remotePort}");

var sendTask = Task.Factory.StartNew(SendWorker, cancellationTokenSource.Token);
var receiveTask = Task.Factory.StartNew(ReceiveWorker, cancellationTokenSource.Token);


while (true)
{
    line = Console.ReadLine();
    if (line == "exit")
    {
        break;
    }

    switch (line)
    {
        case "ip":
            {
                SendMsg(remoteEndPoint, (ushort)MsgIds.GetPublicIPAndPort, null);
            }
            break;
    }
}
cancellationTokenSource.Cancel();
sendTask.Wait();
receiveTask.Wait();

Console.WriteLine("Stop");


void SendWorker()
{

    Packet packet;

    while (true)
    {
        try
        {
            if (cancellationTokenSource.IsCancellationRequested)
                break;

            packet = null;
            lock (lockObj)
            {
                if (sendPackets.Count > 0)
                {
                    packet = sendPackets.Dequeue();
                }
            }

            if (packet != null)
            {
                int size = 2 + 2 + packet.dataLength;
                byte[] buffer = new byte[size];
                MemoryStream ms = new MemoryStream(buffer);

                NetworkBit.WriteUInt16(ms, (ushort)(2 + packet.dataLength));
                NetworkBit.WriteUInt16(ms, packet.msgId);
                if (packet.dataLength > 0)
                    ms.Write(packet.data, packet.dataOffset, packet.dataLength);

                udpClient.Send(buffer, packet.address);
            }
            else
            {
                Thread.Sleep(5);
            }

        }
        catch (Exception ex)
        {
            WriteLine(ex.ToString());
        }
    }
}

async void ReceiveWorker()
{
    UdpReceiveResult result;
    ushort msgId;
    ushort length;

    while (true)
    {
        try
        {
            if (cancellationTokenSource.IsCancellationRequested)
                break;

            result = await udpClient.ReceiveAsync();

            if (cancellationTokenSource.IsCancellationRequested)
                break;

            var buffer = result.Buffer;
            int offset = 0;
            NetworkBit.ReadUInt16(buffer, ref offset, out length);

            NetworkBit.ReadUInt16(buffer, ref offset, out msgId);

            WriteLine($"RemoteEndPoint: {result.RemoteEndPoint}, msgId: {msgId}");

            if (msgHandles.TryGetValue(msgId, out var handler))
            {
                handler(result.RemoteEndPoint, buffer, offset, length - 2);
            }
        }
        catch (Exception ex)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                break;
            WriteLine(ex.Message);
        }
    }
}


void SendMsg(IPEndPoint endPoint, ushort msgId, byte[] data)
{
    Packet packet = new Packet();
    packet.msgId = msgId;
    packet.address = endPoint;

    if (data != null)
    {
        packet.data = data;
        packet.dataOffset = 0;
        packet.dataLength = data.Length;
    }
    lock (lockObj)
    {
        sendPackets.Enqueue(packet);
    }
}

void WriteLine(string line)
{
    lock (lockObj)
    {
        Console.WriteLine(line);
    }
}

enum MsgIds
{
    GetPublicIPAndPort = 1,
    PublicIPAndPort = 2,
}

public class Packet
{
    public ushort msgId;
    public byte[] data;
    public IPEndPoint address;
    public int dataOffset;
    public int dataLength;
}

class GetPublicIPAndPort
{
    public string address { get; set; }
    public int port { get; set; }
}