namespace RedisClone;

using System.Net;
using System.Net.Sockets;
using System.Text;

public class Server
{
    private readonly TcpListener _listener = new TcpListener(IPAddress.Any, 6379);
    private readonly Dictionary<string, DBEntry> _entries = new();

    public void Start()
    {
        _listener.Start();
        Console.WriteLine("Redis clone is up on port 6379...");

        while (true)
        {
            var socket = _listener.AcceptSocket();
            ThreadPool.QueueUserWorkItem(ThreadProc, socket);
        }
    }

    private void ThreadProc(object? o)
    {
        if (o == null) return;
        
        var socket = (Socket)o;

        while (SocketConnected(socket))
        {
            var buff = new byte[1024];
            var length = socket.Receive(buff);
            var request = Encoding.UTF8.GetString(buff, 0, length);
            var args = ParseArgs(request);

            Eval(socket, args);
        }
    }
    
    private bool SocketConnected(Socket socket)
    {
        var part1 = socket.Poll(1000, SelectMode.SelectRead);
        var part2 = socket.Available == 0;

        return !(part1 && part2);
    }
    
    private List<string> ParseArgs(string input)
    {
        var parsedList = new List<string>();
        var numArgs = int.Parse(input.Substring(1, input.IndexOf("\r\n") - 1));
        var currentIndex = input.IndexOf("\r\n") + 2;

        for (var i = 0; i < numArgs; i++)
        {
            var argLength = int.Parse(input.Substring(currentIndex + 1, input.IndexOf("\r\n", currentIndex + 1) - currentIndex - 1));
            currentIndex = currentIndex + argLength.ToString().Length + 3;

            var argValue = input.Substring(currentIndex, argLength);
            parsedList.Add(argValue);

            currentIndex = currentIndex + argLength + 2;
        }

        return parsedList;
    }
    
    private void Eval(Socket socket, List<string> args)
    {
        var cmd = args[0].ToUpper();
        switch (cmd)
        {
            case "ECHO":
                socket.Send(Encoding.UTF8.GetBytes($"+{args[1]}\r\n"));
                break;
            case "PING":
                socket.Send(Encoding.UTF8.GetBytes("+PONG\r\n"));
                break;
            case "SET":
                SetValue(args, socket);
                break;
            case "GET":
                GetValue(args, socket);
                break;
            default:
                socket.Send(Encoding.UTF8.GetBytes("+UNKNOWN COMMAND\r\n"));
                break;

        }
    }
    
    private void SetValue(List<string> args, Socket socket)
    {
    
        DBEntry entry = new(args[1], args[2]);
        
        if (args.Count == 5 && args[3].ToUpper() == "PX")
        {
            var ttl = int.Parse(args[4]);
            entry.TTL = ttl;
        }

        _entries[args[1]] = entry;

        socket.Send(Encoding.UTF8.GetBytes("+OK\r\n"));
    }

    private void GetValue(List<string> args, Socket socket)
    {
        var key = args[1];
        if (_entries.ContainsKey(key))
        {
            var entry = _entries[key];
            
            if (entry.TTL == -1)
            {
                socket.Send(Encoding.UTF8.GetBytes($"+{entry.Value}\r\n"));
                return;
            }
            
            var expiry = entry.CreatedAt.AddMilliseconds(entry.TTL);
            
            if (expiry.CompareTo(DateTime.Now) == -1)
            {
                _entries.Remove(key);
                socket.Send(Encoding.UTF8.GetBytes("$-1\r\n"));
                return;
            }
            
            socket.Send(Encoding.UTF8.GetBytes($"+{entry.Value}\r\n"));
        }
        else
        {
            socket.Send(Encoding.UTF8.GetBytes("$-1\r\n"));
        }
    }
}