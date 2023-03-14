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
Console.WriteLine($"Byte Order: {iBytes[0]} {iBytes[1]} {iBytes[2]} {iBytes[3]}");


int port = 7777;
int serverPort = 7777;
string serverAddress;
IPEndPoint publicEndPoint = null;

serverAddress = "34.96.147.159";

var args2 = CommandArguments.GetEnvironmentArguments();

args2.Get("-port", ref port);
args2.Get("-server-port", ref serverPort);
args2.Get("-server-address", ref serverAddress);

object lockObj = new();
WriteLine(serverAddress + ", " + serverPort);
IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(serverAddress), serverPort);
IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
var udpClient = new UdpClient(localEndPoint);

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


Queue<Packet> sendPackets = new();
Dictionary<ushort, Action<Packet>> msgHandles = new();

msgHandles[(ushort)MsgIds.PublicAddressRequest] = (packet) =>
{
    var req = Deserialize<PublicAddressRequest>(packet);

    PublicAddressResponse res = new PublicAddressResponse();
    res.address = packet.remoteEndPoint.Address.ToString();
    res.port = packet.remoteEndPoint.Port;
    res.sendTimestamp = req.sendTimestamp;

    SendMsg(packet.remoteEndPoint, (ushort)MsgIds.PublicAddressResponse, res);

    WriteLine($"Receive [{packet.remoteEndPoint}] Public Address Request");
};

msgHandles[(ushort)MsgIds.PublicAddressResponse] = (packet) =>
{
    var res = Deserialize<PublicAddressResponse>(packet);
    DateTime sendTime = DateTime.FromFileTime(res.sendTimestamp);
    var useTime = DateTime.UtcNow.Subtract(sendTime).TotalMilliseconds;
    publicEndPoint = new IPEndPoint(IPAddress.Parse(res.address), res.port);
    WriteLine($"This Public Address: {res.address}:{res.port} ({useTime:0.#}ms)");

};


msgHandles[(ushort)MsgIds.PingRequest] = (packet) =>
{
    var req = Deserialize<PingRequest>(packet);

    if (!(req.toAddress == publicEndPoint.Address.ToString() && req.toPort == publicEndPoint.Port))
        return;

    PingResponse res = new PingResponse();
    res.toAddress = req.toAddress;
    res.toPort = req.toPort;
    res.sendTimestamp = req.sendTimestamp;

    SendMsg(packet.remoteEndPoint, (ushort)MsgIds.PublicAddressResponse, res);

    WriteLine($"[{packet.remoteEndPoint}] Ping Request Target: {req.toAddress}:{req.toPort}");
};
msgHandles[(ushort)MsgIds.PingResponse] = (packet) =>
{
    var res = Deserialize<PingResponse>(packet);
    DateTime sendTime = DateTime.FromFileTime(res.sendTimestamp);
    var useTime = DateTime.UtcNow.Subtract(sendTime).TotalMilliseconds;
    WriteLine($"[{packet.remoteEndPoint}] Ping Response Target: {res.toAddress}:{res.toPort} ({useTime:0.#}ms)");
};

string line;

WriteLine($"Address: {localEndPoint.Address}, Port: {localEndPoint.Port}, Server Address: {serverAddress}, Remote Port: {serverPort}");

var sendTask = Task.Factory.StartNew(SendWorker, cancellationTokenSource.Token);
var receiveTask = Task.Factory.StartNew(ReceiveWorker, cancellationTokenSource.Token);


while (true)
{
    line = Console.ReadLine();
    if (!string.IsNullOrEmpty(line))
    {
        if (line == "exit")
        {
            break;
        }
        try
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0])
            {
                case "ip":
                    {
                        SendMsg(remoteEndPoint, (ushort)MsgIds.PublicAddressRequest, new PublicAddressRequest()
                        {
                            sendTimestamp = DateTime.UtcNow.ToFileTime(),
                        });
                    }
                    break;
                case "ping":
                    {
                        IPEndPoint targetEndPoint = IPEndPoint.Parse(parts[1]);

                        SendMsg(targetEndPoint, (ushort)MsgIds.PingRequest, new PingRequest()
                        {
                            toAddress = targetEndPoint.Address.ToString(),
                            toPort = targetEndPoint.Port,
                            sendTimestamp = DateTime.UtcNow.ToFileTime(),
                        });
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            WriteLine(ex.Message + "\n" + ex.StackTrace);
        }
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

                udpClient.Send(buffer, packet.remoteEndPoint);
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
            Packet packet = new Packet();
            packet.remoteEndPoint = result.RemoteEndPoint;

            var buffer = result.Buffer;
            int offset = 0;
            NetworkBit.ReadUInt16(buffer, ref offset, out length);

            NetworkBit.ReadUInt16(buffer, ref offset, out msgId);
            packet.msgId = msgId;
            packet.data = buffer;
            packet.dataOffset = offset;
            packet.dataLength = length - 2;

            WriteLine($"RemoteEndPoint: {result.RemoteEndPoint}, msgId: {msgId}");

            if (msgHandles.TryGetValue(msgId, out var handler))
            {
                handler(packet);
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


void SendMsg(IPEndPoint endPoint, ushort msgId, object msg)
{
    var data = Serialize(msg);
    SendMsgRaw(endPoint, msgId, data);
}

void SendMsgRaw(IPEndPoint endPoint, ushort msgId, byte[] data)
{
    Packet packet = new Packet();
    packet.msgId = msgId;
    packet.remoteEndPoint = endPoint;

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

byte[] Serialize(object msg)
{
    if (msg == null)
        return null;
    return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
}

T Deserialize<T>(Packet packet)
{
    if (packet.data == null)
        return default;
    string text = Encoding.UTF8.GetString(packet.data, packet.dataOffset, packet.dataLength);
    if (typeof(T) == typeof(string))
    {
        return (T)(object)text;
    }
    return JsonSerializer.Deserialize<T>(text);
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
    PublicAddressRequest = 1,
    PublicAddressResponse,
    PingRequest,
    PingResponse,
}

public class Packet
{
    public ushort msgId;
    public byte[] data;
    public IPEndPoint remoteEndPoint;
    public int dataOffset;
    public int dataLength;

}

class PublicAddressRequest
{
    public long sendTimestamp { get; set; }
}

class PublicAddressResponse
{
    public string address { get; set; }
    public int port { get; set; }
    public long sendTimestamp { get; set; }
}



class PingRequest
{
    public long sendTimestamp { get; set; }
    public string toAddress { get; set; }
    public int toPort { get; set; }
}

class PingResponse
{
    public string toAddress { get; set; }
    public int toPort { get; set; }
    public long sendTimestamp { get; set; }

}