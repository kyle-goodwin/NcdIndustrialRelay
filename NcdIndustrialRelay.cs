using System.Net.Sockets;

namespace NCD.IndustrialRelay;

class RelayController
{
    private Socket socket;

    public RelayController(Socket socket)
    {
        this.socket = socket;
    }

    public (bool, List<byte>) StartRelayTimer(byte timer, byte hours, byte minutes, byte seconds, byte relay)
    {
        List<byte> command = WrapInApi([254, 50, (byte)(49 + timer), hours, minutes, seconds, (byte)(relay - 1)]);
        return ProcessControlCommandReturn(SendCommand(command, 4));
    }

    public List<int> ReadSingleAd8(int channel)
    {
        List<byte> command = WrapInApi([254, (byte)(149 + channel)]);
        return TranslateAd(ProcessReadCommandReturn(SendCommand(command, 4)), 8);
    }

    private List<byte> AddChecksum(List<byte> data)
    {
        byte checksum = 0;
        for (int i = 0; i < data.Count; i++)
        {
            checksum = (byte)(checksum + data[i]);
        }
        data.Add(checksum);
        return data;
    }

    private List<byte> WrapInApi(List<byte> data)
    {
        int bytesInPacket = data.Count();
        data.Insert(0, (byte)bytesInPacket);
        data.Insert(0, 170);
        data = AddChecksum(data);
        return data;
    }

    // This deviates from the Python script in that it returns a list of bytes, rather than raw bytes. Meaning there is no need for hex_to_decimal function.
    private List<byte> SendCommand(List<byte> command, int bytes_back)
    {
        byte[] data = new byte[bytes_back];
        socket.Send(command.ToArray());
        socket.Receive(data);
        return data.ToList();
    }

    // This deviates from the Python script in that it throws exceptions rather than returning false from this function.
    private (bool, List<byte>) ProcessControlCommandReturn(List<byte> data)
    {
        if (!CheckHandshake(data)) { throw new Exception("handshake failed"); }
        else if (!CheckBytesBack(data)) { throw new Exception("bytesback failed"); }
        else if (!CheckChecksum(data)) { throw new Exception("checksum failed"); };
        return (true, data);
    }

    // This deviates from the Python script in that it throws exceptions rather than returning false from this function.
    private List<byte> ProcessReadCommandReturn(List<byte> data)
    {
        if (!CheckHandshake(data)) { throw new Exception("handshake failed"); }
        else if (!CheckBytesBack(data)) { throw new Exception("bytesback failed"); }
        else if (!CheckChecksum(data)) { throw new Exception("checksum failed"); };
        return GetPayload(data);
    }

    private List<byte> GetPayload(List<byte> data)
    {
        List<byte> payload = [];
        for (int i = 2; i < data.Count - 1; i++)
        {
            payload.Add(data[i]);
        }
        return payload;
    }

    private bool CheckHandshake(List<byte> data)
    {
        return data[0] == 170;
    }

    private bool CheckBytesBack(List<byte> data)
    {
        return data[1] == data.Count - 3;
    }

    private bool CheckChecksum(List<byte> data)
    {
        int sum = 0;
        for (int i = 0; i < data.Count - 1; i++)
        {
            sum += data[i];
        }
        return (byte)sum == data[data.Count - 1];
    }

    private List<int> TranslateAd(List<byte> data, int resolution)
    {
        List<int> readArray = [];
        if (resolution == 10)
        {
            for (int i = 0; i < data.Count; i += 2)
            {
                readArray.Add(((data[i] & 3) << 8) + data[i + 1]);
            }
        }
        else if (resolution == 8)
        {
            for (int i = 0; i < data.Count; i++)
            {
                readArray.Add(data[i]);
            }
        }
        return readArray;
    }
}
