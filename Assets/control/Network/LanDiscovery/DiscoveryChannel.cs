using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 局域网发现用的 UDP 通道，只负责收发字节，不懂协议内容。
///
/// 每块在线网卡各建一个 socket：既加入组播组，也能往该网段的定向广播地址发包。
/// 这样 VPN、Hyper-V 之类的虚拟网卡不会把包“抢”到错误的路由上去。
/// 收包在后台线程，投递到队列里，由 Unity 主线程调用 TryDequeue 取走。
/// </summary>
public sealed class DiscoveryChannel
{
    public struct Packet
    {
        public readonly byte[] Data;
        public readonly int Length;
        public readonly IPEndPoint Remote;
        public readonly int SocketIndex;

        public Packet(byte[] data, int length, IPEndPoint remote, int socketIndex)
        {
            Data = data;
            Length = length;
            Remote = remote;
            SocketIndex = socketIndex;
        }
    }

    sealed class SocketEntry
    {
        public UdpClient Client;
        public IPAddress Broadcast;
    }

    readonly List<SocketEntry> sockets = new List<SocketEntry>();
    readonly ConcurrentQueue<Packet> incoming = new ConcurrentQueue<Packet>();

    ushort port;
    IPAddress group;
    volatile bool stopping;

    public bool IsRunning { get; private set; }

    public bool TryDequeue(out Packet packet)
    {
        return incoming.TryDequeue(out packet);
    }

    /// <summary>
    /// 按当前网卡建立接收通道。没有任何可用网卡（且没有回环）时返回 false。
    /// </summary>
    public bool Start(ushort discoveryPort, string multicastAddress)
    {
        Stop();

        port = discoveryPort;
        group = IPAddress.Parse(multicastAddress);
        stopping = false;

        foreach (var endpoint in CollectEndpoints())
        {
            try
            {
                sockets.Add(CreateSocket(endpoint));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LAN] 在 {endpoint.Address} 上建立发现通道失败：{e.Message}");
            }
        }

        if (sockets.Count == 0)
        {
            IsRunning = false;
            return false;
        }

        for (var i = 0; i < sockets.Count; i++)
        {
            var index = i;
            var entry = sockets[i];
            _ = Task.Run(() => ReceiveLoop(entry, index));
        }

        IsRunning = true;
        return true;
    }

    public void Stop()
    {
        stopping = true;
        IsRunning = false;

        foreach (var entry in sockets)
        {
            try { entry.Client.Close(); } catch (Exception) { /* 关闭时的异常可以直接忽略 */ }
        }
        sockets.Clear();

        while (incoming.TryDequeue(out _)) { }
    }

    /// <summary>
    /// 组播和每个网段的定向广播各发一份。任一网卡不支持组播或广播都不影响其它网卡。
    /// </summary>
    public void Send(byte[] data)
    {
        if (data == null || data.Length == 0) return;
        var groupEndPoint = new IPEndPoint(group, port);

        foreach (var entry in sockets)
        {
            SendTo(entry, groupEndPoint, data);
            if (entry.Broadcast != null) SendTo(entry, new IPEndPoint(entry.Broadcast, port), data);
        }
    }

    /// <summary>
    /// 用收到探测包的那块网卡原路回包，避免回包从别的网卡出去。
    /// </summary>
    public void Reply(in Packet packet, byte[] data)
    {
        if (data == null || data.Length == 0) return;
        if (packet.SocketIndex < 0 || packet.SocketIndex >= sockets.Count) return;
        SendTo(sockets[packet.SocketIndex], packet.Remote, data);
    }

    static void SendTo(SocketEntry entry, IPEndPoint endPoint, byte[] data)
    {
        try
        {
            entry.Client.Send(data, data.Length, endPoint);
        }
        catch (Exception)
        {
            // 网卡切换、广播被禁时发送会失败，发现功能降级即可，不影响游戏联机
        }
    }

    SocketEntry CreateSocket(EndpointInfo endpoint)
    {
        var client = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            client.ExclusiveAddressUse = false;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(endpoint.Address, port));
            client.JoinMulticastGroup(group, endpoint.Address);
            client.MulticastLoopback = true;
            client.EnableBroadcast = true;

            // Windows 上如果之前往已关闭的端口发过 UDP，会收到 ICMP 不可达，
            // 下一次 Receive 就抛 10054。关掉这个行为，让发现通道只关心真正的数据包。
            try
            {
                client.Client.IOControl(unchecked((int)0x9800000C), new byte[] { 0, 0, 0, 0 }, null);
            }
            catch (Exception)
            {
                // 非 Windows 平台没有这个控制码
            }

            return new SocketEntry
            {
                Client = client,
                Broadcast = endpoint.Mask != null ? GetBroadcastAddress(endpoint.Address, endpoint.Mask) : null,
            };
        }
        catch (Exception)
        {
            // 半途失败时把已经绑好的 socket 关掉，不要泄漏端口
            try { client.Close(); } catch (Exception) { }
            throw;
        }
    }

    async Task ReceiveLoop(SocketEntry entry, int index)
    {
        while (!stopping)
        {
            UdpReceiveResult result;
            try
            {
                result = await entry.Client.ReceiveAsync();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception)
            {
                if (stopping) return;
                // 网卡切换、端口暂时不可用之类的错误，稍后继续等
                await Task.Delay(100);
                continue;
            }

            if (result.Buffer == null || result.Buffer.Length == 0) continue;
            incoming.Enqueue(new Packet(result.Buffer, result.Buffer.Length, result.RemoteEndPoint, index));
        }
    }

    struct EndpointInfo
    {
        public IPAddress Address;
        public IPAddress Mask;
    }

    static List<EndpointInfo> CollectEndpoints()
    {
        var result = new List<EndpointInfo>();
        var seen = new HashSet<IPAddress>();
        IPAddress loopback = null;

        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (Exception)
        {
            return result;
        }

        foreach (var networkInterface in interfaces)
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up) continue;

            IPInterfaceProperties properties;
            try
            {
                properties = networkInterface.GetIPProperties();
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(unicast.Address))
                {
                    loopback = unicast.Address;
                    continue;
                }
                if (!seen.Add(unicast.Address)) continue;

                IPAddress mask = null;
                try
                {
                    mask = unicast.IPv4Mask;
                }
                catch (Exception)
                {
                    // 个别平台拿不到子网掩码，那就只发组播、不发定向广播
                }

                result.Add(new EndpointInfo
                {
                    Address = unicast.Address,
                    Mask = mask,
                });
            }
        }

        // 没有真实网卡时（比如离线单机调试）退回回环，至少同机多开还能互相发现
        if (result.Count == 0 && loopback != null)
        {
            result.Add(new EndpointInfo { Address = loopback, Mask = null });
        }

        return result;
    }

    static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
    {
        var addressBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        if (addressBytes.Length != 4 || maskBytes.Length != 4) return null;

        var broadcast = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            broadcast[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
        }
        return new IPAddress(broadcast);
    }
}
